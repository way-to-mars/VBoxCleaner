using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VBoxCleaner.IO;
using VBoxCleaner.Utils;



namespace VBoxCleaner.Scanners
{

    public static class DrivesScanner
    {
        private static readonly Dictionary<string, HashSet<string>> ExcludePaths = [];
        private static DateTime DictionaryDate = DateTime.Now;
        private static readonly CancellationTokenSource _cts = new();
        private static bool Active => !_cts.IsCancellationRequested;
        public static event Action<string> OnLogPathFound = delegate { };

        public static async Task RoutineAsync(int startDelay, int scanDelay)
        {
            await CancelableDelay.Delay(startDelay, _cts.Token);

            while (Active)
            {
                ResetExcludePathsEvery(hours: 3);
                await ScanDrives();
                await Task.Delay(scanDelay, _cts.Token);
            }

            Logger.WriteLine($"DrivesScanner.RoutineAsync has stopped");
        }

        private static async Task ScanDrives()
        {
            List<string> vmPaths = [];
            List<Task> byDisk = [];

            try
            {
                foreach (var drive in DriveInfo
                    .GetDrives()
                    .Where(d => d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                {
                    byDisk.Add(ScanPath(
                        drive.RootDirectory.FullName,
                        drive.DriveType == DriveType.Fixed));
                }

                foreach (Task task in byDisk) await task;

                return;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"VMsDrivesScanner.ScanDrives exception:\n{ex}");
                return;
            }
        }

        private static async Task ScanPath(string root, bool fixedDrive)
        {
            Logger.WriteLine($"VMsDrivesScanner.ScanPath: {root}");
            Queue<string> folders = new();
            folders.Enqueue(root);
            long i = 0;
            while (folders.Count != 0 && Active)
            {
                string currentFolder = folders.Dequeue();
                try
                {
                    if (ExcludePaths.DoesntContain(root, currentFolder))
                    {
                        string[] vboxFilesIndise = Directory.GetFiles(currentFolder, "*.vbox", SearchOption.TopDirectoryOnly);
                        if (vboxFilesIndise.Length > 0 && currentFolder.HasVBoxLogsInside())
                        {
                            string logPath = Path.Combine(currentFolder, "Logs");
                            Logger.WriteLine($"ScanPath found logs in {logPath}");
                            OnLogPathFound(logPath);
                        }

                        string[] foldersInCurrent = Directory.GetDirectories(currentFolder, "*.*", SearchOption.TopDirectoryOnly);
                        foreach (string _current in foldersInCurrent)
                            folders.Enqueue(_current);
                        i++;
                        if (i % 100 == 0)
                        {
                            await Task.Delay(10, _cts.Token);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex.GetType() == typeof(UnauthorizedAccessException)) { }
                    ExcludePaths.Insert(root, currentFolder);
                }
            }
            folders.Clear();
            Logger.WriteLine($"ScanPath finished on {root}, total folders = {i}");
            return;
        }

        private static bool HasVBoxLogsInside(this string folder)
        {
            try
            {
                string LogsPath = Path.Combine(folder, "Logs");
                if (Directory.Exists(LogsPath))
                {
                    var files = Directory.GetFiles(LogsPath, "*.*");
                    foreach (string fullName in files)
                        if (fullName.IsVboxLog()) return true;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"HasVMLogsInside exception\n{ex}");
            }
            return false;
        }

        private static void ResetExcludePathsEvery(double hours)
        {
            TimeSpan ts = DateTime.Now - DictionaryDate;

            if (ts.TotalHours >= hours)
            {
                DictionaryDate = DateTime.Now;
                ExcludePaths.Clear();
                Logger.WriteLine($"VMsDrivesScanner. Dictionary is cleared because {ts.TotalHours} >= {hours}");
            }
        }

        public static void Terminate()
        {
            Logger.WriteLine($"DrivesScanner.Terminate...");
            _cts.Cancel();
        }

    }
}
