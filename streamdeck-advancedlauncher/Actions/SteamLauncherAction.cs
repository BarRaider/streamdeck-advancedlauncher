using AdvancedLauncher.Backend;
using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using Gameloop.Vdf.Linq;
using Gameloop.Vdf;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog.Fluent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedLauncher.Actions
{
    [PluginActionId("com.barraider.steamlauncher")]
    public class SteamLauncherAction : KeypadBase
    {

        public enum ImageFit
        {
            Fit = 0,
            Center = 1,
            CropLeft = 2,
            CropRight = 3

        }
        private class PluginSettings
        {

            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ApplicationId = String.Empty,
                    Applications = null,
                    ShowAppName = false,
                    ImageFit = ImageFit.Fit
                };
                return instance;
            }

            [JsonProperty(PropertyName = "applicationId")]
            public string ApplicationId { get; set; }

            [JsonProperty(PropertyName = "applications")]
            public List<SteamInstalledApplication> Applications { get; set; }

            [JsonProperty(PropertyName = "showAppName")]
            public bool ShowAppName { get; set; }

            [JsonProperty(PropertyName = "imageFit")]
            public ImageFit ImageFit { get; set; }
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

            if (appInfo != null && titleParameters != null && settings.ShowAppName)
            {
                await Connection.SetTitleAsync(appInfo.Name?.SplitToFitKey(titleParameters));
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
                            //appImage = FetchImage(appInfo.ImageURL);
                            using Bitmap img = FetchImage(appInfo.ImageURL);
                            appImage = SetImageFit(img);
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
            var directories = new List<String>
            {
                steamAppsFolder
            };

            string libraryFile = Path.Combine(steamAppsFolder, STEAM_LIBRARY_FILE);
            if (!File.Exists(libraryFile))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"LoadAdditionalLibraryFolders: File not found {libraryFile}");
                return directories;
            }

            ParseVDFLibraries(libraryFile, ref directories);
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} LoadAdditionalLibraryFolders found {directories.Count - 1} potential additional library folders");
            return directories;
        }

        private void ParseVDFLibraries(string libraryFile, ref List<string> directories)
        {
            try
            {
                VProperty library = VdfConvert.Deserialize(File.ReadAllText(libraryFile));
                foreach (var child in library?.Value?.Children())
                {
                    VProperty prop = child as VProperty;
                    if (prop == null)
                    {
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} ParseVDFLibraries failed to convert entity to VProperty: {child}");
                        continue;
                    }

                    // Folders have a numeric value
                    if (!Int32.TryParse(prop.Key, out _))
                    {
                        continue;
                    }

                    string path = string.Empty;
                    if (prop.Value.Type == VTokenType.Value)
                    {
                        path = prop.Value?.ToString();
                        if (String.IsNullOrEmpty(path) || !Directory.Exists(path))
                        {
                            Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} ParseVDFLibraries (Old Format) failed to locate path: {prop}");
                            continue;
                        }
                    }
                    else if (prop.Value.Type == VTokenType.Object)
                    {

                        path = prop.Value?["path"]?.ToString();
                        if (string.IsNullOrEmpty(path))
                        {
                            Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} ParseVDFLibraries failed to locate path: {prop}");
                            continue;
                        }

                        string mounted = prop.Value?["mounted"]?.ToString() ?? "1";
                        if (mounted != "1")
                        {
                            Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} ParseVDFLibraries skipping unmounted folder: {path}");
                            continue;
                        }
                    }
                    else
                    {
                        Logger.Instance.LogMessage(TracingLevel.WARN, $"{this.GetType()} ParseVDFLibraries invalid property type: {prop.Value.Type} for {prop}");
                        continue;
                    }
                    
                    directories.Add(Path.Combine(path, STEAM_APPS_DIR));
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} ParseVDFLibraries Exception: {ex}");
            }
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
            var apps = settings.Applications.OrderBy(a => a.Name).ToList();
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Found {apps.Count} apps in {directories.Count} dirs");
            settings.Applications = apps;
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

        private Bitmap SetImageFit(Bitmap img)
        {
            if (img == null)
            {
                return null;
            }

            Image tmpImage;
            var newImage = Tools.GenerateGenericKeyImage(out Graphics graphics);
            if (img.Width > img.Height)
            {
                tmpImage = (Bitmap)ResizeImageByHeight(img, newImage.Height);
            }
            else
            {
                tmpImage = (Bitmap)ResizeImageByWidth(img, newImage.Width);
            }

            
            int startX;
            switch (settings.ImageFit)
            {
                case ImageFit.CropLeft:
                    graphics.DrawImage(tmpImage, new Rectangle(0, 0, newImage.Width, newImage.Height), new Rectangle(0, 0, newImage.Height, newImage.Width), GraphicsUnit.Pixel);
                    break;
                case ImageFit.CropRight:
                    startX = tmpImage.Width - newImage.Width;
                    if (startX < 0)
                    {
                        startX = 0;
                    }
                    graphics.DrawImage(tmpImage, new Rectangle(0, 0, newImage.Width, newImage.Height), new Rectangle(startX, 0, newImage.Width, newImage.Height), GraphicsUnit.Pixel);
                    break;
                case ImageFit.Center:
                    startX = (tmpImage.Width /2) - (newImage.Width / 2);
                    if (startX < 0)
                    {
                        startX = 0;
                    }
                    graphics.DrawImage(tmpImage, new Rectangle(0, 0, newImage.Width, newImage.Height), new Rectangle(startX, 0, newImage.Width, newImage.Height), GraphicsUnit.Pixel);
                    break;
                case ImageFit.Fit:
                    graphics.DrawImage(img, new Rectangle(0,0, newImage.Width, newImage.Height));
                    break;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetImageFit unsupported ImageFit {settings.ImageFit}");
                    return null;
            }
            tmpImage.Dispose();
            return newImage;
        }

       private Image ResizeImageByHeight(Image img, int newHeight)
        {
            if (img == null)
            {
                return null;
            }

            int originalWidth = img.Width;
            int originalHeight = img.Height;

            // Figure out the ratio
            double ratio = (double)newHeight / (double)originalHeight;
            int newWidth = (int) (originalWidth *  ratio);
            return ResizeImage(img, newHeight, newWidth);
        }

        private Image ResizeImageByWidth(Image img, int newWidth)
        {
            if (img == null)
            {
                return null;
            }

            int originalWidth = img.Width;
            int originalHeight = img.Height;

            // Figure out the ratio
            double ratio = (double)newWidth / (double)originalWidth;
            int newHeight = (int)(originalHeight * ratio);
            return ResizeImage(img, newHeight, newWidth);
        }

        private Image ResizeImage(Image original, int newHeight, int newWidth)
        {
            Image canvas = new Bitmap(newWidth, newHeight);
            Graphics graphic = Graphics.FromImage(canvas);

            graphic.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphic.SmoothingMode = SmoothingMode.HighQuality;
            graphic.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphic.CompositingQuality = CompositingQuality.HighQuality;

            graphic.Clear(Color.Black); // Padding
            graphic.DrawImage(original, 0, 0, newWidth, newHeight);

            return canvas;
        }

    }

    #endregion
}