using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Xunit;
using CDR.DataHolder.IntegrationTests.Infrastructure;
using CDR.DataHolder.Repository.Infrastructure;
using CDR.DataHolder.IntegrationTests.Fixtures;
using CDR.DataHolder.IntegrationTests.Infrastructure.API2;
using CDR.DataHolder.IntegrationTests.Models.EnergyAccountsResponse;

#nullable enable

namespace CDR.DataHolder.IntegrationTests
{
    public class US27571_MDH_EnergyAPI_GetAccounts : BaseTest, IClassFixture<RegisterSoftwareProductFixture>
    {
        const string SCOPE_ACCOUNTS_BASIC_READ = "openid profile common:customer.basic:read energy:accounts.basic:read";
        const string SCOPE_WITHOUT_ACCOUNTS_BASIC_READ = "openid profile common:customer.basic:read";

        private RegisterSoftwareProductFixture Fixture { get; init; }

        public US27571_MDH_EnergyAPI_GetAccounts(RegisterSoftwareProductFixture fixture)
        {
            Fixture = fixture;
        }

        private static string? ToStringOrNull(DateTime? dateTime)
        {
            return dateTime?.ToString("yyyy-MM-dd");
        }

        private static (string, int) GetExpectedResponse(string? accessToken,
            string baseUrl, string selfUrl,
            bool? isOwned = null, string? openStatus = null, string? productCategory = null,
            int? page = null, int? pageSize = null)
        {
            ExtractClaimsFromToken(accessToken, out var customerId, out var softwareProductId);

            var effectivePage = page ?? 1;
            var effectivePageSize = pageSize ?? 25;

            using var dbContext = new DataHolderDatabaseContext(new DbContextOptionsBuilder<DataHolderDatabaseContext>().UseSqlServer(DATAHOLDER_CONNECTIONSTRING).Options);

            var accounts = dbContext.Accounts.AsNoTracking()
                .Include(account => account.Customer)
                .Where(account => account.Customer.CustomerId == new Guid(customerId))
                .Select(account => new
                {
                    accountId = IdPermanenceEncrypt(account.AccountId, customerId, softwareProductId),
                    accountNumber = account.AccountNumber,
                    displayName = account.DisplayName,
                    creationDate = account.CreationDate.ToString("yyyy-MM-dd"),
                    plans = account.AccountPlans.OrderBy(ap => ap.AccountPlanId).Select(accountPlan => new
                    {
                        nickname = accountPlan.Nickname,
                        servicePointIds = accountPlan.ServicePoints.OrderBy(sp => sp.ServicePointId).Select(sp => sp.ServicePointId).ToArray(),
                        planOverview = new
                        {
                            displayName = accountPlan.PlanOverview.DisplayName,
                            startDate = accountPlan.PlanOverview.StartDate.ToString("yyyy-MM-dd"),
                            endDate = ToStringOrNull(accountPlan.PlanOverview.EndDate)
                        }
                    }),
                })
                .ToList();

            var totalRecords = accounts.Count;

            // Paging
            accounts = accounts
                .OrderBy(account => account.displayName).ThenBy(account => account.accountId)
                .Skip((effectivePage - 1) * effectivePageSize)
                .Take(effectivePageSize)
                .ToList();

            var totalPages = (int)Math.Ceiling((double)totalRecords / effectivePageSize);

            const int MINPAGE = 1;
            if (page < MINPAGE)
            {
                throw new Exception($"Page {page} out of range. Min Page is {MINPAGE}");
            }
            var maxPage = ((totalRecords - 1) / pageSize) + 1;
            if (page > maxPage)
            {
                throw new Exception($"Page {page} out of range. Max Page is {maxPage} (Records={totalRecords}, PageSize={pageSize})");
            }

            var expectedResponse = new
            {
                data = new
                {
                    accounts,
                },
                links = new
                {
                    first = totalPages == 0 ? null : GetUrl(baseUrl, isOwned, openStatus, productCategory, 1, effectivePageSize),
                    last = totalPages == 0 ? null : GetUrl(baseUrl, isOwned, openStatus, productCategory, totalPages, effectivePageSize),
                    next = totalPages == 0 || effectivePage == totalPages ? null : GetUrl(baseUrl, isOwned, openStatus, productCategory, effectivePage + 1, effectivePageSize),
                    prev = totalPages == 0 || effectivePage == 1 ? null : GetUrl(baseUrl, isOwned, openStatus, productCategory, effectivePage - 1, effectivePageSize),
                    self = selfUrl,
                },
                meta = new
                {
                    totalRecords,
                    totalPages
                }
            };

            return (
                JsonConvert.SerializeObject(expectedResponse, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                }),

                totalRecords
            );
        }

        static string GetUrl(string baseUrl,
            bool? isOwned = null, string? openStatus = null, string? productCategory = null,
            int? queryPage = null, int? queryPageSize = null)
        {
            var query = new KeyValuePairBuilder();

            if (isOwned != null)
            {
                query.Add("is-owned", isOwned.Value ? "true" : "false");
            }

            if (openStatus != null)
            {
                query.Add("open-status", openStatus);
            }

            if (productCategory != null)
            {
                query.Add("product-category", productCategory);
            }

            if (queryPage != null)
            {
                query.Add("page", queryPage.Value);
            }

            if (queryPageSize != null)
            {
                query.Add("page-size", queryPageSize.Value);
            }

            return query.Count > 0 ?
                $"{baseUrl}?{query.Value}" :
                baseUrl;
        }

        private async Task Test_AC01_AC02_AC04_AC04_AC05_AC06_AC07(
            TokenType tokenType,
            bool? isOwned = null, string? openStatus = null, string? productCategory = null,
            int? queryPage = null, int? queryPageSize = null,
            int? expectedRecordCount = null)
        {
            // Arrange
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(tokenType, scope: SCOPE_ACCOUNTS_BASIC_READ);
            ExtractClaimsFromToken(accessToken, out var customerId, out var softwareProductId);

            var baseUrl = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts";
            var url = GetUrl(baseUrl, isOwned, openStatus, productCategory, queryPage, queryPageSize);

            (var expectedResponse, var totalRecords) = GetExpectedResponse(accessToken, baseUrl, url,
                isOwned, openStatus, productCategory,
                queryPage, queryPageSize);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = url,
                XV = "1",
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(HttpStatusCode.OK);

                // Assert - Check content type
                Assert_HasContentType_ApplicationJson(response.Content);

                // Assert - Check XV
                Assert_HasHeader(api.XV, response.Headers, "x-v");

                // Assert - Check x-fapi-interaction-id
                Assert_HasHeader(null, response.Headers, "x-fapi-interaction-id");

                // Assert - Check json
                await Assert_HasContent_Json(expectedResponse, response.Content);

                // Assert - Record count
                if (expectedRecordCount != null)
                {
                    totalRecords.Should().Be(expectedRecordCount);
                }
            }
        }

        [Theory]
        [InlineData(TokenType.MARY_MOSS)]
        public async Task AC01_Get_ShouldRespondWith_200OK_Accounts(TokenType tokenType)
        {
            await Test_AC01_AC02_AC04_AC04_AC05_AC06_AC07(tokenType);
        }


        [Theory]
        [InlineData("000", HttpStatusCode.BadRequest)]
        [InlineData("foo", HttpStatusCode.BadRequest)]
        [InlineData("", HttpStatusCode.BadRequest)]
        public async Task AC02_Get_WithInvalidXFAPIAuthDate_ShouldRespondWith_400BadRequest(string xFapiAuthDate, HttpStatusCode expectedStatusCode)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                XV = "1",
                XFapiAuthDate = xFapiAuthDate,
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check application/json
                    Assert_HasContentType_ApplicationJson(response.Content);

                    var expectedResponse = @"{
                        ""errors"": [{
                            ""code"": ""urn:au-cds:error:cds-all:Header/Invalid"",
                            ""title"": ""Invalid Header"",
                            ""detail"": ""x-fapi-auth-date"",
                            ""meta"": {}
                        }]
                    }";

                    await Assert_HasContent_Json(expectedResponse, response.Content);
                }
            }
        }

        [Theory]
        [InlineData(1001, HttpStatusCode.BadRequest)]
        public async Task AC03_Get_WithInvalidPageSize_ShouldRespondWith_400BadRequest(int pageSize, HttpStatusCode expectedStatusCode)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            var baseUrl = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts";
            var url = GetUrl(baseUrl, queryPageSize: pageSize);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = url,
                XV = "1",
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check application/json
                    Assert_HasContentType_ApplicationJson(response.Content);

                    var expectedResponse = @"{
                        ""errors"": [{
                            ""code"": ""urn:au-cds:error:cds-all:Field/InvalidPageSize"",
                            ""title"": ""Invalid Page Size"",
                            ""detail"": ""page-size pagination field is greater than the maximum allowed"",
                            ""meta"": {}
                        }]
                    }";

                    await Assert_HasContent_Json(expectedResponse, response.Content);
                }
            }
        }

        [Theory]
        [InlineData("foo", HttpStatusCode.BadRequest)]
        public async Task AC04_Get_WithInvalidXV_ShouldRespondWith_400BadRequest(string xv, HttpStatusCode expectedStatusCode)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                XV = xv,
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check application/json
                    Assert_HasContentType_ApplicationJson(response.Content);

                    var expectedResponse = @"{
                        ""errors"": [{
                            ""code"": ""urn:au-cds:error:cds-all:Header/InvalidVersion"",
                            ""title"": ""Invalid Version"",
                            ""detail"": ""Version header must be a positive integer"",
                            ""meta"": {}
                        }]
                    }";

                    await Assert_HasContent_Json(expectedResponse, response.Content);
                }
            }
        }

        [Theory]
        [InlineData("2", HttpStatusCode.NotAcceptable)]
        public async Task AC05_Get_WithUnsupportedXV_ShouldRespondWith_406NotAcceptable(string xv, HttpStatusCode expectedStatusCode)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                XV = xv,
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check application/json
                    Assert_HasContentType_ApplicationJson(response.Content);

                    var expectedResponse = @"{
                        ""errors"": [{
                            ""code"": ""urn:au-cds:error:cds-all:Header/UnsupportedVersion"",
                            ""title"": ""Unsupported Version"",
                            ""detail"": """",
                            ""meta"": {}
                        }]
                    }";

                    await Assert_HasContent_Json(expectedResponse, response.Content);
                }
            }
        }

        [Theory]
        [InlineData(0, HttpStatusCode.BadRequest)]
        public async Task AC06_Get_WithInvalidPage_ShouldRespondWith_400BadRequest(int page, HttpStatusCode expectedStatusCode)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            var baseUrl = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts";
            var url = GetUrl(baseUrl, queryPage: page);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = url,
                XV = "1",
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check application/json
                    Assert_HasContentType_ApplicationJson(response.Content);

                    var expectedResponse = @"{
                        ""errors"": [{
                            ""code"": ""urn:au-cds:error:cds-all:Field/Invalid"",
                            ""title"": ""Invalid Field"",
                            ""detail"": ""page"",
                            ""meta"": {}
                        }]
                    }";

                    await Assert_HasContent_Json(expectedResponse, response.Content);
                }
            }
        }

        [Theory]
        [InlineData(100, HttpStatusCode.UnprocessableEntity)]
        public async Task AC07_Get_WithInvalidPage_OutOfRange_ShouldRespondWith_422UnprocessableEntity(int page, HttpStatusCode expectedStatusCode)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            var baseUrl = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts";
            var url = GetUrl(baseUrl, queryPage: page);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = url,
                XV = "1",
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check application/json
                    Assert_HasContentType_ApplicationJson(response.Content);

                    var expectedResponse = @"{
                        ""errors"": [{
                            ""code"": ""urn:au-cds:error:cds-all:Field/InvalidPage"",
                            ""title"": ""Invalid Page"",
                            ""detail"": ""page parameter is out of range.  Maximum page is 1"",
                            ""meta"": {}
                        }]
                    }";

                    await Assert_HasContent_Json(expectedResponse, response.Content);
                }
            }
        }

        [Theory]
        [InlineData("1", HttpStatusCode.OK)]
        [InlineData("", HttpStatusCode.BadRequest)]
        [InlineData(null, HttpStatusCode.BadRequest)]
        public async Task AC08_Get_WithMissingXV_ShouldRespondWith_400BadRequest(string XV, HttpStatusCode expectedStatusCode)
        {
            await Test_ACX15_ACX16_ACX17(XV, expectedStatusCode, @"{
                ""errors"": [
                    {
                    ""code"": ""urn:au-cds:error:cds-all:Header/Missing"",
                    ""title"": ""Missing Required Header"",
                    ""detail"": ""x-v"",
                    ""meta"": {}
                    }
                ]
            }");
        }

        [Theory]
        [InlineData("DateTime.Now.RFC1123", HttpStatusCode.OK)]
        [InlineData(null, HttpStatusCode.BadRequest)]  // omit xfapiauthdate
        public async Task AC09_Get_WithMissingXFAPIAUTHDATE_ShouldRespondWith_400BadRequest(string XFapiAuthDate, HttpStatusCode expectedStatusCode)
        {
            await Test_ACX18_ACX19(GetDate(XFapiAuthDate), expectedStatusCode, @"{
                ""errors"": [
                    {
                    ""code"": ""urn:au-cds:error:cds-all:Header/Missing"",
                    ""title"": ""Missing Required Header"",
                    ""detail"": ""x-fapi-auth-date"",
                    ""meta"": {}
                    }
                ]
            }");
        }

        [Fact]
        public async Task ACX02_Get_WithPageSize5_ShouldRespondWith_200OK_Page1Of5Records()
        {
            await Test_AC01_AC02_AC04_AC04_AC05_AC06_AC07(TokenType.MARY_MOSS, queryPage: 1, queryPageSize: 5);
        }

        [Fact]
        public async Task ACX03_Get_WithPageSize5_AndPage3_ShouldRespondWith_200OK_Page3Of5Records()
        {
            await Test_AC01_AC02_AC04_AC04_AC05_AC06_AC07(TokenType.MARY_MOSS, queryPage: 3, queryPageSize: 5);
        }

        [Fact]
        public async Task ACX04_Get_WithPageSize5_AndPage5_ShouldRespondWith_200OK_Page5Of5Records()
        {
            await Test_AC01_AC02_AC04_AC04_AC05_AC06_AC07(TokenType.MARY_MOSS, queryPage: 5, queryPageSize: 5);
        }

        [Theory]
        [InlineData(SCOPE_ACCOUNTS_BASIC_READ, HttpStatusCode.OK)]
        [InlineData(SCOPE_WITHOUT_ACCOUNTS_BASIC_READ, HttpStatusCode.Forbidden)]
        public async Task ACX08_Get_WithoutEnergyAccountsReadScope_ShouldRespondWith_403Forbidden(string scope, HttpStatusCode expectedStatusCode)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS, scope);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                XV = "1",
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check application/json
                    Assert_HasContentType_ApplicationJson(response.Content);

                    var expectedResponse = @"{
                        ""errors"": [{
                            ""code"": ""urn:au-cds:error:cds-all:Authorisation/InvalidConsent"",
                            ""title"": ""Consent Is Invalid"",
                            ""detail"": ""The authorised consumer's consent is insufficient to execute the resource"",
                            ""meta"": {}
                        }]
                    }";

                    await Assert_HasContent_Json(expectedResponse, response.Content);
                }
            }
        }

        private async Task Test_ACX09_ACX11(TokenType tokenType, HttpStatusCode expectedStatusCode, string expectedWWWAuthenticateResponse)
        {
            // Arrange
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(tokenType);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                XV = "1",
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                // Assert - Check error response
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check error response 
                    Assert_HasHeader(expectedWWWAuthenticateResponse, response.Headers, "WWW-Authenticate");
                }
            }
        }

        [Theory]
        [InlineData(TokenType.MARY_MOSS, HttpStatusCode.OK)]
        [InlineData(TokenType.INVALID_FOO, HttpStatusCode.Unauthorized)]
        public async Task ACX09_Get_WithInvalidAccessToken_ShouldRespondWith_401Unauthorized(TokenType tokenType, HttpStatusCode expectedStatusCode)
        {
            // Arrange
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(tokenType);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                XV = "1",
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);
            }
        }

        [Theory]
        [InlineData(TokenType.MARY_MOSS, HttpStatusCode.OK)]
        [InlineData(TokenType.INVALID_EMPTY, HttpStatusCode.Unauthorized)]
        [InlineData(TokenType.INVALID_OMIT, HttpStatusCode.Unauthorized)]
        public async Task ACX11_Get_WithNoAccessToken_ShouldRespondWith_401Unauthorized(TokenType tokenType, HttpStatusCode expectedStatusCode)
        {
            await Test_ACX09_ACX11(tokenType, expectedStatusCode, "Bearer");
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        public async Task ACX10_Get_WithExpiredAccessToken_ShouldRespondWith_401Unauthorized(HttpStatusCode expectedStatusCode)
        {
            // Arrange
            var accessToken = BaseTest.EXPIRED_CONSUMER_ACCESS_TOKEN;

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                XV = "1",
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                // Assert - Check error response
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check error response
                    Assert_HasHeader(@"Bearer error=""invalid_token"", error_description=""The token expired at ",
                        response.Headers,
                        "WWW-Authenticate",
                        true); // starts with
                }
            }
        }

        private async Task Test_ACX12_ACX13_ACX14(Table table, string id, string status, HttpStatusCode expectedStatusCode, string? expectedErrorResponse = null)
        {
            if (table == Table.SOFTWAREPRODUCT)
            {
                await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS); // Ensure token cache is populated before changing status in case InlineData scenarios above are run/debugged out of order
            }

            var saveStatus = GetStatus(table, id);
            SetStatus(table, id, status);

            try
            {
                // Arrange
                var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

                // Act
                var api = new Infrastructure.API
                {
                    CertificateFilename = CERTIFICATE_FILENAME,
                    CertificatePassword = CERTIFICATE_PASSWORD,
                    HttpMethod = HttpMethod.Get,
                    URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                    XV = "1",
                    XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                    AccessToken = accessToken
                };
                var response = await api.SendAsync();

                // Assert
                using (new AssertionScope())
                {
                    // Assert - Check status code
                    response.StatusCode.Should().Be(expectedStatusCode);

                    // Assert - Check error response
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        await Assert_HasContent_Json(expectedErrorResponse, response.Content);
                    }
                }
            }
            finally
            {
                SetStatus(table, id, saveStatus);
            }
        }

        [Theory]
        [InlineData("ACTIVE", "Active", HttpStatusCode.OK)]
        [InlineData("INACTIVE", "Inactive", HttpStatusCode.Forbidden)]
        [InlineData("REMOVED", "Removed", HttpStatusCode.Forbidden)]
        public async Task ACX12_Get_WithADRSoftwareProductNotActive_ShouldRespondWith_403Forbidden_NotActiveErrorResponse(string status, string statusDescription, HttpStatusCode expectedStatusCode)
        {
            await Test_ACX12_ACX13_ACX14(Table.SOFTWAREPRODUCT, SOFTWAREPRODUCT_ID, status, expectedStatusCode, $@"{{ 
                ""errors"": [{{ 
                        ""code"": ""urn:au-cds:error:cds-all:Authorisation/AdrStatusNotActive"", 
                        ""title"": ""ADR Status Is Not Active"",
                        ""detail"": ""Software product status is { statusDescription }"", 
                        ""meta"": {{}} 
                }}]
            }}");
        }

        [Theory]
        [InlineData("ACTIVE", "Active", HttpStatusCode.OK)]
        [InlineData("INACTIVE", "Inactive", HttpStatusCode.Forbidden)]
        [InlineData("REMOVED", "Removed", HttpStatusCode.Forbidden)]
        public async Task ACX14_Get_WithADRParticipationNotActive_ShouldRespondWith_403Forbidden_NotActiveErrorResponse(string status, string statusDescription, HttpStatusCode expectedStatusCode)
        {
            await Test_ACX12_ACX13_ACX14(Table.LEGALENTITY, LEGALENTITYID, status, expectedStatusCode, $@"{{
                ""errors"": [{{
                    ""code"": ""urn:au-cds:error:cds-all:Authorisation/AdrStatusNotActive"",
                    ""title"": ""ADR Status Is Not Active"",
                    ""detail"": ""ADR status is { statusDescription }"",
                    ""meta"": {{}}
                }}]
            }}");
        }

        private async Task Test_ACX15_ACX16_ACX17(string XV, HttpStatusCode expectedStatusCode, string expectedErrorResponse)
        {
            // Arrange
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                XV = XV,
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                // Assert - Check error response
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check content type 
                    Assert_HasContentType_ApplicationJson(response.Content);

                    // Assert - Check error response
                    await Assert_HasContent_Json(expectedErrorResponse, response.Content);
                }
            }
        }

        [Theory]
        [InlineData("1", HttpStatusCode.OK)]
        [InlineData("2", HttpStatusCode.NotAcceptable)]
        public async Task ACX15_Get_WithXV2_ShouldRespondWith_406NotAcceptable(string XV, HttpStatusCode expectedStatusCode)
        {
            await Test_ACX15_ACX16_ACX17(XV, expectedStatusCode, @"{
                ""errors"": [
                    {
                    ""code"": ""urn:au-cds:error:cds-all:Header/UnsupportedVersion"",
                    ""title"": ""Unsupported Version"",
                    ""detail"": """",
                    ""meta"": {}
                    }
                ]
            }");
        }

        [Theory]
        [InlineData("1", HttpStatusCode.OK)]
        [InlineData("foo", HttpStatusCode.BadRequest)]
        [InlineData("99999999999999999999999999999999999999999999999999", HttpStatusCode.BadRequest)]
        [InlineData("-1", HttpStatusCode.BadRequest)]
        public async Task ACX16_Get_WithInvalidXV_ShouldRespondWith_400BadRequest(string XV, HttpStatusCode expectedStatusCode)
        {
            await Test_ACX15_ACX16_ACX17(XV, expectedStatusCode, @"{
                ""errors"": [
                    {
                    ""code"": ""urn:au-cds:error:cds-all:Header/InvalidVersion"",
                    ""title"": ""Invalid Version"",
                    ""detail"": ""Version header must be a positive integer"",
                    ""meta"": {}
                    }
                ]
            }");
        }

        private async Task Test_ACX18_ACX19(string? XFapiAuthDate, HttpStatusCode expectedStatusCode, string? expectedErrorResponse = null)
        {
            // Arrange
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                XV = "1",
                XFapiAuthDate = XFapiAuthDate,
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                // Assert - Check error response
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check content type 
                    Assert_HasContentType_ApplicationJson(response.Content);

                    // Assert - Check error response
                    await Assert_HasContent_Json(expectedErrorResponse, response.Content);
                }
            }
        }

        [Theory]
        [InlineData("DateTime.Now.RFC1123", HttpStatusCode.OK)]
        [InlineData("DateTime.UtcNow", HttpStatusCode.BadRequest)]
        [InlineData("foo", HttpStatusCode.BadRequest)]
        public async void ACX19_Get_WithInvalidXFAPIAUTHDATE_ShouldRespondWith_400BadRequest(string XFapiAuthDate, HttpStatusCode expectedStatusCode)
        {
            await Test_ACX18_ACX19(GetDate(XFapiAuthDate), expectedStatusCode, @"{
                ""errors"": [
                    {
                    ""code"": ""urn:au-cds:error:cds-all:Header/Invalid"",
                    ""title"": ""Invalid Header"",
                    ""detail"": ""x-fapi-auth-date"",
                    ""meta"": {}
                    }
                ]
            }");
        }

        [Theory]
        [InlineData("123", HttpStatusCode.OK)]
        public async Task ACX20_Get_WithXFAPIInteractionId123_ShouldRespondWith_200OK_Accounts_AndXFapiInteractionIDis123(string xFapiInteractionId, HttpStatusCode expectedStatusCode)
        {
            // Arrange
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                XV = "1",
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                XFapiInteractionId = xFapiInteractionId,
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                // Assert - Check x-fapi-interaction-id header
                Assert_HasHeader(xFapiInteractionId, response.Headers, "x-fapi-interaction-id");
            }
        }

        [Theory]
        [InlineData(CERTIFICATE_FILENAME, CERTIFICATE_PASSWORD, HttpStatusCode.OK)]
        [InlineData(ADDITIONAL_CERTIFICATE_FILENAME, ADDITIONAL_CERTIFICATE_PASSWORD, HttpStatusCode.Unauthorized)]  // Different holder of key
        public async Task ACX21_Get_WithDifferentHolderOfKey_ShouldRespondWith_401Unauthorized(string certificateFilename, string certificatePassword, HttpStatusCode expectedStatusCode)
        {
            // Arrange
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = certificateFilename,
                CertificatePassword = certificatePassword,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                XV = "1",
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                // Assert - Check error response
                if (expectedStatusCode != HttpStatusCode.OK)
                {
                    var expectedResponse = @"{
                        ""errors"": [
                            {
                                ""code"": ""401"",
                                ""title"": ""Unauthorized"",
                                ""detail"": ""invalid_token"",
                                ""meta"": {}
                            }
                        ]
                    }";

                    await Assert_HasContent_Json(expectedResponse, response.Content);
                }
            }
        }

        [Theory]
        [InlineData(USERID_MARYMOSS, ACCOUNTIDS_ALL_MARY_MOSS)] // All accounts
        [InlineData(USERID_MARYMOSS, ACCOUNTIDS_SUBSET_MARY_MOSS)] // Subset of accounts
        public async Task ACX22_Get_WhenConsumerDidNotGrantConsentToAllAccounts_ShouldRespondWith_200OK_ConsentedAccounts(string userId, string consentedAccounts)
        {
            static async Task<Response?> GetAccounts(string? accessToken)
            {
                var api = new Infrastructure.API
                {
                    CertificateFilename = CERTIFICATE_FILENAME,
                    CertificatePassword = CERTIFICATE_PASSWORD,
                    HttpMethod = HttpMethod.Get,
                    URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                    XV = "1",
                    XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                    AccessToken = accessToken
                };
                var response = await api.SendAsync();
                if (response.StatusCode != HttpStatusCode.OK) throw new Exception("Error getting accounts");

                var json = await response.Content.ReadAsStringAsync();

                var accountsResponse = JsonConvert.DeserializeObject<Response>(json);

                return accountsResponse;
            }

            // Arrange - Get authcode
            (var authCode, _, var codeVerifier) = await new DataHolder_Authorise_APIv2
            {
                UserId = userId,
                OTP = AUTHORISE_OTP,
                SelectedAccountIds = consentedAccounts,
            }.Authorise();

            // Act
            var tokenResponse = await DataHolder_Token_API.GetResponse(authCode, codeVerifier: codeVerifier);
            var accountsResponse = await GetAccounts(tokenResponse?.AccessToken);
            ExtractClaimsFromToken(tokenResponse?.AccessToken, out var custId, out var softwareProductId);
            var encryptedAccountIds = consentedAccounts.Split(',').Select(consentedAccountId => IdPermanenceEncrypt(consentedAccountId, custId, softwareProductId));

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check each account in response is one of the consented accounts
                foreach (var account in accountsResponse?.Data?.Accounts ?? throw new NullReferenceException())
                {
                    encryptedAccountIds.Should().Contain(account.AccountId);
                }
            }
        }

        [Theory]
        [InlineData(USERID_MARYMOSS, ACCOUNTIDS_ALL_MARY_MOSS)]
        public async Task ACX23_GetAccountsMultipleTimes_ShouldRespondWith_SameEncryptedAccountIds(string userId, string consentedAccounts)
        {
            static async Task<string?[]?> GetAccountIds(string userId, string consentedAccounts)
            {
                static async Task<Response?> GetAccounts(string? accessToken)
                {
                    var api = new Infrastructure.API
                    {
                        CertificateFilename = CERTIFICATE_FILENAME,
                        CertificatePassword = CERTIFICATE_PASSWORD,
                        HttpMethod = HttpMethod.Get,
                        URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts",
                        XV = "1",
                        XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                        AccessToken = accessToken
                    };
                    var response = await api.SendAsync();
                    if (response.StatusCode != HttpStatusCode.OK) throw new Exception("Error getting accounts");

                    var json = await response.Content.ReadAsStringAsync();

                    var accountsResponse = JsonConvert.DeserializeObject<Response>(json);

                    return accountsResponse;
                }

                // Get authcode
                (var authCode, _, var codeVerifier) = await new DataHolder_Authorise_APIv2
                {
                    UserId = userId,
                    OTP = AUTHORISE_OTP,
                    SelectedAccountIds = consentedAccounts,
                }.Authorise();

                // Get token
                var tokenResponse = await DataHolder_Token_API.GetResponse(authCode, codeVerifier: codeVerifier);

                // Get accounts
                var accountsResponse = await GetAccounts(tokenResponse?.AccessToken);

                // Return list of account ids
                return accountsResponse?.Data?.Accounts?.Select(x => x.AccountId).ToArray();
            }

            // Act - Get accounts
            var encryptedAccountIDs1 = await GetAccountIds(userId, consentedAccounts);

            // Act - Get accounts again
            var encryptedAccountIDs2 = await GetAccountIds(userId, consentedAccounts);

            // Assert
            using (new AssertionScope())
            {
                encryptedAccountIDs1.Should().NotBeNullOrEmpty();
                encryptedAccountIDs2.Should().NotBeNullOrEmpty();

                // Assert - Encrypted account ids should be same
                encryptedAccountIDs1.Should().BeEquivalentTo(encryptedAccountIDs2);
            }
        }
    }
}
