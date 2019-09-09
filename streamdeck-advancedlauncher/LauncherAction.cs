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

namespace AdvancedLauncher
{

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: Pingu2k5
    //---------------------------------------------------

    [PluginActionId("com.barraider.advancedlauncher")]
    public class LauncherAction : PluginBase
    {
        private const string MAX_INSTANCES = "1";
        private class PluginSettings
        {
            
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Application = String.Empty,
                    AppArguments = String.Empty,
                    LimitInstances = false,
                    MaxInstances = MAX_INSTANCES,
                    KillInstances = false
                };
                return instance;
            }

            [FilenameProperty]
            [JsonProperty(PropertyName = "application")]
            public string Application { get; set; }

            [JsonProperty(PropertyName = "appArguments")]
            public string AppArguments { get; set; }

            [JsonProperty(PropertyName = "limitInstances")]
            public bool LimitInstances { get; set; }

            [JsonProperty(PropertyName = "maxInstances")]
            public string MaxInstances { get; set; }

            [JsonProperty(PropertyName = "killInstances")]
            public bool KillInstances { get; set; }
        }

        #region Private Members

        private readonly PluginSettings settings;
        private int maxInstances = 1;
        private Icon fileIcon;
        private Bitmap fileImage;

        #endregion
        public LauncherAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            InitializeSettings();
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");
            await HandleApplicationRun();
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
            if (settings.KillInstances)
            {
                settings.MaxInstances = "1";
            }

            if (!Int32.TryParse(settings.MaxInstances, out maxInstances))
            {
                settings.MaxInstances = MAX_INSTANCES;
            }

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

        private async Task HandleApplicationRun()
        {
            try
            {
                if (String.IsNullOrEmpty(settings.Application))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "HandleApplicationRun called, but no application configured");
                    await Connection.ShowAlert();
                    return;
                }

                if (!File.Exists(settings.Application))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"HandleApplicationRun called, but file does not exist: {settings.Application}");
                    await Connection.ShowAlert();
                    return;
                }

                FileInfo fileInfo = new FileInfo(settings.Application);
                string fileName = fileInfo.Name.Substring(0,fileInfo.Name.LastIndexOf('.'));
                // Kill existing instances
                if (settings.KillInstances)
                {
                    Process.GetProcessesByName(fileName).ToList().ForEach(p =>
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Killing process: {p.ProcessName} PID: {p.Id}");
                        p.Kill();
                    });
                }

                // Do not spwan a new process if there are already too many running
                if (settings.LimitInstances)
                {
                    int count = Process.GetProcessesByName(fileName).ToList().Count;
                    if (count >= maxInstances)
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Not spawning a new instance of {settings.Application} because there are {count}/{maxInstances} running.");
                        return;
                    }
                }

                // We can launch the app
                RunApplication();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleApplicationRun Exception for {settings.Application} {ex}");
                await Connection.ShowAlert();
            }
        }

        private void RunApplication()
        {
            // Prepare the process to run
            ProcessStartInfo start = new ProcessStartInfo();
            // Enter in the command line arguments, everything you would enter after the executable name itself
            if (!String.IsNullOrEmpty(settings.AppArguments))
            {
                start.Arguments = settings.AppArguments;
            }
            // Enter the executable to run, including the complete path
            start.FileName = settings.Application;
            // Do you want to show a console window?
            //start.WindowStyle = ProcessWindowStyle.Hidden;
            //start.CreateNoWindow = true;

            // Launch the app
            Process.Start(start);
        }

        #endregion
    }
}