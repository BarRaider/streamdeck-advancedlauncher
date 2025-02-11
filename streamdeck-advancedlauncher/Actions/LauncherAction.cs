using AdvancedLauncher.Backend;
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
using Windows.Storage;
using Windows.Storage.Streams;

namespace AdvancedLauncher.Actions
{

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: Pingu2k5
    // Subscriber: Chefmans
    //---------------------------------------------------

    [PluginActionId("com.barraider.advancedlauncher")]
    public class LauncherAction : KeypadBase
    {
        private const int MAX_INSTANCES = 1;
        private const int POST_KILL_LAUNCH_DELAY = 0;

        public enum LongPressAction
        {
            Nothing = 0,
            KillProcess = 1,
        }

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
                    BringToFront = false,
                    BackgroundRun = false,
                    ParseEnvironmentVariables = false,
                    LongKeypressTime = DEFAULT_LONG_KEYPRESS_LENGTH_MS.ToString(),
                    LongPressAction = LongPressAction.Nothing,
                    FixTinyIcons = false

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

            [JsonProperty(PropertyName = "backgroundRun")]
            public bool BackgroundRun { get; set; }

            [JsonProperty(PropertyName = "envVars")]
            public bool ParseEnvironmentVariables { get; set; }

            [JsonProperty(PropertyName = "longKeypressTime")]
            public string LongKeypressTime { get; set; }

            [JsonProperty(PropertyName = "longPressAction")]
            public LongPressAction LongPressAction { get; set; }

            [JsonProperty(PropertyName = "fixTinyIcons")]
            public bool FixTinyIcons { get; set; }
            
        }

        #region Private Members
        private const string ADMIN_IMAGE_FILE = @"images\shield.png";
        private const int DEFAULT_LONG_KEYPRESS_LENGTH_MS = 600;

        private readonly PluginSettings settings;
        private int maxInstances = 1;
        private Bitmap fileImage;
        private bool fileImageHasLaunchedIndicator = false;
        private int postKillLaunchDelay = 0;
        private Image prefetchedAdminImage = null;

        private bool longKeyPressed = false;
        private int longKeypressTime = DEFAULT_LONG_KEYPRESS_LENGTH_MS;
        private readonly System.Timers.Timer tmrRunLongPress = new System.Timers.Timer();

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
            tmrRunLongPress.Interval = longKeypressTime;
            tmrRunLongPress.Elapsed += TmrRunLongPress_Elapsed;
            InitializeSettings();
            OnTick();
        }

        public override void Dispose()
        {
            tmrRunLongPress.Stop();
            tmrRunLongPress.Elapsed -= TmrRunLongPress_Elapsed;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        private void TmrRunLongPress_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            LongKeyPress();
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed {this.GetType()}");
            longKeyPressed = false;

            tmrRunLongPress.Interval = longKeypressTime > 0 ? longKeypressTime : DEFAULT_LONG_KEYPRESS_LENGTH_MS;
            tmrRunLongPress.Start();
        }

        public async override void KeyReleased(KeyPayload payload)
        {
            tmrRunLongPress.Stop();
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Released {this.GetType()}");
            if (!longKeyPressed) // Take care of the short keypress
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Short Keypress {this.GetType()}");
                await HandleApplicationRun();
            }
        }

        public async override void OnTick()
        {
            if (fileImage != null)
            {
                HandleRunningIndicator();
                await Connection.SetImageAsync(fileImage);
            }
        }

        public async override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            string appOld = settings.Application;
            bool fixTinyIconsOld = settings.FixTinyIcons;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            if (appOld != settings.Application || fixTinyIconsOld != settings.FixTinyIcons) // Application has changed
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
            FetchFileImage();
        }

        private async void InitializeSettings()
        {
            bool updateSettings = false;
            if (settings.KillInstances)
            {
                settings.MaxInstances = "1";
                updateSettings = true;
            }

            if (!Int32.TryParse(settings.MaxInstances, out maxInstances))
            {
                settings.MaxInstances = MAX_INSTANCES.ToString();
                updateSettings = true;
            }

            if (!Int32.TryParse(settings.PostKillLaunchDelay, out postKillLaunchDelay))
            {
                settings.PostKillLaunchDelay = POST_KILL_LAUNCH_DELAY.ToString();
                updateSettings = true;
            }

            if (!Int32.TryParse(settings.LongKeypressTime, out longKeypressTime))
            {
                settings.LongKeypressTime = DEFAULT_LONG_KEYPRESS_LENGTH_MS.ToString();
                longKeypressTime = DEFAULT_LONG_KEYPRESS_LENGTH_MS;
                updateSettings = true;
            }

            if (updateSettings)
            {
                await SaveSettings();
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
                string fileName = fileInfo.Name.Substring(0, fileInfo.Name.LastIndexOf('.'));
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
                start.Arguments = settings.ParseEnvironmentVariables ? Environment.ExpandEnvironmentVariables(settings.AppArguments) : settings.AppArguments;
            }

            // Enter Working (Start In) Directory
            string workingDir = settings.ParseEnvironmentVariables ? Environment.ExpandEnvironmentVariables(settings.AppStartIn) : settings.AppStartIn;
            if (Directory.Exists(workingDir))
            {
                start.WorkingDirectory = workingDir;
            }
            // Enter the executable to run, including the complete path
            start.FileName = settings.ParseEnvironmentVariables ? Environment.ExpandEnvironmentVariables(settings.Application) : settings.Application;

            if (settings.BackgroundRun)
            {
                start.WindowStyle = ProcessWindowStyle.Hidden;
                //start.CreateNoWindow = true;
            }

            if (settings.RunAsAdmin)
            {
                start.Verb = "runas";
            }

            // Launch the app
            Process.Start(start);
        }

        private void HandleRunningIndicator()
        {
            if (!settings.ShowRunningIndicator)
            {
                return;
            }

            FileInfo fileInfo = new FileInfo(settings.Application);
            string fileName = fileInfo.Name.Substring(0, fileInfo.Name.LastIndexOf('.'));

            // Check if there are any running instances
            if (ProcessesCache.Instance.GetProcessCountByProcessName(fileName) > 0)
            {
                if (fileImageHasLaunchedIndicator) // No need to do anything as the indicator already exists
                {
                    return;
                }
                AddLaunchedIndicator();
            }
            else if (fileImageHasLaunchedIndicator)
            {
                FetchFileImage();
            }
        }

        private void AddLaunchedIndicator()
        {
            if (fileImage == null)
            {
                return;
            }

            try
            {
                Bitmap newImage = (Bitmap)fileImage.Clone();

                // Add Circle
                Graphics graphics = Graphics.FromImage(newImage);
                graphics.FillCircle(new SolidBrush(Color.FromArgb(0, 210, 106)), 30, 120, 12);

                // Replace image
                fileImage.Dispose();
                fileImage = newImage;
                fileImageHasLaunchedIndicator = true;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} AddLaunchedIndicator Exception: {ex}");
            }
        }

        private void FetchFileImage()
        {
            if (fileImage != null)
            {
                fileImage.Dispose();
                fileImage = null;
            }

            fileImageHasLaunchedIndicator = false;
            // Try to extract Icon
            if (!String.IsNullOrEmpty(settings.Application) && File.Exists(settings.Application))
            {
                FileInfo fileInfo = new FileInfo(settings.Application);
                using (Bitmap fileIcon = GetBestFileIcon(fileInfo.FullName))
                {
                    if (fileIcon == null)
                    {
                        return;
                    }
                    fileImage = Tools.GenerateGenericKeyImage(out Graphics graphics);

                    // Check if app icon is smaller than the Stream Deck key
                    if (fileIcon.Width < fileImage.Width && fileIcon.Height < fileImage.Height)
                    {
                        float position = Math.Min(fileIcon.Width / 2, fileIcon.Height / 2);
                        graphics.DrawImage(fileIcon, position, position, fileImage.Width - position * 2, fileImage.Height - position * 2);
                    }
                    else // App icon is bigger or equals to the size of a stream deck key
                    {
                        graphics.DrawImage(fileIcon, 0, 0, fileImage.Width, fileImage.Height);
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
            }
        }

        private Bitmap GetBestFileIcon(string fileName)
        {
            try
            {
                if (!settings.FixTinyIcons)
                {
                    Bitmap img = null;
                    try
                    {
                        img = IconExtraction.ThumbnailProvider.GetThumbnail(fileName, options: IconExtraction.ThumbnailOptions.IconOnly);
                        if (img != null)
                        {
                            return img;
                        }
                    }
                    catch { }
                }
                using Icon icon = IconExtraction.Shell.OfPath(fileName, small: false);
                if (icon != null)
                {
                    return icon.ToBitmap();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} GetBestFileIcon Exception: {ex}");
            }
            return null;
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

        private async void LongKeyPress()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Long Keypress {this.GetType()}");
            longKeyPressed = true;
            if (settings.LongPressAction == LongPressAction.Nothing)
            {
                return;
            }
            else if (settings.LongPressAction == LongPressAction.KillProcess)
            {
                await KillApplication();
                await Connection.ShowOk();
            }
        }


        #endregion
    }
}