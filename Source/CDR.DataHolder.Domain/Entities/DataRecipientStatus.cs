using Newtonsoft.Json;

namespace CDR.DataHolder.Domain.Entities
{
    public class DataRecipientStatus
    {
        [JsonProperty(PropertyName = "legalEntityId")]
        public string ID { get; set; }

        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }
    }
}
