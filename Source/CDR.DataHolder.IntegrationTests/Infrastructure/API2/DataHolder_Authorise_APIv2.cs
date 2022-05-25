using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;

#nullable enable

namespace CDR.DataHolder.IntegrationTests.Infrastructure.API2
{
    public class EDataHolder_Authorise_IncorrectCustomerId : Exception { }
    public class EDataHolder_Authorise_IncorrectOneTimePassword : Exception { }

    public class DataHolder_Authorise_APIv2
    {
        /// <summary>
        /// The customer's userid with the DataHolder - eg "jwilson"
        /// </summary>
        public string? UserId { get; init; }

        /// <summary>
        /// The OTP (One-time password) that is sent to the customer (via sms etc) so the DataHolder can authenticate the Customer.
        /// For the mock solution use "000789"
        /// </summary>
        public string? OTP { get; init; }

        /// <summary>
        /// Comma delimited list of account ids the user is granting consent for
        /// </summary>
        public string? SelectedAccountIds { get; init; }

        private string[]? SelectedAccountIdsArray => SelectedAccountIds?.Split(",");

        /// <summary>
        /// Scope
        /// </summary>
        public string Scope { get; init; } = BaseTest.SCOPE;

        /// <summary>
        /// Lifetime (in seconds) of the access token
        /// </summary>
        public int TokenLifetime { get; init; } = 3600;

        /// <summary>
        /// Lifetime (in seconds) of the CDR arrangement.
        /// 7776000 = 90 days
        /// </summary>
        public int SharingDuration { get; init; } = 7776000;

        public string? RequestUri { get; init; }

        public string CertificateFilename { get; init; } = BaseTest.CERTIFICATE_FILENAME;
        public string CertificatePassword { get; init; } = BaseTest.CERTIFICATE_PASSWORD;


        public string ClientId { get; init; } = BaseTest.SOFTWAREPRODUCT_ID.ToLower();
        // public string RedirectURI { get; init; } = BaseTest.SOFTWAREPRODUCT_REDIRECT_URI_FOR_INTEGRATION_TESTS;
        public string RedirectURI { get; init; } = BaseTest.SubstituteConstant(BaseTest.SOFTWAREPRODUCT_REDIRECT_URI_FOR_INTEGRATION_TESTS);
        public string JwtCertificateFilename { get; init; } = BaseTest.JWT_CERTIFICATE_FILENAME;
        public string JwtCertificatePassword { get; init; } = BaseTest.JWT_CERTIFICATE_PASSWORD;
        public string CodeVerifier { get; set; }

        /// <summary>
        /// Perform authorisation and consent flow. Returns authCode and idToken
        /// </summary>
        public async Task<(string authCode, string idToken)> AuthoriseWithRequestObject()
        {
            // Create cookie container since we need to share cookies across multiple requests
            var cookieContainer = new CookieContainer();

            // Call authorise endpoint, it will redirect to DataHolder login endpoint
            var authResponse = await IdentityServer_AuthoriseWithRequestObject(cookieContainer);

            // Set userid and postback
            var userIdResponse = await DataHolder_Login_UserId(cookieContainer, authResponse);

            // Set password and postback, it will validate user then redirect to IdentityServer which will redirect to DataHolder consent endpoint
            var passwordResponse = await DataHolder_Login_Password(cookieContainer, userIdResponse);

            // Select accounts to share and postback
            var selectAccountsResponse = await DataHolder_Consent_SelectAccountsToShare(cookieContainer, passwordResponse);

            (var authCode, var idToken) = await DataHolder_Consent_Confirm(cookieContainer, selectAccountsResponse);

            return (authCode, idToken);
        }

        /// <summary>
        /// Perform authorisation and consent flow. Returns authCode and idToken
        /// </summary>
        public async Task<(string authCode, string idToken, string codeVerifier)> Authorise()
        {
            if (this.CodeVerifier == null)
            {
                this.CodeVerifier = string.Concat(System.Guid.NewGuid().ToString(), '-', System.Guid.NewGuid().ToString());
            }

            // Create cookie container since we need to share cookies across multiple requests
            var cookieContainer = new CookieContainer();

            // Call authorise endpoint, it will redirect to DataHolder login endpoint
            var authResponse = await IdentityServer_Authorise(cookieContainer);

            // Set userid and postback
            var userIdResponse = await DataHolder_Login_UserId(cookieContainer, authResponse);

            // Set password and postback, it will validate user then redirect to IdentityServer which will redirect to DataHolder consent endpoint
            var passwordResponse = await DataHolder_Login_Password(cookieContainer, userIdResponse);

            // Select accounts to share and postback
            var selectAccountsResponse = await DataHolder_Consent_SelectAccountsToShare(cookieContainer, passwordResponse);

            (var authCode, var idToken) = await DataHolder_Consent_Confirm(cookieContainer, selectAccountsResponse);

            return (authCode, idToken, this.CodeVerifier);
        }

        // Create http client with cookie container
        private HttpClient CreateHttpClient(CookieContainer cookieContainer, bool allowAutoRedirect = true)
        {
            var httpClientHandler = new HttpClientHandler
            {
                UseDefaultCredentials = true,
                AllowAutoRedirect = allowAutoRedirect,
                UseCookies = true,
                CookieContainer = cookieContainer,
            };

            httpClientHandler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            httpClientHandler.ClientCertificates.Add(new X509Certificate2(CertificateFilename, CertificatePassword, X509KeyStorageFlags.Exportable));
            var httpClient = new HttpClient(httpClientHandler);

            return httpClient;
        }

        private async Task<HttpResponseMessage?> IdentityServer_AuthoriseWithRequestObject(CookieContainer cookieContainer)
        {
            var URL = new AuthoriseURLBuilder
            {
                Scope = Scope,
                TokenLifetime = TokenLifetime,
                SharingDuration = SharingDuration,
                RequestUri = RequestUri,
                ClientId = ClientId,
                RedirectURI = RedirectURI,
                JWT_CertificateFilename = JwtCertificateFilename,
                JWT_CertificatePassword = JwtCertificatePassword,
            }.URL;

            var request = new HttpRequestMessage(HttpMethod.Get, URL);

            // Start DataRecipientConsentCallback in case request fails (since dataholder will send a redirect with error to the callback uri)
            var callback = new DataRecipientConsentCallback(redirectUrl: RedirectURI);
            callback.Start();
            try
            {
                var response = await CreateHttpClient(cookieContainer).SendAsync(request);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"{nameof(DataHolder_Authorise_APIv2)}.{nameof(IdentityServer_AuthoriseWithRequestObject)} - Response.StatusCode={response.StatusCode}, Response.Content={await response.Content.ReadAsStringAsync()}, request.RequestUri={request.RequestUri}");
                }

                var responseFragment = response?.RequestMessage?.RequestUri?.Fragment;
                if (responseFragment?.StartsWith("#error=invalid_request") ?? false)
                {
                    throw new NotSupportedException($"{nameof(DataHolder_Authorise_APIv2)}.{nameof(IdentityServer_AuthoriseWithRequestObject)} - Request failed with request object. RequestUri={URL}, RequestUri Scope={Scope}, RedirectUri={response?.RequestMessage?.RequestUri}");
                }

                return response;
            }
            finally
            {
                await callback.Stop();
            }
        }

        private async Task<HttpResponseMessage?> IdentityServer_Authorise(CookieContainer cookieContainer)
        {
            var requestUri = this.RequestUri;

            if (string.IsNullOrEmpty(requestUri))
            {
                // Send the PAR request.
                var response = await DataHolder_Par_API.SendRequest(
                    clientId: this.ClientId,
                    scope: this.Scope,
                    sharingDuration: this.SharingDuration,
                    redirectUri: this.RedirectURI,
                    codeVerifier: this.CodeVerifier,
                    certificateFilename: this.CertificateFilename,
                    certificatePassword: this.CertificatePassword,
                    jwtCertificateForClientAssertionFilename: this.JwtCertificateFilename,
                    jwtCertificateForClientAssertionPassword: this.JwtCertificatePassword,
                    jwtCertificateForRequestObjectFilename: this.JwtCertificateFilename,
                    jwtCertificateForRequestObjectPassword: this.JwtCertificatePassword);

                if (response.StatusCode != HttpStatusCode.Created) throw new Exception("Error with PAR request - StatusCode");

                var parResponse = await DataHolder_Par_API.DeserializeResponse(response);
                if (string.IsNullOrEmpty(parResponse?.RequestURI)) throw new Exception("Error with PAR request - RequestURI");

                requestUri = parResponse?.RequestURI;
            }

            var URL = new AuthoriseURLBuilder
            {
                Scope = Scope,
                TokenLifetime = TokenLifetime,
                SharingDuration = SharingDuration,
                RequestUri = requestUri,
                ClientId = ClientId,
                RedirectURI = RedirectURI,
                JWT_CertificateFilename = JwtCertificateFilename,
                JWT_CertificatePassword = JwtCertificatePassword,
            }.URL;

            var request = new HttpRequestMessage(HttpMethod.Get, URL);

            // Start DataRecipientConsentCallback in case request fails (since dataholder will send a redirect with error to the callback uri)
            var callback = new DataRecipientConsentCallback(redirectUrl: RedirectURI);
            callback.Start();
            try
            {
                var response = await CreateHttpClient(cookieContainer).SendAsync(request);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"{nameof(DataHolder_Authorise_APIv2)}.{nameof(IdentityServer_Authorise)} - Response.StatusCode={response.StatusCode}, Response.Content={await response.Content.ReadAsStringAsync()}, request.RequestUri={request.RequestUri}");
                }

                var responseFragment = response?.RequestMessage?.RequestUri?.Fragment;
                if (responseFragment?.StartsWith("#error=invalid_request") ?? false)
                {
                    throw new NotSupportedException($"{nameof(DataHolder_Authorise_APIv2)}.{nameof(IdentityServer_Authorise)} - Request failed with request object. RequestUri={URL}, RequestUri Scope={Scope}, RedirectUri={response?.RequestMessage?.RequestUri}");
                }

                return response;
            }
            finally
            {
                await callback.Stop();
            }
        }

        // Handle redirect to DataHolder login endpoint, set userid (customer id) and postback 
        private async Task<HttpResponseMessage?> DataHolder_Login_UserId(CookieContainer cookieContainer, HttpResponseMessage? authResponse)
        {
            if (authResponse == null || authResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"{nameof(DataHolder_Authorise_APIv2)}.{nameof(DataHolder_Login_UserId)} - {nameof(authResponse)} not 200OK");
            }

            // Load html
            var html = await authResponse.Content.ReadAsStringAsync();
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Parse the form 
            var formFields = HtmlParser.ParseForm(html, "//form");
            formFields["CustomerId"] = UserId;
            formFields["button"] = "page2";

            // Postback
            var requestUri = authResponse?.RequestMessage?.RequestUri?.AbsoluteUri;
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = HtmlParser.FormUrlEncodedContent(formFields),
            };
            var response = await CreateHttpClient(cookieContainer).SendAsync(request);
            return response;
        }

        // Set password and postback, DataHolder will validate user and redirect to IdentityServer which will then redirect to DataHolder consent endpoint
        private async Task<HttpResponseMessage?> DataHolder_Login_Password(CookieContainer cookieContainer, HttpResponseMessage? userIdResponse) //, IEnumerable<string> cookies)
        {
            if (userIdResponse == null || userIdResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"{nameof(DataHolder_Authorise_APIv2)}.{nameof(DataHolder_Login_Password)} - {nameof(userIdResponse)} not 200OK");
            }

            // Load html
            var html = await userIdResponse.Content.ReadAsStringAsync();

            // Check that customer id was valid
            if (html.Contains("Incorrect customer ID", StringComparison.InvariantCultureIgnoreCase))
            {
                throw new EDataHolder_Authorise_IncorrectCustomerId();
            }

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Parse the form
            var formFields = HtmlParser.ParseForm(html, "//form");
            formFields["Otp"] = OTP;
            formFields["button"] = "auth";

            // Postback
            var requestUri = userIdResponse?.RequestMessage?.RequestUri?.AbsoluteUri;
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = HtmlParser.FormUrlEncodedContent(formFields)
            };
            var response = await CreateHttpClient(cookieContainer).SendAsync(request);
            return response;
        }

        // Select user bank accounts to share and postback 
        private async Task<HttpResponseMessage?> DataHolder_Consent_SelectAccountsToShare(CookieContainer cookieContainer, HttpResponseMessage? passwordResponse) //, IEnumerable<string> cookies)
        {
            if (passwordResponse == null || passwordResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"{nameof(DataHolder_Authorise_APIv2)}.{nameof(DataHolder_Consent_SelectAccountsToShare)} - {nameof(passwordResponse)} not 200OK");
            }

            // Load html
            var html = await passwordResponse.Content.ReadAsStringAsync();

            // Check that password was valid
            if (html.Contains("Incorrect one time password", StringComparison.InvariantCultureIgnoreCase))
            {
                throw new EDataHolder_Authorise_IncorrectOneTimePassword();
            }

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Parse the form
            var formFields = HtmlParser.ParseForm(html, "//form");

            // Set selected accounts
            if (SelectedAccountIdsArray != null)
            {
                int i = 0;
                foreach (string selectedAccountId in SelectedAccountIdsArray)
                {
                    formFields[$"SelectedAccountIds[{i++}]"] = selectedAccountId;
                }
            }
            formFields["button"] = "page2";

            // Postback
            string? requestUri = passwordResponse?.RequestMessage?.RequestUri?.AbsoluteUri;
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = HtmlParser.FormUrlEncodedContent(formFields)
            };
            var response = await CreateHttpClient(cookieContainer).SendAsync(request);
            return response;
        }

        // Confirm selection of accounts and postback 
        private async Task<(string authCode, string idToken)> DataHolder_Consent_Confirm(CookieContainer cookieContainer, HttpResponseMessage? selectAccountsResponse)
        {
            async Task<(string authCode, string idToken)> Postback(HttpRequestMessage request)
            {
                // Upon postback of consent, IdentityServer will redirect to the Data Recipient's redirect url with the authcode etc
                // We need to start a webhost (DataRecipientConsentCallback) to catch the callback
                var callback = new DataRecipientConsentCallback(redirectUrl: RedirectURI);
                callback.Start();
                try
                {
                    // The redirect will happen once we post the callback
                    var response = await CreateHttpClient(cookieContainer).SendAsync(request);

                    var fragment = response.RequestMessage?.RequestUri?.Fragment;
                    if (fragment == null)
                    {
                        throw new Exception($"{nameof(DataHolder_Consent_Confirm)}.{nameof(Postback)} - failed");
                    }

                    var query = HttpUtility.ParseQueryString(fragment.TrimStart('#'));

                    Exception RaiseException(string errorMessage)
                    {
                        var authCode = query?["code"];
                        var idToken = query?["id_token"];
                        var responseRequestUri = response?.RequestMessage?.RequestUri;

                        return new Exception($"{errorMessage}\r\nauthCode={authCode},idToken={idToken},response.RequestMessage.RequestUri={responseRequestUri}");
                    }

                    var authCode = query["code"];
                    if (authCode == null)
                    {
                        throw RaiseException("authCode is null");
                    }

                    var idToken = query["id_token"];
                    if (idToken == null)
                    {
                        throw RaiseException("idToken is null");
                    }

                    var state = query["state"];
                    var nonce = query["nonce"];
                    var scope = query["scope"];

                    return (authCode, idToken);
                }
                finally
                {
                    await callback.Stop();
                }
            }

            if (selectAccountsResponse == null || selectAccountsResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"{nameof(DataHolder_Authorise_APIv2)}.{nameof(DataHolder_Consent_Confirm)} - {nameof(selectAccountsResponse)} not 200OK");
            }

            // Load html
            var html = await selectAccountsResponse.Content.ReadAsStringAsync();
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Parse the form
            var formFields = HtmlParser.ParseForm(html, "//form");
            formFields.Remove("SelectedAccountIds");
            for (int i = 0; i < 99; i++)
            {
                formFields.Remove($"SelectedAccountIds[{i}]");
            }
            if (SelectedAccountIdsArray != null)
            {
                int i = 0;
                foreach (string selectedAccountId in SelectedAccountIdsArray)
                {
                    formFields[$"SelectedAccountIds[{i++}]"] = selectedAccountId;
                }
            }
            formFields["button"] = "consent";

            // Postback
            string? requestUri = selectAccountsResponse?.RequestMessage?.RequestUri?.AbsoluteUri;
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = HtmlParser.FormUrlEncodedContent(formFields)
            };
            (var authCode, var idToken) = await Postback(request);

            return (authCode, idToken);
        }
    }
}
