using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedLauncher.Backend
{
    class SteamInstalledApplication
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; private set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; private set; }

        public SteamInstalledApplication(string appId, string appName)
        {
            Id = appId;
            Name = appName;
        }
    }
}
