using Newtonsoft.Json;

namespace Codex.Web.Monaco.Models
{
    public sealed class ToolTipModel
    {
        [JsonProperty(PropertyName = "projectId")]
        public string projectId { get; set; }

        [JsonProperty(PropertyName = "fullName")]
        public string fullName { get; set; }

        [JsonProperty(PropertyName = "comment")]
        public string comment { get; set; }

        [JsonProperty(PropertyName = "symbolKind")]
        public string symbolKind { get; set; }

        [JsonProperty(PropertyName = "typeName")]
        public string typeName { get; set; }
    }
}