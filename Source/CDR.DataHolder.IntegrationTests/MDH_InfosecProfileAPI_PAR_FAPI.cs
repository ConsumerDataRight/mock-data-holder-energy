using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Web;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using CDR.DataHolder.IntegrationTests.Infrastructure.API2;
using CDR.DataHolder.IntegrationTests.Fixtures;
using System.Net.Http.Headers;
using AutoMapper.Configuration.Annotations;

#nullable enable

namespace CDR.DataHolder.IntegrationTests
{
    public class USX00001_MDH_InfosecProfileAPI_PAR_FAPI : BaseTest, IClassFixture<RegisterSoftwareProductFixture>
    {
        [Fact]
        public async Task ACX01_FAPI_AudienceAsParUri_ShouldRespondWith_400BadRequest()
        {
            // Arrange
            var aud = BaseTest.DH_TLS_AUTHSERVER_BASE_URL + "/connect/par";

            // Act
            var response = await DataHolder_Par_API.SendRequest(scope: BaseTest.SCOPE, aud: aud);

            // Assert
            using (new AssertionScope())
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

                Assert_HasContentType_ApplicationJson(response.Content);

                var expectedResponse = @"{""error"":""invalid_client"",""error_description"":""ERR-JWT-001: client_assertion - Invalid audience""}";

                await Assert_HasContent_Json(expectedResponse, response.Content);
            }
        }

        [Fact]
        public async Task ACX02_FAPI_InvalidAudience_ShouldRespondWith_400BadRequest()
        {
            // Arrange
            var aud = "https://invalid.issuer";

            // Act
            var response = await DataHolder_Par_API.SendRequest(scope: BaseTest.SCOPE, aud: aud);

            // Assert
            using (new AssertionScope())
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

                Assert_HasContentType_ApplicationJson(response.Content);

                var expectedResponse = @"{""error"":""invalid_client"",""error_description"":""ERR-JWT-001: client_assertion - Invalid audience""}";

                await Assert_HasContent_Json(expectedResponse, response.Content);
            }
        }

        [Fact]
        public async Task ACX03_FAPI_NoNbfClaim_ShouldRespondWith_400BadRequest()
        {
            // Arrange

            // Act
            var response = await DataHolder_Par_API.SendRequest(scope: BaseTest.SCOPE, addNotBeforeClaim: false);

            // Assert
            using (new AssertionScope())
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

                Assert_HasContentType_ApplicationJson(response.Content);

                var expectedResponse = @"{""error"":""invalid_request_object"",""error_description"":""ERR-GEN-024: Invalid request - nbf is missing""}";

                await Assert_HasContent_Json(expectedResponse, response.Content);
            }
        }

        [Fact]
        public async Task ACX04_FAPI_NoExpClaim_ShouldRespondWith_400BadRequest()
        {
            // Arrange

            // Act
            var response = await DataHolder_Par_API.SendRequest(scope: BaseTest.SCOPE, addExpiryClaim: false);

            // Assert
            using (new AssertionScope())
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

                Assert_HasContentType_ApplicationJson(response.Content);

                var expectedResponse = @"{""error"":""invalid_request_object"",""error_description"":""ERR-JWT-004: request - token validation error""}";                

                await Assert_HasContent_Json(expectedResponse, response.Content);
            }
        }

        [Fact]
        public async Task ACX05_FAPI_ExpiredRequestObject_ShouldRespondWith_400BadRequest()
        {
            // Arrange

            // Act
            var response = await DataHolder_Par_API.SendRequest(scope: BaseTest.SCOPE, nbfOffsetSeconds: -3600, expOffsetSeconds: -3600);

            // Assert
            using (new AssertionScope())
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

                Assert_HasContentType_ApplicationJson(response.Content);

                var expectedResponse = @"{""error"":""invalid_request_object"",""error_description"":""ERR-JWT-002: request has expired""}";

                await Assert_HasContent_Json(expectedResponse, response.Content);
            }
        }

        [Fact]
        public async Task ACX06_FAPI_NbfGreaterThan60Mins_ShouldRespondWith_400BadRequest()
        {
            // Arrange

            // Act
            var response = await DataHolder_Par_API.SendRequest(scope: BaseTest.SCOPE, nbfOffsetSeconds: -3600);

            // Assert
            using (new AssertionScope())
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

                Assert_HasContentType_ApplicationJson(response.Content);

                var expectedResponse = @"{""error"":""invalid_request_object"",""error_description"":""ERR-GEN-025: Invalid request - exp claim cannot be longer than 60 minutes after the nbf claim""}";

                await Assert_HasContent_Json(expectedResponse, response.Content);
            }
        }

        [Fact]
        public async Task ACX07_FAPI_ExpGreaterThan60Mins_ShouldRespondWith_400BadRequest()
        {
            // Arrange

            // Act
            var response = await DataHolder_Par_API.SendRequest(scope: BaseTest.SCOPE, expOffsetSeconds: 3600);

            // Assert
            using (new AssertionScope())
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

                Assert_HasContentType_ApplicationJson(response.Content);

                var expectedResponse = @"{""error"":""invalid_request_object"",""error_description"":""ERR-GEN-025: Invalid request - exp claim cannot be longer than 60 minutes after the nbf claim""}";

                await Assert_HasContent_Json(expectedResponse, response.Content);
            }
        }

        [Fact]
        public async Task ACX08_FAPI_NoRequestObject_ShouldRespondWith_400BadRequest()
        {
            // Arrange

            // Act
            var response = await DataHolder_Par_API.SendRequest(scope: BaseTest.SCOPE, addRequestObject: false);

            // Assert
            using (new AssertionScope())
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

                Assert_HasContentType_ApplicationJson(response.Content);

                var expectedResponse = @"{""errors"":[{""code"":""urn:au-cds:error:cds-all:Field/Missing"",""title"":""Missing Required Field"",""detail"":""request"",""meta"":{}}]}";

                await Assert_HasContent_Json(expectedResponse, response.Content);
            }
        }

        [Fact]
        public async Task ACX09_FAPI_WithRequestUri_ShouldRespondWith_400BadRequest()
        {
            // Arrange

            // Act
            var response = await DataHolder_Par_API.SendRequest(scope: BaseTest.SCOPE, requestUri: Guid.NewGuid().ToString());

            // Assert
            using (new AssertionScope())
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

                Assert_HasContentType_ApplicationJson(response.Content);

                var expectedResponse = @"{""error"":""invalid_request"",""error_description"":""ERR-PAR-001: request_uri form parameter is not supported""}";

                await Assert_HasContent_Json(expectedResponse, response.Content);
            }
        }

        [Fact]
        public async Task ACX10_FAPI_WithInvalidRedirectUri_ShouldRespondWith_400BadRequest()
        {
            // Arrange

            // Act
            var response = await DataHolder_Par_API.SendRequest(scope: BaseTest.SCOPE, redirectUri: "https://junk.com/invalid");

            // Assert
            using (new AssertionScope())
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

                Assert_HasContentType_ApplicationJson(response.Content);

                var expectedResponse = @"{""error"":""invalid_request"",""error_description"":""ERR-GEN-007: Invalid redirect_uri for client""}";

                await Assert_HasContent_Json(expectedResponse, response.Content);
            }
        }

        [Fact]
        public async Task ACX11_FAPI_WithResponseModeQuery_ShouldRespondWith_400BadRequest()
        {
            // Arrange

            // Act
            var response = await DataHolder_Par_API.SendRequest(scope: BaseTest.SCOPE, responseMode: "query");

            // Assert
            using (new AssertionScope())
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

                Assert_HasContentType_ApplicationJson(response.Content);

                var expectedResponse = @"{""error"":""invalid_request"",""error_description"":""ERR-GEN-013: response_mode is not supported""}";

                await Assert_HasContent_Json(expectedResponse, response.Content);
            }
        }

        
        [Fact(Skip = "https://dev.azure.com/CDR-AU/Participant%20Tooling/_workitems/edit/51303")]
        public async Task ACX12_FAPI_AuthorizeWithNoRequestOrRequestUri_ShouldRespondWith_400BadRequest()
        {
            // Arrange
            var url = $"{BaseTest.DH_TLS_AUTHSERVER_BASE_URL}/connect/authorize?client_id=3e6c5f3d-bd58-4aaa-8c23-acfec837b506&redirect_uri=https://www.certification.openid.net/test/a/cdr-mdh/callback&scope=openid%20profile%20common:customer.basic:read%20energy:accounts.basic:read%20energy:accounts.concessions:read%20cdr:registration&claims=%7B%22id_token%22:%7B%22acr%22:%7B%22value%22:%22urn:cds.au:cdr:2%22,%22essential%22:true%7D%7D,%22sharing_duration%22:7776000%7D&state=XhnoTrPAlD&nonce=IkUxjBqVuJ&response_type=code%20id_token";
            var _clientHandler = new HttpClientHandler();
            _clientHandler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            _clientHandler.AllowAutoRedirect = true;
            using var client = new HttpClient(_clientHandler);

            // Act
            var response = await client.GetAsync(url);

            // Assert
            using (new AssertionScope())
            {
                response.StatusCode.Should().Be(HttpStatusCode.OK);

                response?.Content?.Headers?.ContentType?.ToString().Should().StartWith("text/html");

                response?.RequestMessage?.RequestUri?.PathAndQuery.Should().StartWith("/connect/authorize");

                var expectedErrorResponse = "ERR-AUTH-008: request_uri is missing";
                var actualResponseContent = await response?.Content?.ReadAsStringAsync();
                actualResponseContent.Should().Contain(expectedErrorResponse);
            }
        }
    }
}