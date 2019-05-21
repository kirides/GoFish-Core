using System.Text.Json.Serialization;

namespace GoFishCore.WpfUI
{
    internal class ConfigJson
    {
        [JsonPropertyName("last_directory")]
        public string LastDirectory { get; set; }
        [JsonPropertyName("last_search")]
        public string LastSearch { get; set; }
        [JsonPropertyName("case_sensitive")]
        public bool CaseSensitive { get; set; }
    }
}
