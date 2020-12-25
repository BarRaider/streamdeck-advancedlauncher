using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedLauncher.Backend
{
    class UWPPackageInfo
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; private set; }

        [JsonProperty(PropertyName = "path")]
        public string Path { get; private set; }

        [JsonProperty(PropertyName = "logo")]
        public Uri Logo { get; private set; }

        public UWPPackageInfo(string name, string path, Uri logo)
        {
            Name = name;
            Path = path;
            Logo = logo;
        }
    }
}
