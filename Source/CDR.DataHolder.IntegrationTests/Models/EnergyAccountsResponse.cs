using Newtonsoft.Json;

#nullable enable

namespace CDR.DataHolder.IntegrationTests.Models.EnergyAccountsResponse
{
    public class Response
    {
        [JsonProperty("data")]
        public Data? Data { get; set; }

        [JsonProperty("links")]
        public Links? Links { get; set; }

        [JsonProperty("meta")]
        public Meta? Meta { get; set; }
    }

    public class Data
    {
        [JsonProperty("accounts")]
        public Account[]? Accounts { get; set; }
    }

    public class Account
    {
        [JsonProperty("accountId")]
        public string? AccountId { get; set; }

        [JsonProperty("accountNumber")]
        public string? AccountNumber { get; set; }

        [JsonProperty("displayName")]
        public string? DisplayName { get; set; }

        [JsonProperty("creationDate")]
        public string? CreationDate { get; set; }

        [JsonProperty("plans")]
        public Plan[]? Plans { get; set; }
    }

    public class Plan
    {
        [JsonProperty("nickname")]
        public string? Nickname { get; set; }

        [JsonProperty("servicePointIds")]
        public string[]? ServicePointIds { get; set; }

        [JsonProperty("planOverview")]
        public PlanOverview? PlanOverview { get; set; }
    }

    public class PlanOverview
    {
        [JsonProperty("displayName")]
        public string? DisplayName { get; set; }

        [JsonProperty("startDate")]
        public string? StartDate { get; set; }

        [JsonProperty("endDate")]
        public string? EndDate { get; set; }
    }

    public class Links
    {
        [JsonProperty("first")]
        public string? First { get; set; }

        [JsonProperty("last")]
        public string? Last { get; set; }

        [JsonProperty("next")]
        public string? Next { get; set; }

        [JsonProperty("prev")]
        public string? Prev { get; set; }

        [JsonProperty("self")]
        public string? Self { get; set; }
    }

    public class Meta
    {
        [JsonProperty("totalRecords")]
        public long? TotalRecords { get; set; }

        [JsonProperty("totalPages")]
        public long? TotalPages { get; set; }
    }
}
