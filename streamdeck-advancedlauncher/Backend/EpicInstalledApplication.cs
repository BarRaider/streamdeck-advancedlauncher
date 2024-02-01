using Newtonsoft.Json;

namespace AdvancedLauncher.Backend
{
    class EpicInstalledApplication
    {
        [JsonProperty(PropertyName = "namespace")]
        public string Namespace { get; private set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; private set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; private set; }
        
        [JsonProperty(PropertyName = "displayName")]
        public string DisplayName { get; private set; }
        
        [JsonProperty(PropertyName = "imageUrls")]
        public string[] ImageUrls { get; private set; }

        public EpicInstalledApplication(string appNamespace, string appId, string appName, string appDisplayName, string[] imageUrls)
        {
            Namespace = appNamespace;
            Id = appId;
            Name = appName;
            DisplayName = appDisplayName;
            ImageUrls = imageUrls;
        }
    }
}
