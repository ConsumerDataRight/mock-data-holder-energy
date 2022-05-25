using Newtonsoft.Json;

namespace CDR.DataHolder.Domain.Entities
{
    public class SoftwareProductStatus
    {
        [JsonProperty(PropertyName = "softwareProductId")]
        public string ID { get; set; }

        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }
    }
}
