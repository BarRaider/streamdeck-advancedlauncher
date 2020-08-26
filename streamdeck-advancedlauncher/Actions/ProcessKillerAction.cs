using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedLauncher.Actions
{
    [PluginActionId("com.barraider.advancedlauncher.processkiller")]
    public class ProcessKillerAction : PluginBase
    {
        private class PluginSettings
        {

            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Application = String.Empty
                };
                return instance;
            }

            [FilenameProperty]
            [JsonProperty(PropertyName = "application")]
            public string Application { get; set; }
        }

        #region Private Members

        private readonly PluginSettings settings;
        private Icon fileIcon;
        private Bitmap fileImage;

        #endregion
        public ProcessKillerAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
                SaveSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            InitializeSettings();
            OnTick();
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");
            await KillApplication();
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            if (fileImage != null)
            {
                await Connection.SetImageAsync(fileImage);
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void InitializeSettings()
        {
            // Cleanup
            if (fileIcon != null)
            {
                fileIcon.Dispose();
                fileIcon = null;
            }
            if (fileImage != null)
            {
                fileImage.Dispose();
                fileImage = null;
            }

            // Try to extract Icon
            if (!String.IsNullOrEmpty(settings.Application) && File.Exists(settings.Application))
            {
                FileInfo fileInfo = new FileInfo(settings.Application);
                fileIcon = IconExtraction.Shell.OfPath(fileInfo.FullName, small: false);
                if (fileIcon != null)
                {
                    // Get a bitmap image of the icon
                    fileImage = fileIcon.ToBitmap();
                }
            }
        }

        private async Task KillApplication()
        {
            try
            {
                if (String.IsNullOrEmpty(settings.Application))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "KillApplication called, but no application configured");
                    await Connection.ShowAlert();
                    return;
                }

                if (!File.Exists(settings.Application))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"KillApplication called, but file does not exist: {settings.Application}");
                    await Connection.ShowAlert();
                    return;
                }

                FileInfo fileInfo = new FileInfo(settings.Application);
                string fileName = fileInfo.Name.Substring(0, fileInfo.Name.LastIndexOf('.'));
                // Kill existing instances
                Process.GetProcessesByName(fileName).ToList().ForEach(p =>
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Killing process: {p.ProcessName} PID: {p.Id}");
                    p.Kill();
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"KillApplication Exception for {settings.Application} {ex}");
                await Connection.ShowAlert();
            }
        }

        #endregion
    }
}