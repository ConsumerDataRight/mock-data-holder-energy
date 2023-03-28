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
using CDR.DataHolder.IntegrationTests.Models;

#nullable enable

namespace CDR.DataHolder.IntegrationTests
{
    public class US27571_MDH_EnergyAPI_GetAccounts : BaseTest, IClassFixture<RegisterSoftwareProductFixture>
    {
        const string SCOPE_ACCOUNTS_BASIC_READ = "openid profile common:customer.basic:read energy:accounts.basic:read";
        const string SCOPE_WITHOUT_ACCOUNTS_BASIC_READ = "openid profile common:customer.basic:read";
        private readonly string ENERGY_GET_ACCOUNTS_BASE_URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts";

        private RegisterSoftwareProductFixture Fixture { get; init; }

        public US27571_MDH_EnergyAPI_GetAccounts(RegisterSoftwareProductFixture fixture)
        {
            Fixture = fixture;
        }

        private static string? ToStringOrNull(DateTime? dateTime)
        {
            return dateTime?.ToString("yyyy-MM-dd");
        }

        private (string, int) GetExpectedResponse(string? accessToken,
            string baseUrl, string selfUrl,
            bool? isOwned = null, string? openStatus = null, string? productCategory = null,
            int? page = null, int? pageSize = null, string? apiVersion =null)
        {
            ExtractClaimsFromToken(accessToken, out var loginId, out var softwareProductId);

            var effectivePage = page ?? 1;
            var effectivePageSize = pageSize ?? 25;

            using var dbContext = new DataHolderDatabaseContext(new DbContextOptionsBuilder<DataHolderDatabaseContext>().UseSqlServer(DATAHOLDER_CONNECTIONSTRING).Options);

            var accounts = dbContext.Accounts.AsNoTracking()
                .Include(account => account.Customer)
                .Where(account => account.Customer.LoginId == loginId)
                .Where(account => account.OpenStatus == openStatus || (String.IsNullOrEmpty(openStatus) || openStatus.Equals("ALL", StringComparison.OrdinalIgnoreCase)))
                .Select(account => new
                {
                    accountId = IdPermanenceEncrypt(account.AccountId, loginId, softwareProductId),
                    accountNumber = account.AccountNumber,
                    displayName = account.DisplayName,
                    creationDate = account.CreationDate.ToString("yyyy-MM-dd"),
                    openStatus = (apiVersion == "2") ? account.OpenStatus : null,
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

        private string GetUrl(string baseUrl,
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

        private async Task Test_ValidGetAccountsScenario(
            TokenType tokenType,
            bool? isOwned = null, string? openStatus = null, string? productCategory = null,
            int? queryPage = null, int? queryPageSize = null,
            int? expectedRecordCount = null, string apiVersion = "1")
        {
            // Arrange
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(tokenType, scope: SCOPE_ACCOUNTS_BASIC_READ);

            var baseUrl = ENERGY_GET_ACCOUNTS_BASE_URL;
            var url = GetUrl(baseUrl, isOwned, openStatus, productCategory, queryPage, queryPageSize);

            (var expectedResponse, var totalRecords) = GetExpectedResponse(accessToken, baseUrl, url,
                isOwned, openStatus, productCategory,
                queryPage, queryPageSize, apiVersion:apiVersion);

            // Act
            var response = await GetAccounts(accessToken, url, apiVersion);
            
            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(HttpStatusCode.OK);

                // Assert - Check content type
                Assert_HasContentType_ApplicationJson(response.Content);

                // Assert - Check XV
                Assert_HasHeader(apiVersion.ToString(), response.Headers, "x-v");

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
        [InlineData(TokenType.MARY_MOSS, "1")]
        public async Task AC01_GetAccounts_ShouldRespondWith_200OK_Accounts(TokenType tokenType, string apiVersion)
        {
            await Test_ValidGetAccountsScenario(tokenType, apiVersion: apiVersion);
        }

        // Note: Covers US45029-AC01a, US45029-AC01b, US45029-AC01c and US45029-AC01d
        [Theory]
        [InlineData(TokenType.MARY_MOSS, "ALL",     "2")]
        [InlineData(TokenType.MARY_MOSS, "OPEN",    "2")]
        [InlineData(TokenType.MARY_MOSS, "CLOSED",  "2")]
        [InlineData(TokenType.MARY_MOSS, null,      "2")]
        public async Task AC01_GetAccountsV2_ShouldRespondWith_200OK_Accounts(TokenType tokenType, string? openStatus, string apiVersion)
        {
            await Test_ValidGetAccountsScenario(tokenType, apiVersion: apiVersion, openStatus: openStatus);
        }


        [Theory]
        [InlineData("000",                  HttpStatusCode.BadRequest,  "1", "Header/Invalid", "Invalid Header")]
        [InlineData("foo",                  HttpStatusCode.BadRequest,  "1", "Header/Invalid", "Invalid Header")]
        [InlineData("",                     HttpStatusCode.BadRequest,  "1", "Header/Invalid", "Invalid Header")]
        [InlineData("DateTime.Now.RFC1123", HttpStatusCode.OK,          "1", "Header/Missing", "Missing Required")]
        [InlineData(null,                   HttpStatusCode.BadRequest,  "1", "Header/Missing", "Missing Required Header")]
        [InlineData("DateTime.UtcNow",      HttpStatusCode.BadRequest,  "1", "Header/Invalid", "Invalid Header")]
        [InlineData("000",                  HttpStatusCode.BadRequest,  "2", "Header/Invalid", "Invalid Header")]
        [InlineData("foo",                  HttpStatusCode.BadRequest,  "2", "Header/Invalid", "Invalid Header")]
        [InlineData("",                     HttpStatusCode.BadRequest,  "2", "Header/Invalid", "Invalid Header")]
        [InlineData("DateTime.Now.RFC1123", HttpStatusCode.OK,          "2", "Header/Missing", "Missing Required")]
        [InlineData(null,                   HttpStatusCode.BadRequest,  "2", "Header/Missing", "Missing Required Header")]
        [InlineData("DateTime.UtcNow",      HttpStatusCode.BadRequest,  "2", "Header/Invalid", "Invalid Header")]

        public async Task AC02_AC09_Get_WithInvalidXFAPIAuthDate_ShouldRespondWith_400BadRequest(
            string xFapiAuthDate,
            HttpStatusCode expectedStatusCode,
            string apiVersion,
            string expectedErrorCode, 
            string expectedErrorTitle)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = ENERGY_GET_ACCOUNTS_BASE_URL,
                XV = apiVersion,
                XFapiAuthDate = GetDate(xFapiAuthDate),
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

                    var expectedResponse = $@"{{
                        ""errors"": [{{
                            ""code"": ""urn:au-cds:error:cds-all:{expectedErrorCode}"",
                            ""title"": ""{expectedErrorTitle}"",
                            ""detail"": ""x-fapi-auth-date"",
                            ""meta"": {{}}
                        }}]
                    }}";

                    await Assert_HasContent_Json(expectedResponse, response.Content);
                }
            }
        }  

        [Theory]
        [InlineData(null, 1001, HttpStatusCode.BadRequest,          "urn:au-cds:error:cds-all:Field/InvalidPageSize",   "Invalid Page Size", "page-size pagination field is greater than the maximum 1000 allowed",     "1")]
        [InlineData(100,  null, HttpStatusCode.UnprocessableEntity, "urn:au-cds:error:cds-all:Field/InvalidPage",       "Invalid Page",      "page parameter is out of range.  Maximum page is 1",                      "1")]
        [InlineData(0,    null, HttpStatusCode.BadRequest,          "urn:au-cds:error:cds-all:Field/Invalid",           "Invalid Field",     "Page parameter is out of range. Minimum page is 1, maximum page is 1000", "1")]
        [InlineData(null, 1001, HttpStatusCode.BadRequest,          "urn:au-cds:error:cds-all:Field/InvalidPageSize",   "Invalid Page Size", "page-size pagination field is greater than the maximum 1000 allowed",     "2")]
        [InlineData(100,  null, HttpStatusCode.UnprocessableEntity, "urn:au-cds:error:cds-all:Field/InvalidPage",       "Invalid Page",      "page parameter is out of range.  Maximum page is 1",                      "2")]
        [InlineData(0,    null, HttpStatusCode.BadRequest,          "urn:au-cds:error:cds-all:Field/Invalid",           "Invalid Field",     "Page parameter is out of range. Minimum page is 1, maximum page is 1000", "2")]
        public async Task AC03_AC06_AC07_Get_WithInvalidPageSizeOrPageSize(int? page, int? pageSize, HttpStatusCode expectedStatusCode, string expectedErrorCode, string expectedErrorTitle, string expectedErrorDetail, string apiVersion)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            var baseUrl = ENERGY_GET_ACCOUNTS_BASE_URL;
            var url = GetUrl(baseUrl, queryPageSize: pageSize, queryPage:page);

            // Act
            var response = await GetAccounts(accessToken, url, apiVersion: apiVersion);

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check application/json
                    Assert_HasContentType_ApplicationJson(response.Content);

                    var expectedResponse = $@"{{
                        ""errors"": [{{
                            ""code"": ""{expectedErrorCode}"",
                            ""title"": ""{expectedErrorTitle}"",
                            ""detail"": ""{expectedErrorDetail}"",
                            ""meta"": {{}}
                        }}]
                    }}";

                    await Assert_HasContent_Json(expectedResponse, response.Content);

                }
            }
        }


        private async Task<HttpResponseMessage> GetAccounts(string? accessToken, string url, string? apiVersion="1")
        {
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = url,
                XV = apiVersion,
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            return await api.SendAsync();
        }

        [Theory]
        [InlineData("foo",  HttpStatusCode.BadRequest,      "Header/InvalidVersion",        "Invalid Version",          "Version header must be a positive integer between 1 and 1000")]
        [InlineData("-1",   HttpStatusCode.BadRequest,      "Header/InvalidVersion",        "Invalid Version",          "Version header must be a positive integer between 1 and 1000")]
        [InlineData("3",    HttpStatusCode.NotAcceptable,   "Header/UnsupportedVersion",    "Unsupported Version",      "Version Requested is lower than the minimum version or greater than maximum version")]
        [InlineData("",     HttpStatusCode.BadRequest,      "Header/InvalidVersion",        "Invalid Version",          "Version header must be a positive integer between 1 and 1000")]
        [InlineData(null,   HttpStatusCode.BadRequest,      "Header/Missing",               "Missing Required Header", "An API version X-V Header is required but was not specified")]
        public async Task AC04_AC05_AC08_Get_WithInvalidXV_ShouldRespondWith_400BadRequest(
            string apiVersion,
            HttpStatusCode expectedStatusCode,
            string expectedErrorCode,
            string expectedErrorTitle,
            string expectedErrorDetail)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            // Act
            var response = await GetAccounts(accessToken, ENERGY_GET_ACCOUNTS_BASE_URL, apiVersion: apiVersion);
 
            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check application/json
                    Assert_HasContentType_ApplicationJson(response.Content);

                    var expectedResponse = $@"{{
                        ""errors"": [{{
                            ""code"": ""urn:au-cds:error:cds-all:{expectedErrorCode}"",
                            ""title"": ""{expectedErrorTitle}"",
                            ""detail"": ""{expectedErrorDetail}"",
                            ""meta"": {{}}
                        }}]
                    }}";


                    await Assert_HasContent_Json(expectedResponse, response.Content);
                }
            }
        }
       
        [Fact]
        public async Task ACX01_Get_WithPageSize5_ShouldRespondWith_200OK_Page1Of5Records()
        {
            await Test_ValidGetAccountsScenario(TokenType.MARY_MOSS, queryPage: 1, queryPageSize: 5);
        }

        [Fact]
        public async Task ACX02_Get_WithPageSize5_AndPage3_ShouldRespondWith_200OK_Page3Of5Records()
        {
            await Test_ValidGetAccountsScenario(TokenType.MARY_MOSS, queryPage: 3, queryPageSize: 5);
        }

        [Fact]
        public async Task ACX03_Get_WithPageSize5_AndPage5_ShouldRespondWith_200OK_Page5Of5Records()
        {
            await Test_ValidGetAccountsScenario(TokenType.MARY_MOSS, queryPage: 5, queryPageSize: 5);
        }

        [Theory]
        [InlineData(SCOPE_ACCOUNTS_BASIC_READ,          HttpStatusCode.OK,          "1")]
        [InlineData(SCOPE_WITHOUT_ACCOUNTS_BASIC_READ,  HttpStatusCode.Forbidden,   "1")]
        [InlineData(SCOPE_ACCOUNTS_BASIC_READ,          HttpStatusCode.OK,          "2")]
        [InlineData(SCOPE_WITHOUT_ACCOUNTS_BASIC_READ,  HttpStatusCode.Forbidden,   "2")]
        public async Task ACX04_Get_WithoutEnergyAccountsReadScope_ShouldRespondWith_403Forbidden(string scope, HttpStatusCode expectedStatusCode, string apiVersion)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS, scope);

            // Act
            var response = await GetAccounts(accessToken, ENERGY_GET_ACCOUNTS_BASE_URL, apiVersion:apiVersion);

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

        [Theory]
        [InlineData(TokenType.MARY_MOSS,    HttpStatusCode.OK,              "1")]
        [InlineData(TokenType.INVALID_FOO,  HttpStatusCode.Unauthorized,    "1")]
        [InlineData(TokenType.MARY_MOSS,    HttpStatusCode.OK,              "2")]
        [InlineData(TokenType.INVALID_FOO,  HttpStatusCode.Unauthorized,    "2")]
        public async Task ACX05_Get_WithInvalidAccessToken_ShouldRespondWith_401Unauthorized(TokenType tokenType, HttpStatusCode expectedStatusCode, string apiVersion)
        {
            // Arrange
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(tokenType);

            // Act
            var response = await GetAccounts(accessToken, ENERGY_GET_ACCOUNTS_BASE_URL, apiVersion:apiVersion);

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);
            }
        }

        [Theory]
        [InlineData(TokenType.MARY_MOSS,        HttpStatusCode.OK)]
        [InlineData(TokenType.INVALID_EMPTY,    HttpStatusCode.Unauthorized)]
        [InlineData(TokenType.INVALID_OMIT,     HttpStatusCode.Unauthorized)]
        public async Task ACX06_Get_WithNoAccessToken_ShouldRespondWith_401Unauthorized(TokenType tokenType, HttpStatusCode expectedStatusCode)
        {
         
            // Arrange
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(tokenType);

            // Act
            var response = await GetAccounts(accessToken, ENERGY_GET_ACCOUNTS_BASE_URL);

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                // Assert - Check error response
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Assert - Check error response 
                    Assert_HasHeader("Bearer", response.Headers, "WWW-Authenticate");
                }
            }

        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        public async Task ACX07_Get_WithExpiredAccessToken_ShouldRespondWith_401Unauthorized(HttpStatusCode expectedStatusCode)
        {
            // Arrange
            var accessToken = BaseTest.EXPIRED_CONSUMER_ACCESS_TOKEN;

            // Act
            var response = await GetAccounts(accessToken, ENERGY_GET_ACCOUNTS_BASE_URL);
           
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

        private async Task Test_ACX08_ACX09(EntityType entityType, string id, string status, HttpStatusCode expectedStatusCode, string? expectedErrorResponse = null)
        {
            var saveStatus = GetStatus(entityType, id);
            SetStatus(entityType, id, status);

            try
            {
                var accessToken = string.Empty;
                // Arrange
                try
                {
                    accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS, useCache:false); // Ensure token cache is populated before changing status in case InlineData scenarios above are run/debugged out of order
                }
                catch (AuthoriseException ex)
                {
                    // Assert
                    using (new AssertionScope())
                    {
                        // Assert - Check status code
                        ex.StatusCode.Should().Be(expectedStatusCode);

                        // Assert - Check error response
                        ex.Error.Should().Be("urn:au-cds:error:cds-all:Authorisation/AdrStatusNotActive");
                        ex.ErrorDescription.Should().Be(expectedErrorResponse);

                        return;
                    }
                }

                // Act
                var response = await GetAccounts(accessToken, ENERGY_GET_ACCOUNTS_BASE_URL);

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
                SetStatus(entityType, id, saveStatus);
            }
        }

        [Theory]
        [InlineData("ACTIVE",   HttpStatusCode.OK)]
        [InlineData("INACTIVE", HttpStatusCode.OK)]
        [InlineData("REMOVED",  HttpStatusCode.OK)]
        public async Task ACX08_Get_WithADRSoftwareProductNotActive_ShouldRespondWith_403Forbidden_NotActiveErrorResponse(string status, HttpStatusCode expectedStatusCode)
        {
            await Test_ACX08_ACX09(EntityType.SOFTWAREPRODUCT, SOFTWAREPRODUCT_ID, status, expectedStatusCode, $"ERR-GEN-002: Software product status is {status}");
        }

        [Theory]
        [InlineData("ACTIVE",   HttpStatusCode.OK)]
        [InlineData("INACTIVE", HttpStatusCode.OK)]
        [InlineData("REMOVED",  HttpStatusCode.OK)]
        public async Task ACX09_Get_WithADRParticipationNotActive_ShouldRespondWith_403Forbidden_NotActiveErrorResponse(string status, HttpStatusCode expectedStatusCode)
        {
            await Test_ACX08_ACX09(EntityType.LEGALENTITY, LEGALENTITYID, status, expectedStatusCode, $"ERR-GEN-002: Software product status is {status}");
        }

        [Theory]
        [InlineData("123", HttpStatusCode.OK, "1")]
        [InlineData("123", HttpStatusCode.OK, "2")]
        public async Task ACX10_Get_WithXFAPIInteractionId123_ShouldRespondWith_200OK_Accounts_AndXFapiInteractionIDis123(string xFapiInteractionId, HttpStatusCode expectedStatusCode, string apiVersion)
        {
            // Arrange
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            // Act
            //var response = await GetAccounts(accessToken, $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts");
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = ENERGY_GET_ACCOUNTS_BASE_URL,
                XV = apiVersion,
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
        [InlineData(CERTIFICATE_FILENAME,               CERTIFICATE_PASSWORD,            HttpStatusCode.OK)]
        [InlineData(ADDITIONAL_CERTIFICATE_FILENAME,    ADDITIONAL_CERTIFICATE_PASSWORD, HttpStatusCode.Unauthorized)]  // Different holder of key
        public async Task ACX11_Get_WithDifferentHolderOfKey_ShouldRespondWith_401Unauthorized(string certificateFilename, string certificatePassword, HttpStatusCode expectedStatusCode)
        {
            // Arrange
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            // Act
            //var response = await GetAccounts(accessToken, ENERGY_GET_ACCOUNTS_BASE_URL);
            var api = new Infrastructure.API
            {
                CertificateFilename = certificateFilename,
                CertificatePassword = certificatePassword,
                HttpMethod = HttpMethod.Get,
                URL = ENERGY_GET_ACCOUNTS_BASE_URL,
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
        public async Task ACX12_Get_WhenConsumerDidNotGrantConsentToAllAccounts_ShouldRespondWith_200OK_ConsentedAccounts(string userId, string consentedAccounts)
        {
             async Task<Response?> GetAccounts2(string? accessToken)
            {
                var response = await GetAccounts(accessToken, ENERGY_GET_ACCOUNTS_BASE_URL);

                if (response.StatusCode != HttpStatusCode.OK) throw new Exception("Error getting accounts");

                var json = await response.Content.ReadAsStringAsync();

                var accountsResponse = JsonConvert.DeserializeObject<Response>(json);

                return accountsResponse;
            }

            // Arrange - Get authcode
            (var authCode, _) = await new DataHolder_Authorise_APIv2
            {
                UserId = userId,
                OTP = AUTHORISE_OTP,
                SelectedAccountIds = consentedAccounts,
                RequestUri = await PAR_GetRequestUri(responseMode: "form_post")
            }.Authorise();

            // Act
            var tokenResponse = await DataHolder_Token_API.GetResponse(authCode);
            var accountsResponse = await GetAccounts2(tokenResponse?.AccessToken);
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
        public async Task ACX13_GetAccountsMultipleTimes_ShouldRespondWith_SameEncryptedAccountIds(string userId, string consentedAccounts)
        {
            async Task<string?[]?> GetAccountIds(string userId, string consentedAccounts)
            {
                async Task<Response?> GetAccounts(string? accessToken)
                {
                    var api = new Infrastructure.API
                    {
                        CertificateFilename = CERTIFICATE_FILENAME,
                        CertificatePassword = CERTIFICATE_PASSWORD,
                        HttpMethod = HttpMethod.Get,
                        URL = ENERGY_GET_ACCOUNTS_BASE_URL,
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
                (var authCode, _) = await new DataHolder_Authorise_APIv2
                {
                    UserId = userId,
                    OTP = AUTHORISE_OTP,
                    SelectedAccountIds = consentedAccounts,
                    RequestUri = await PAR_GetRequestUri(responseMode: "form_post")
                }.Authorise();

                // Get token
                var tokenResponse = await DataHolder_Token_API.GetResponse(authCode);

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

        [Theory]
        [InlineData("1", "1", "1",      HttpStatusCode.OK,            true)]  //Valid. Should return v1
        [InlineData("1", "2", "1",      HttpStatusCode.OK,            true)]  //Valid. Should return v1 - x-min-v is ignored when > x-v
        [InlineData("2", "1", "2",      HttpStatusCode.OK,            true)]  //Valid. Should return v2 - x-v is supported and higher than x-min-v 
        [InlineData("2", "2", "2",      HttpStatusCode.OK,            true)]  //Valid. Should return v2 - x-v is supported equal to x-min-v 
        [InlineData("3", "2", "2",      HttpStatusCode.OK,            true)]  //Valid. Should return v2 - x-v is NOT supported and x-min-v is supported
        [InlineData("2", "3", "2",      HttpStatusCode.OK,            true)]  //Valid. Should return v2 - x-min-v is ignored when > x-v (test using highest supported version)
        [InlineData("3", "3", "N/A",    HttpStatusCode.NotAcceptable, false)] //Invalid. Both x-v and x-min-v exceed MDHE supported version of 2

        public async Task ACX14_ApiVersionAndMinimumSupportedVersionScenarios(
            string apiVersion,
            string apiMinVersion,
            string expectedApiVersionResponse,
            HttpStatusCode expectedStatusCode, 
            bool isExpectedToBeSupported)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = ENERGY_GET_ACCOUNTS_BASE_URL,
                XV = apiVersion,
                XMinV = apiMinVersion,
                XFapiAuthDate = DateTime.Now.ToUniversalTime().ToString("r"),
                AccessToken = accessToken
            };
            var response = await api.SendAsync();

            // Assert
            using (new AssertionScope())
            {
                // Assert - Check status code
                response.StatusCode.Should().Be(expectedStatusCode);

                if (!isExpectedToBeSupported)
                {
                    // Assert - Check application/json
                    Assert_HasContentType_ApplicationJson(response.Content);

                    var expectedResponse = $@"{{
                        ""errors"": [{{
                            ""code"": ""urn:au-cds:error:cds-all:Header/UnsupportedVersion"",
                            ""title"": ""Unsupported Version"",
                            ""detail"": ""Version Requested is lower than the minimum version or greater than maximum version"",
                            ""meta"": {{}}
                        }}]
                    }}";

                    await Assert_HasContent_Json(expectedResponse, response.Content);
                }
                else
                {
                    // Assert - Check XV
                    Assert_HasHeader(expectedApiVersionResponse, response.Headers, "x-v");
                }

            }
        }  
    }
}
