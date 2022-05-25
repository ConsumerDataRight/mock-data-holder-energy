using System.Collections.Generic;
using IdentityModel;
using IdentityServer4.Models;

namespace CDR.DataHolder.IdentityServer.Configuration
{
    public static class InMemoryConfig
    {
        public static IEnumerable<ApiResource> Apis =>
            new List<ApiResource>
            {
                new ApiResource("cds-au", "Mock Data Holder (MDH) Resource API")
                {
                    // Need to add all the scopes supported by cds-au api.
                    // All the scopes supported by the cds-au API.
					Scopes = new string[] {
                        API.Infrastructure.Constants.CdrScopes.Registration,
                        API.Infrastructure.Constants.ApiScopes.Energy.AccountsBasicRead,
                        API.Infrastructure.Constants.ApiScopes.Energy.ConcessionsRead,
                        API.Infrastructure.Constants.ApiScopes.Common.CustomerBasicRead,
                        
                    }
                }
            };

        public static IEnumerable<ApiScope> ApiScopes =>
            new[]
            {
                // These are the supported scopes for this data holder implementation.
                new ApiScope(API.Infrastructure.Constants.CdrScopes.Registration, "Dynamic Client Registration (DCR)"),
                new ApiScope(API.Infrastructure.Constants.ApiScopes.Energy.AccountsBasicRead, "Basic read access to energy accounts"),
                new ApiScope(API.Infrastructure.Constants.ApiScopes.Energy.ConcessionsRead, "The scope would allow the third party to access the details of any concessions for a customer’s energy account."),
                new ApiScope(API.Infrastructure.Constants.ApiScopes.Common.CustomerBasicRead, "Basic read access to customer information"),

                // These are the additional scopes for CDR.  These need to be here to allow a DR with more scopes than supported to authorise.
                new ApiScope(API.Infrastructure.Constants.CdrScopes.MetricsBasicRead, "Metrics data accessible ONLY to the CDR Register"),
                new ApiScope(API.Infrastructure.Constants.CdrScopes.MetadataUpdate, "Update notification accessible ONLY to the CDR Register"),
            };

        public static IEnumerable<IdentityResource> IdentityResources =>
            new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
            };
    }
}