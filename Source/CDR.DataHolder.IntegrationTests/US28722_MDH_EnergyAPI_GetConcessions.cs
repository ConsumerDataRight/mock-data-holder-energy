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

#nullable enable

namespace CDR.DataHolder.IntegrationTests
{
    public class US28722_MDH_EnergyAPI_GetConcessions : BaseTest, IClassFixture<RegisterSoftwareProductFixture>
    {
        const string SCOPE_ACCOUNTS_CONCESSIONS_READ = "openid energy:accounts.concessions:read";

        private RegisterSoftwareProductFixture Fixture { get; init; }

        public US28722_MDH_EnergyAPI_GetConcessions(RegisterSoftwareProductFixture fixture)
        {
            Fixture = fixture;
        }

        private static (string, int) GetExpectedResponse(
            string? accessToken,
            string accountId,
            string baseUrl, 
            string selfUrl,
            int? page = null, 
            int? pageSize = null)
        {
            ExtractClaimsFromToken(accessToken, out var customerId, out var softwareProductId);

            var effectivePage = page ?? 1;
            var effectivePageSize = pageSize ?? 25;

            using var dbContext = new DataHolderDatabaseContext(new DbContextOptionsBuilder<DataHolderDatabaseContext>().UseSqlServer(DATAHOLDER_CONNECTIONSTRING).Options);

            var concessions = dbContext.AccountConcessions.AsNoTracking()
                .Include(accountConcession => accountConcession.Account)
                .Where(accountConcession => accountConcession.Account.AccountId == accountId)
                .Select(accountConcession => new
                {
                    type = accountConcession.Type,
                    displayName = accountConcession.DisplayName,
                    additionalInfo = accountConcession.AdditionalInfo,
                    additionalInfoUri = accountConcession.AdditionalInfoUri,
                    startDate = (accountConcession.StartDate ?? DateTime.MinValue).ToString("yyyy-MM-dd") ?? "",
                    endDate = (accountConcession.EndDate ?? DateTime.MinValue).ToString("yyyy-MM-dd") ?? "",
                    discountFrequency = accountConcession.DiscountFrequency,
                    amount = accountConcession.Amount,
                    percentage = accountConcession.Percentage,
                    appliedTo = (accountConcession.AppliedTo ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries),
                })
                .ToList();

            int totalRecords = concessions.Count;

            // Paging
            concessions = concessions
                .OrderBy(accountConcession => accountConcession.displayName)
                .Skip((effectivePage - 1) * effectivePageSize)
                .Take(effectivePageSize)
                .ToList();

            var totalPages = (int)Math.Ceiling((double)totalRecords / effectivePageSize);

            var expectedResponse = new
            {
                data = new
                {
                    concessions,
                },
                links = new
                {
                    self = selfUrl,
                },
                meta = new
                {
                }
            };

            return (
                JsonConvert.SerializeObject(expectedResponse, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented
                }),

                totalRecords
            );
        }

        static string GetUrl(string baseUrl, int? queryPage = null, int? queryPageSize = null)
        {
            var query = new KeyValuePairBuilder();

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

        private async Task Test_AC01(
           TokenType tokenType,
           string accountId,
           int? queryPage = null, int? queryPageSize = null,
           int? expectedRecordCount = null)
        {
            // Arrange
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(tokenType, scope: SCOPE_ACCOUNTS_CONCESSIONS_READ);
            ExtractClaimsFromToken(accessToken, out var customerId, out var softwareProductId);

            var encryptedAccountId = IdPermanenceEncrypt(accountId, customerId, softwareProductId);  
            var baseUrl = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts/{encryptedAccountId}/concessions";
            var url = GetUrl(baseUrl, queryPage, queryPageSize);

            (var expectedResponse, var totalRecords) = GetExpectedResponse(accessToken,
                accountId,
                baseUrl, url,
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
        [InlineData(TokenType.MARY_MOSS, ACCOUNTID_MARY_MOSS)]
        public async Task AC01_Get_ShouldRespondWith_200OK_Concessions(TokenType tokenType, string accountId)
        {
            await Test_AC01(tokenType, accountId);
        }

        [Theory]
        [InlineData("000", HttpStatusCode.BadRequest)]
        [InlineData("foo", HttpStatusCode.BadRequest)]
        [InlineData("", HttpStatusCode.BadRequest)]
        public async Task AC02_Get_WithInvalidXFAPIAuthDate_ShouldRespondWith_400BadRequest(string xFapiAuthDate, HttpStatusCode expectedStatusCode)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);
            var accountId = ACCOUNTID_MARY_MOSS;

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts/{accountId}/concessions",
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
        [InlineData("foo", HttpStatusCode.BadRequest)]
        public async Task AC03_Get_WithInvalidXV_ShouldRespondWith_400BadRequest(string xv, HttpStatusCode expectedStatusCode)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);
            var accountId = ACCOUNTID_MARY_MOSS;

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts/{accountId}/concessions",
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
        public async Task AC04_Get_WithUnsupportedXV_ShouldRespondWith_406NotAcceptable(string xv, HttpStatusCode expectedStatusCode)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);
            var accountId = ACCOUNTID_MARY_MOSS;

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts/{accountId}/concessions",
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
        [InlineData("foo", HttpStatusCode.NotFound)]
        public async Task AC06_Get_WithInvalidAccountId_ShouldRespondWith_404NotFound(string accountId, HttpStatusCode expectedStatusCode)
        {
            // Arrange 
            var accessToken = await Fixture.DataHolder_AccessToken_Cache.GetAccessToken(TokenType.MARY_MOSS);

            // Act
            var api = new Infrastructure.API
            {
                CertificateFilename = CERTIFICATE_FILENAME,
                CertificatePassword = CERTIFICATE_PASSWORD,
                HttpMethod = HttpMethod.Get,
                URL = $"{DH_MTLS_GATEWAY_URL}/cds-au/v1/energy/accounts/{accountId}/concessions",
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
                            ""code"": ""urn:au-cds:error:cds-energy:Authorisation/InvalidEnergyAccount"",
                            ""title"": ""Invalid Energy Account"",
                            ""detail"": ""foo"",
                            ""meta"": {}
                        }]
                    }";

                    await Assert_HasContent_Json(expectedResponse, response.Content);
                }
            }
        }
    }
}
