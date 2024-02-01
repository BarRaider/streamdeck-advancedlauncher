using AdvancedLauncher.Backend;
using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedLauncher.Actions
{
    [PluginActionId("com.barraider.epiclauncher")]
    public sealed class EpicLauncherAction : KeypadBase
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
                    ApplicationNamespace = String.Empty,
                    ApplicationId = String.Empty,
                    Applications = null,
                    ShowAppName = false,
                    ImageFit = ImageFit.Fit
                };
                return instance;
            }

            [JsonProperty(PropertyName = "applicationNamespace")]
            public string ApplicationNamespace { get; set; }
            
            [JsonProperty(PropertyName = "applicationId")]
            public string ApplicationId { get; set; }
            
            [JsonProperty(PropertyName = "applicationName")]
            public string ApplicationName { get; set; }
            
            [JsonProperty(PropertyName = "applicationDisplayName")]
            public string ApplicationDisplayName { get; set; }

            [JsonProperty(PropertyName = "applications")]
            public List<EpicInstalledApplication> Applications { get; set; }

            [JsonProperty(PropertyName = "showAppName")]
            public bool ShowAppName { get; set; }

            [JsonProperty(PropertyName = "imageFit")]
            public ImageFit ImageFit { get; set; }
        }

        #region Private Members

        private readonly string _epicManifestsDir;
        private readonly string _epicCatalogFile;
        private const string EpicLaunchUrl = @"com.epicgames.launcher://apps/{0}:{1}:{2}?action=launch&silent=true";

        private readonly PluginSettings _settings;
        private TitleParameters _titleParameters;
        private Bitmap _appImage;
        private SteamAppInfo _appInfo;

        #endregion
        public EpicLauncherAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            var applicationDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            _epicManifestsDir = $@"{applicationDataFolder}\Epic\EpicGamesLauncher\Data\Manifests";
            _epicCatalogFile = $@"{applicationDataFolder}\Epic\EpicGamesLauncher\Data\Catalog\catcache.bin";
            
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                _settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                _settings = payload.Settings.ToObject<PluginSettings>();
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
            if (string.IsNullOrEmpty(_settings.ApplicationId))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, "Key pressed but application id is null!");
                return;
            }

            var launchUrl = string.Format(EpicLaunchUrl, _settings.ApplicationNamespace, _settings.ApplicationId, _settings.ApplicationName);
            System.Diagnostics.Process.Start(launchUrl);
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            if (_appImage != null)
            {
                await Connection.SetImageAsync(_appImage);
            }

            if (_appInfo != null && _titleParameters != null && _settings.ShowAppName)
            {
                await Connection.SetTitleAsync(_appInfo.Name?.SplitToFitKey(_titleParameters));
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(_settings, payload.Settings);
            InitializeSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(_settings));
        }

        private void InitializeSettings()
        {
            _appInfo = null;
            if (!String.IsNullOrEmpty(_settings.ApplicationId))
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
            _titleParameters = e?.Event?.Payload?.TitleParameters;
        }

        private async void FetchAppInfo()
        {
            
            try
            {
                // Cleanup
                if (_appImage != null)
                {
                    _appImage.Dispose();
                    _appImage = null;
                }

                var installedApp = _settings.Applications.FirstOrDefault(app => app.Id == _settings.ApplicationId);
                if (installedApp == default) return;

                _settings.ApplicationNamespace = installedApp.Namespace;
                _settings.ApplicationName = installedApp.Name;
                _settings.ApplicationDisplayName = installedApp.DisplayName;
                
                if (installedApp.ImageUrls.Length == 0) return;
                
                using Bitmap img = FetchImage(installedApp.ImageUrls.First());
                _appImage = SetImageFit(img);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"FetchAppInfo Exception: {ex}");
            }
        }

        private void LoadInstalledApps()
        {
            
            _settings.Applications = new List<EpicInstalledApplication>();
            if (!Directory.Exists(_epicManifestsDir))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"LoadInstalledApps: Could not find Epic directory {_epicManifestsDir}");
                return;
           }

            var gameImageUrls = new Dictionary<string, string[]>();
            try
            {
                var base64decodedBytes = Convert.FromBase64String(File.ReadAllText(_epicCatalogFile).Trim());
                var catalogContents = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(base64decodedBytes));
                var jsonArray = (JArray)catalogContents;
                var catalogGamesJson = jsonArray
                    .Select(obj => (JObject)obj)
                    // Filter only for games
                    .Where(obj => ((JArray)obj["categories"]).Any(obj => ((JObject)obj)["path"].ToString() == "games"))
                    .ToArray();
                foreach (var gameJson in catalogGamesJson)
                {
                    if (gameJson["keyImages"] == null || gameJson["id"] == null) continue;

                    gameImageUrls.Add(
                        gameJson["id"].ToString(),
                        gameJson["keyImages"]
                            .Select(keyImage => ((JObject)keyImage)["url"].ToString())
                            .ToArray()
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"LoadingInstalledApps: Failed loading image urls! from catalog!: {ex}");
            }
            
            
            foreach (var filename in Directory.EnumerateFiles(_epicManifestsDir, "*.item"))
            {
                try
                {
                    var fileContents = File.ReadAllText(filename).Trim();
                    var json = (JObject)JsonConvert.DeserializeObject(fileContents);
                    if (json?["MainGameCatalogNamespace"] == null) continue;
                    if (json["MainGameCatalogItemId"] == null) continue;
                    if (json["MainGameAppName"] == null) continue;
                    if (json["DisplayName"] == null) continue;
                    var appId = json["MainGameCatalogItemId"].ToString();

                    _settings.Applications.Add(new EpicInstalledApplication(
                        json["MainGameCatalogNamespace"].ToString(),
                        appId,
                        json["MainGameAppName"].ToString(),
                        json["DisplayName"].ToString(),
                        gameImageUrls.TryGetValue(appId, out var url) ? url : new string[]{ }
                    ));
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"LoadInstalledApps: Failed to iterate on {filename}: {ex}");
                    return;
                }
            }

            var apps = _settings.Applications.GroupBy(a => a.Id).Select(g => g.FirstOrDefault())?.OrderBy(a => a.DisplayName).ToList();
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} Found {apps.Count} apps");
            _settings.Applications = apps;
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
            switch (_settings.ImageFit)
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
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"SetImageFit unsupported ImageFit {_settings.ImageFit}");
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