using AdvancedLauncher.Backend;
using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog.Fluent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedLauncher.Actions
{
    [PluginActionId("com.barraider.steamlauncher")]
    public class SteamLauncherAction : PluginBase
    {
        private class PluginSettings
        {

            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ApplicationId = String.Empty,
                    Applications = null,
                    ShowAppName = false
                };
                return instance;
            }

            [JsonProperty(PropertyName = "applicationId")]
            public string ApplicationId { get; set; }

            [JsonProperty(PropertyName = "applications")]
            public List<SteamInstalledApplication> Applications { get; set; }

            [JsonProperty(PropertyName = "showAppName")]
            public bool ShowAppName { get; set; }
        }

        #region Private Members

        private const string REGISTRY_STEAM_PATH = @"HKEY_CURRENT_USER\Software\Valve\Steam";
        private const string REGISTRY_STEAM_INSTALL_DIR_KEY = "SteamPath";
        private const string STEAM_APP_INFO_URI = "https://store.steampowered.com/api/appdetails/?appids={0}";
        private const string STEAM_APPS_DIR = "steamapps";
        private const string STEAM_LIBRARY_FILE = "libraryfolders.vdf";
        private const string STEAM_APPS_EXTENSION = "*.acf";
        private const string STEAM_LAUNCH_URL = @"steam://rungameid/{0}";

        private readonly PluginSettings settings;
        private TitleParameters titleParameters;
        private Bitmap appImage;
        private SteamAppInfo appInfo;

        #endregion
        public SteamLauncherAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            Connection.OnTitleParametersDidChange += Connection_OnTitleParametersDidChange;
            Connection.OnPropertyInspectorDidAppear += Connection_OnPropertyInspectorDidAppear;
            InitializeSettings();
            OnTick();
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
            Connection.OnTitleParametersDidChange -= Connection_OnTitleParametersDidChange;
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");
            if (String.IsNullOrEmpty(settings.ApplicationId))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, "Key pressed but application id is null!");
                return;
            }

            System.Diagnostics.Process.Start(String.Format(STEAM_LAUNCH_URL, settings.ApplicationId));
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            if (appImage != null)
            {
                await Connection.SetImageAsync(appImage);
            }

            if (appInfo != null && settings.ShowAppName)
            {
                await Connection.SetTitleAsync(Tools.SplitStringToFit(appInfo.Name, titleParameters));
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void InitializeSettings()
        {
            appInfo = null;
            if (!String.IsNullOrEmpty(settings.ApplicationId))
            {
                FetchAppInfo();
            }

            SaveSettings();
        }

        #endregion

        #region Private Methods

        private void Connection_OnPropertyInspectorDidAppear(object sender, SDEventReceivedEventArgs<BarRaider.SdTools.Events.PropertyInspectorDidAppear> e)
        {
            LoadInstalledApps();
            SaveSettings();
        }

        private void Connection_OnTitleParametersDidChange(object sender, SDEventReceivedEventArgs<BarRaider.SdTools.Events.TitleParametersDidChange> e)
        {
            titleParameters = e?.Event?.Payload?.TitleParameters;
        }


        private async void FetchAppInfo()
        {

            try
            {
                // Cleanup
                if (appImage != null)
                {
                    appImage.Dispose();
                    appImage = null;
                }

                if (!String.IsNullOrEmpty(settings.ApplicationId) && int.TryParse(settings.ApplicationId, out int appId))
                {
                    string appURL = String.Format(STEAM_APP_INFO_URI, settings.ApplicationId);
                    using HttpClient client = new HttpClient() { Timeout = new TimeSpan(0, 0, 10) };
                    HttpResponseMessage response = await client.GetAsync(appURL);
                    if (response.IsSuccessStatusCode)
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        JObject obj = JObject.Parse(body);
                        var data = obj.SelectToken($"{settings.ApplicationId}.data");
                        if (data != null)
                        {
                            appInfo = data.ToObject<SteamAppInfo>();
                            appImage = FetchImage(appInfo.ImageURL);
                        }
                    }
                    else
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"FetchAppInfo failed for app {settings.ApplicationId}! Response: {response.ReasonPhrase} Status Code: {response.StatusCode}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"FetchAppInfo Exception: {ex}");
            }
        }

        private List<String> LoadAdditionalLibraryFolders(string steamAppsFolder)
        {
            var  directories = new List<String>
            {
                steamAppsFolder
            };

            string libraryFile = Path.Combine(steamAppsFolder, STEAM_LIBRARY_FILE);
            if (!File.Exists(libraryFile))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"File not found {libraryFile}");
                return directories;
            }

            var foldersLines = File.ReadAllLines(libraryFile);
            foreach (var line in foldersLines)
            {
                string currLine = line.Replace('\t', ',').Replace("\"", "").Trim();
                string[] variables = currLine.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (variables.Length == 2 &&  Int32.TryParse(variables[0], out _) && Directory.Exists(variables[1]))
                {
                    directories.Add(Path.Combine(variables[1], STEAM_APPS_DIR));
                }
             }

            return directories;
        }

        private void LoadInstalledApps()
        {
            settings.Applications = new List<SteamInstalledApplication>();
            var steamDir = GetSteamInstallDir();
            var appsDir = Path.Combine(steamDir, STEAM_APPS_DIR);
            if (String.IsNullOrEmpty(steamDir) || !Directory.Exists(appsDir))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"LoadInstalledApps: Could not find Steam directory {appsDir}");
                return;
            }

            var directories = LoadAdditionalLibraryFolders(appsDir);
            foreach (var directory in directories)
            {
                if (!Directory.Exists(directory))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"LoadInstalledApps: Missing Steam directory {directory}");
                    continue;
                }
                foreach (var filename in Directory.EnumerateFiles(directory, STEAM_APPS_EXTENSION))
                {
                    try
                    {
                        string appId = String.Empty;
                        string appName = String.Empty;
                        var gameInfo = File.ReadAllLines(filename);
                        foreach (var line in gameInfo)
                        {
                            string currLine = line.Replace('\t', ',').Replace("\"", "").Trim();
                            string[] variables = currLine.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (variables[0] == "appid")
                            {
                                appId = variables[1];
                            }
                            else if (variables[0] == "name")
                            {
                                appName = variables[1];
                            }

                            if (!String.IsNullOrEmpty(appId) && !String.IsNullOrEmpty(appName))
                            {
                                break;
                            }
                        }

                        if (String.IsNullOrEmpty(appId) || String.IsNullOrEmpty(appName))
                        {
                            Logger.Instance.LogMessage(TracingLevel.WARN, $"LoadInstalledApps: Could not find valid Game info in {filename}");
                            continue;
                        }
                        settings.Applications.Add(new SteamInstalledApplication(appId, appName));
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"LoadInstalledApps: Failed to iterate on {filename}: {ex}");
                        return;
                    }
                }
            }
            settings.Applications = settings.Applications.OrderBy(a => a.Name).ToList();
        }

        private String GetSteamInstallDir()
        {
            return Registry.GetValue(REGISTRY_STEAM_PATH, REGISTRY_STEAM_INSTALL_DIR_KEY, @"c:/program files (x86)/steam").ToString();
        }

        private Bitmap FetchImage(string imageUrl)
        {
            try
            {
                if (String.IsNullOrEmpty(imageUrl))
                {
                    return null;
                }

                using WebClient client = new WebClient();
                using Stream stream = client.OpenRead(imageUrl);
                Bitmap image = new Bitmap(stream);
                return image;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Failed to fetch image: {imageUrl} {ex}");
            }
            return null;
        }

        #endregion
    }
}