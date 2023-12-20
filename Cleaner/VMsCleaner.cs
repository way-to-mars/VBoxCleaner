using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VBoxCleaner.IO;
using VBoxCleaner.Scanners;
using VBoxCleaner.Utils;

namespace VBoxCleaner.Cleaners
{
    internal static class VMsCleaner
    {
        private static readonly ConcurrentDictionary<string, int> logDict = new();
        private static readonly CancellationTokenSource _cts = new();
        private const int tick = 50;
        private const int finalTick = 10;
        private static int deletingCounter = 0;

        //       public static Action OnTerminated = delegate { };
        public static Action OnVMAppeared = delegate { };
        public static Action OnAllVMsGone = delegate { };

        public static void Subscribe()
        {
            ProcessesScaner.OnVMsAdd += PutProcesses;
            ProcessesScaner.OnVMsGone += Remove;
            DrivesScanner.OnLogPathFound += PutPath;
        }
        public static void WaitTermination()
        {
            Logger.WriteLine("VMsCleaner.WaitTermination...");
            if (!_cts.IsCancellationRequested) return;
            while (deletingCounter > 0 || !logDict.IsEmpty)
            {
                Logger.WriteLine($"\tVMsCleaner.WaitTermination counter = {deletingCounter} logDict has {logDict.Count} entries");
                Thread.Sleep(finalTick);
            }
            // force termination
            KillProcessesByName("VBoxSVC");
            KillProcessesByName("VBoxSDS");
            Logger.WriteLine("VMsCleaner.WaitTermination has finished");
        }
        private static void PutProcesses(List<Process> processes)
        {
            bool emptyBefore = logDict.IsEmpty;
            foreach (var process in processes)
            {
                try
                {
                    int pid = process.Id;
                    string path = process.GetCommandLine().LogPath();
                    if (path is not null)
                    {
                        if (logDict.TryAdd(path, pid))
                        {
                            Debug.WriteLine($"Created new pair '{path}' to {pid}");
                            Debug.WriteLine(string.Join(Environment.NewLine, logDict));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"VMsCleaning.PutProcesses exception:\n{ex}");
                }
            }
            if (!logDict.IsEmpty && emptyBefore)
            {
                OnVMAppeared.Invoke();
            }
        }
        private static void PutPath(string path)
        {
            if (!logDict.ContainsKey(path))
            {
                Debug.WriteLine($"PutPath: '{path}' is about to be cleaned");
                _ = DeleteLogsAsync(path);
            }
            else
                Debug.WriteLine($"PutPath: '{path}' is already in logDict ");
        }
        private static void Remove(List<Process> processes)
        {
            DropCleaner.Clean();
            foreach (var process in processes)
            {
                string path = logDict.FindFirstKeyByValue(process.Id, "");
                if (path.IsNotEmpty())
                {
                    logDict.TryRemove(path, out _);  // Remove path from logDict no matter if logs are deleted or not
                    Interlocked.Increment(ref deletingCounter);
                    _ = DeleteLogsAsync(path);  // Invoke deleting and don't mind when it finishes
                }
                else
                    Debug.WriteLine($"no logs for process with pid={process.Id}");
            }

            if (logDict.IsEmpty)
            {
                OnAllVMsGone.Invoke();
            }
        }
        private static async Task DeleteLogsAsync(string path)
        {
            // Taking several (10) attempts to delete log files and then exit
            bool delResult = false;
            int attempts = 0;

            for (; attempts < 10 && !delResult; ++attempts)
            {
                delResult = DeleteVMLogs(path);

                if (!delResult) await CancelableDelay.Delay(tick, finalTick, _cts.Token);
            }

            Logger.WriteLine($"..deleting logs in '{path}'[attempts={attempts}]: {(delResult ? "OK" : "Failed")} [counter={deletingCounter}]");
            Interlocked.Decrement(ref deletingCounter);
        }
        private static bool DeleteVMLogs(string path)
        {
            bool totalResult = true;

            if (Directory.Exists(path))
            {
                try
                {
                    IEnumerable<string> files = Directory.GetFiles(path).Where(f => f.IsVboxLog());
                    foreach (string file in files)
                    {
                        bool delResult = SafeDelete.SecureDelete(file!);
                        Logger.WriteLine($"..deleting {file} : {(delResult ? "OK" : "Failed")}");
                        totalResult &= delResult;
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"DeleteVMLogs caught exception: {ex}");
                    return false;
                }
            }
            else
            {
                Logger.WriteLine($"path '{path}' doesn't exist");
            }

            return totalResult;
        }
        private static void KillProcessesByName(string name)
        {
            foreach (Process p in Process.GetProcessesByName(name))
            {
                try
                {
                    p.Kill();
                    Logger.WriteLine($"KillProcessesByName: killing {name} - done. HasExited = {p.HasExited}");
                }
                catch (Exception ex) { Logger.WriteLine($"KillProcessesByName: killing {name} - failed{ex}"); }
            }
        }
        public static void AskTermination()
        {
            Logger.WriteLine("VMsCleaner.AskTermination");
            try { _cts.Cancel(); }
            catch (Exception ex) { Logger.WriteLine($"VMsCleaner.AskTermination:\n{ex}"); }
        }
    }
}
