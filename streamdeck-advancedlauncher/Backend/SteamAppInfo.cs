using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedLauncher.Backend
{
    class SteamAppInfo
    {
        [JsonProperty(PropertyName = "steam_appid")]
        public string Id { get; private set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; private set; }

        [JsonProperty(PropertyName = "header_image")]
        public string ImageURL { get; private set; }
    }
}
