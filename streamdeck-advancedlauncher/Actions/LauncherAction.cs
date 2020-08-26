using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdvancedLauncher.Actions
{

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: Pingu2k5
    // Subscriber: Chefmans
    //---------------------------------------------------

    [PluginActionId("com.barraider.advancedlauncher")]
    public class LauncherAction : PluginBase
    {
        private const int MAX_INSTANCES = 1;
        private const int POST_KILL_LAUNCH_DELAY = 0;
        private class PluginSettings
        {
            
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Application = String.Empty,
                    AppArguments = String.Empty,
                    AppStartIn = String.Empty,
                    LimitInstances = false,
                    MaxInstances = MAX_INSTANCES.ToString(),
                    KillInstances = false,
                    PostKillLaunchDelay = POST_KILL_LAUNCH_DELAY.ToString(),
                    RunAsAdmin = false,
                    ShowRunningIndicator = false,
                    BringToFront = false
                };
                return instance;
            }

            [FilenameProperty]
            [JsonProperty(PropertyName = "application")]
            public string Application { get; set; }

            [JsonProperty(PropertyName = "appStartIn")]
            public string AppStartIn { get; set; }

            [JsonProperty(PropertyName = "appArguments")]
            public string AppArguments { get; set; }

            [JsonProperty(PropertyName = "limitInstances")]
            public bool LimitInstances { get; set; }

            [JsonProperty(PropertyName = "maxInstances")]
            public string MaxInstances { get; set; }

            [JsonProperty(PropertyName = "killInstances")]
            public bool KillInstances { get; set; }

            [JsonProperty(PropertyName = "postKillLaunchDelay")]
            public string PostKillLaunchDelay { get; set; }

            [JsonProperty(PropertyName = "runAsAdmin")]
            public bool RunAsAdmin { get; set; }

            [JsonProperty(PropertyName = "showRunningIndicator")]
            public bool ShowRunningIndicator { get; set; }

            [JsonProperty(PropertyName = "bringToFront")]
            public bool BringToFront { get; set; }
        }

        #region Private Members
        private const string ADMIN_IMAGE_FILE = @"images\shield.png";

        private readonly PluginSettings settings;
        private int maxInstances = 1;
        private Bitmap fileImage;
        private int postKillLaunchDelay = 0;
        private Image prefetchedAdminImage = null;

        #endregion
        public LauncherAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            await HandleApplicationRun();
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            if (fileImage != null)
            {
                await Connection.SetImageAsync(fileImage);
                await HandleRunningIndicator();
            }
        }

        public async override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            string appOld = settings.Application;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            if (appOld != settings.Application) // Application has changed
            {
                InitializeStartInDirectory();
            }
            InitializeSettings();
            await Connection.SetTitleAsync((String)null);
            await SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void InitializeStartInDirectory()
        {
            if (!File.Exists(settings.Application))
            {
                return;
            }
            FileInfo fileInfo = new FileInfo(settings.Application);
            settings.AppStartIn = fileInfo.Directory.FullName;
        }

        private void InitializeSettings()
        {
            if (settings.KillInstances)
            {
                settings.MaxInstances = "1";
            }

            if (!Int32.TryParse(settings.MaxInstances, out maxInstances))
            {
                settings.MaxInstances = MAX_INSTANCES.ToString();
            }

            if (!Int32.TryParse(settings.PostKillLaunchDelay, out postKillLaunchDelay))
            {
                settings.PostKillLaunchDelay = POST_KILL_LAUNCH_DELAY.ToString();
            }

            FetchFileImage();
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

                    if (postKillLaunchDelay > 0)
                    {
                        Thread.Sleep(postKillLaunchDelay * 1000);
                    }
                }

                // Do not spawn a new process if there are already too many running
                if (settings.LimitInstances)
                {
                    int count = Process.GetProcessesByName(fileName).ToList().Count;
                    if (count >= maxInstances)
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Not spawning a new instance of {settings.Application} because there are {count}/{maxInstances} running.");

                        // Check if we should bring the existing process to front
                        HandleBringToFront(fileName);
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

            // Enter Working (Start In) Directory
            if (Directory.Exists(settings.AppStartIn))
            {
                start.WorkingDirectory = settings.AppStartIn;
            }
            // Enter the executable to run, including the complete path
            start.FileName = settings.Application;
            // Do you want to show a console window?
            //start.WindowStyle = ProcessWindowStyle.Hidden;
            //start.CreateNoWindow = true;

            if (settings.RunAsAdmin)
            {
                start.Verb = "runas";
            }

            // Launch the app
            Process.Start(start);
        }

        private async Task HandleRunningIndicator()
        {
            if (!settings.ShowRunningIndicator)
            {
                return;
            }

            FileInfo fileInfo = new FileInfo(settings.Application);
            string fileName = fileInfo.Name.Substring(0, fileInfo.Name.LastIndexOf('.'));

            // Check if there are any running instances
            if (Process.GetProcessesByName(fileName).Length > 0)
            {
                await Connection.SetTitleAsync($"🟢{new String(' ',10)}");
            }
            else
            {
                await Connection.SetTitleAsync((String)null);
            }
        }

        private void FetchFileImage()
        {
            if (fileImage != null)
            {
                fileImage.Dispose();
                fileImage = null;
            }

            // Try to extract Icon
            if (!String.IsNullOrEmpty(settings.Application) && File.Exists(settings.Application))
            {
                FileInfo fileInfo = new FileInfo(settings.Application);
                var fileIcon = IconExtraction.Shell.OfPath(fileInfo.FullName, small: false);
                if (fileIcon != null)
                {
                    using (Bitmap fileIconAsBitmap = fileIcon.ToBitmap())
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Bitmap size is: {fileIconAsBitmap.Width}x{fileIconAsBitmap.Height}");
                        fileImage = Tools.GenerateGenericKeyImage(out Graphics graphics);

                        // Check if app icon is smaller than the Stream Deck key
                        if (fileIconAsBitmap.Width < fileImage.Width && fileIconAsBitmap.Height < fileImage.Height)
                        {
                            float position = Math.Min(fileIconAsBitmap.Width / 2, fileIconAsBitmap.Height / 2);
                            graphics.DrawImage(fileIconAsBitmap, position, position, fileImage.Width - position * 2, fileImage.Height - position * 2);
                        }
                        else // App icon is bigger or equals to the size of a stream deck key
                        {
                            graphics.DrawImage(fileIconAsBitmap, 0, 0, fileImage.Width, fileImage.Height);
                        }

                        // Add shield image
                        if (settings.RunAsAdmin)
                        {
                            var adminImage = GetAdminImage();
                            if (adminImage != null)
                            {
                                graphics.DrawImage(adminImage, fileImage.Width - adminImage.Width, fileImage.Height - adminImage.Height, adminImage.Width, adminImage.Height);
                            }
                        }

                        graphics.Dispose();
                    }
                    fileIcon.Dispose();
                }
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsIconic(IntPtr hWnd);

        private void HandleBringToFront(string processFileName)
        {
            if (!settings.BringToFront)
            {
                return;
            }

            var proc = Process.GetProcessesByName(processFileName).Where(p => !String.IsNullOrEmpty(p.MainWindowTitle)).OrderByDescending(p => p.Id).FirstOrDefault();
            if (proc == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"HandleBringToFront error, could not find process for {processFileName}");
                return;
            }

            IntPtr handle = proc.MainWindowHandle;
            if (SetForegroundWindow(handle))
            {
                if (!IsIconic(handle))
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Successfully set foreground window for {processFileName} HWND: {handle}");
                    return;
                }
            }
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to set foreground window for {processFileName} HWND: {handle}, trying to force it");
            MinimizeAndRestoreWindow(handle);

        }

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, ShowWindowEnum nCmdShow);

        private void MinimizeAndRestoreWindow(IntPtr hWnd)
        {
            ShowWindow(hWnd, ShowWindowEnum.MINIMIZE);
            ShowWindow(hWnd, ShowWindowEnum.RESTORE);
        }

        private Image GetAdminImage()
        {
            if (prefetchedAdminImage == null)
            {
                if (File.Exists(ADMIN_IMAGE_FILE))
                {
                    prefetchedAdminImage = Image.FromFile(ADMIN_IMAGE_FILE);
                }
            }

            return prefetchedAdminImage;
        }


        #endregion
    }
}