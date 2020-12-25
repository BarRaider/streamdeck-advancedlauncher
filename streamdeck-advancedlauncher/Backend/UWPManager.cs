using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedLauncher.Backend
{

    internal class UWPManager
    {
        #region Private Members

        private static UWPManager instance = null;
        private static readonly object objLock = new object();
        private List<UWPPackageInfo> apps = new List<UWPPackageInfo>();

        #endregion

        #region Constructors

        public static UWPManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                lock (objLock)
                {
                    if (instance == null)
                    {
                        instance = new UWPManager();
                    }
                    return instance;
                }
            }
        }

        private UWPManager()
        {
        }

        #endregion

        #region Public Methods

        public List<UWPPackageInfo> GetUWCApps(bool forceReload = false)
        {
            if (apps.Count > 0 && !forceReload)
            {
                return apps;
            }

            Windows.Management.Deployment.PackageManager packageManager = new Windows.Management.Deployment.PackageManager();
            IEnumerable<Windows.ApplicationModel.Package> packages = (IEnumerable<Windows.ApplicationModel.Package>)packageManager.FindPackagesForUser("");

            foreach (var package in packages.Where(p => !p.IsFramework).OrderBy(p => p.DisplayName))
            {
                try
                {
                    var storage = package.InstalledLocation;
                    apps.Add(new UWPPackageInfo(package.DisplayName, storage.Path, package.Logo));
                }
                catch { }
            }

            Logger.Instance.LogMessage(TracingLevel.INFO, $"GetUWCApps returned {apps.Count} apps");
            apps = apps.OrderBy(a => a.Name).ToList();
            return apps;
        }

        public async Task<bool> RunAppAsync(string appDisplayName)
        {
            try
            {
                Windows.Management.Deployment.PackageManager packageManager = new Windows.Management.Deployment.PackageManager();
                IEnumerable<Windows.ApplicationModel.Package> packages = (IEnumerable<Windows.ApplicationModel.Package>)packageManager.FindPackagesForUser("");

                var app = packages.FirstOrDefault(p => !p.IsFramework && p.DisplayName == appDisplayName);
                if (app == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"RunAppAsync could not find UWC app {appDisplayName}. Total apps: {packages.ToList().Count}");
                    return false;
                }

                var runner = await app.GetAppListEntriesAsync();
                if (runner.Count > 0)
                {
                    _ = runner[0].LaunchAsync();
                    return true;
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"RunAppAsync GetAppListEntriesAsync returned 0 entries for UWC app {appDisplayName}.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"RunAppAsync Exception: {ex}");
            }
            return false;
        }
    }

    #endregion
}
