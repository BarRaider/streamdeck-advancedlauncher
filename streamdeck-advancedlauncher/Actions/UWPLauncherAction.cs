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
    [PluginActionId("com.barraider.msstorelauncher")]
    public class UWPLauncherAction : PluginBase
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
            public List<UWPPackageInfo> Applications { get; set; }

            [JsonProperty(PropertyName = "showAppName")]
            public bool ShowAppName { get; set; }
        }

        #region Private Members

        private readonly PluginSettings settings;
        private TitleParameters titleParameters;
        private Bitmap appImage;
        private UWPPackageInfo appInfo;

        #endregion
        public UWPLauncherAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            InitializeSettings();
            OnTick();
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            Connection.OnTitleParametersDidChange -= Connection_OnTitleParametersDidChange;
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");
            if (String.IsNullOrEmpty(settings.ApplicationId))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, "Key pressed but application id is null!");
                return;
            }

            if (!await UWPManager.Instance.RunAppAsync(settings.ApplicationId))
            {
                await Connection.ShowAlert();
            }
            else
            {
                await Connection.ShowOk();
            }
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
                await Connection.SetTitleAsync(Tools.SplitStringToFit(appInfo.Name, titleParameters));
            }
        }

        public async override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            bool showName = settings.ShowAppName;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            if (showName != settings.ShowAppName && !settings.ShowAppName)
            {
                await Connection.SetTitleAsync(null);
            }
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


        private void FetchAppInfo()
        {
            try
            {
                appInfo = null;
                // Cleanup
                if (appImage != null)
                {
                    appImage.Dispose();
                    appImage = null;
                }

                if (!String.IsNullOrEmpty(settings.ApplicationId))
                {
                    appInfo = UWPManager.Instance.GetUWCApps().FirstOrDefault(a => a.Name == settings.ApplicationId);
                    if (appInfo == null)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"FetchAppInfo could not find UWC app: {settings.ApplicationId}");
                        return;
                    }
                    appImage = (Bitmap)Image.FromFile(appInfo.Logo.LocalPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"FetchAppInfo Exception: {ex}");
            }
        }

        private void LoadInstalledApps()
        {
            settings.Applications = UWPManager.Instance.GetUWCApps();
        }

        private void Connection_OnSendToPlugin(object sender, SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "refreshapps":
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"refreshApps called");
                        settings.Applications = UWPManager.Instance.GetUWCApps(true);
                        SaveSettings();
                        break;
                }
            }
        }

        #endregion
    }
}