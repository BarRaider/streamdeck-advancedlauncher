using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedLauncher.Backend
{

    public class ProcessesCache
    {
        #region Private Members

        private const int PROCESS_CACHE_LENGTH_MS = 2500;

        private static ProcessesCache instance = null;
        private static readonly object objLock = new object();
        private static readonly object processRefreshLock = new object();
        private Dictionary<string, int> dictProcessCounts = new Dictionary<string, int>();
        private DateTime lastUpdate = DateTime.MinValue;

        #endregion

        #region Constructors

        public static ProcessesCache Instance
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
                        instance = new ProcessesCache();
                    }
                    return instance;
                }
            }
        }

        private ProcessesCache()
        {
            PopulateCache();
        }

        private void PopulateCache()
        {
            lock (processRefreshLock)
            {
                if ((DateTime.Now - lastUpdate).TotalMilliseconds < PROCESS_CACHE_LENGTH_MS)
                {
                    return;
                }
                dictProcessCounts = Process.GetProcesses().ToList().GroupBy(p => p.ProcessName).Select(g => new { Name = g.Key, Count = g.Count() }).ToDictionary(g => g.Name.ToLowerInvariant(), g => g.Count);
                lastUpdate = DateTime.Now;
            }

        }

        public int GetProcessCountByProcessName(string processName)
        {
            try
            {
                if ((DateTime.Now - lastUpdate).TotalMilliseconds >= PROCESS_CACHE_LENGTH_MS)
                {
                    PopulateCache();
                }

                processName = processName.ToLowerInvariant();
                if (!dictProcessCounts.ContainsKey(processName))
                {
                    return 0;
                }
                return dictProcessCounts[processName];
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} ProcessesCache GetProcessCountByProcessName Exception: {ex}");

            }
            return 0;
        }

        #endregion
    }
}
