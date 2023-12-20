using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VBoxCleaner.IO;
using VBoxCleaner.Scanners;
using VBoxCleaner.Utils;

namespace VBoxCleaner.Cleaners
{
    public static class RootCleaner
    {
        private static readonly CancellationTokenSource _cts = new();
        private static bool hasLogs = false;
        private const int tick = 200;
        private const int finalTick = 10;
        private static bool busy = false;

        public static void Subscribe()
        {
            // trying to delete root logs on start
            hasLogs = !DeleteRootLogs();

            ProcessesScaner.OnVBoxSDSAppeared += () => { hasLogs = true; };
            ProcessesScaner.OnVBoxSDSGone += () => { _ = DeleteLogsAsync(); };
        }

        public static void WaitTermination()
        {
            Logger.WriteLine("RootCleaner.WaitTermination...");
            if (!_cts.IsCancellationRequested) return;
            while (hasLogs)
            {
                Logger.WriteLine("\tRootCleaner.WaitTermination hasLogs = true");
                Thread.Sleep(finalTick);
            }
            Logger.WriteLine("RootCleaner.WaitTermination has finished");
        }

        private static List<string> GetRootPaths()
        {
            List<string> result = [];
            string userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            string programData = Environment.GetEnvironmentVariable("PROGRAMDATA");

            if (userProfile.IsNotEmpty())
                result.Add(Path.Combine(userProfile!, ".VirtualBox"));    // "%USERPROFILE%\.VirtualBox"                                                                     
            else
                Logger.WriteLine("GetRootPaths: %USERPROFILE% is empty");

            if (programData.IsNotEmpty())
                result.Add(Path.Combine(programData!, "VirtualBox"));    // "%PROGRAMDATA%\VirtualBox"
            else
                Logger.WriteLine("GetRootPaths: %PROGRAMDATA% is empty");

            string windrive = Path.GetPathRoot(Environment.SystemDirectory);
            return Directory
                .GetDirectories(Path.Combine(windrive, "users"))
                .Select(path => Path.Combine(path, ".VirtualBox"))
                .Concat(result)
                .Where(Directory.Exists)
                .ToList();  // C:\Users\<EveryUser>\.VirtualBox
        }

        private static async Task DeleteLogsAsync()
        {
            if (busy) return;

            busy = true;
            // Taking several (10) attempts to delete log files and then exit
            bool delResult = false;
            int attempts = 0;

            for (; attempts < 10 && !delResult; ++attempts)
            {
                delResult = DeleteRootLogs();
                if (!delResult) await CancelableDelay.Delay(tick, finalTick, _cts.Token);
            }

            Logger.WriteLine($"..deleting root logs: {(delResult ? "OK" : "Failed")}");

            if (_cts.IsCancellationRequested && !delResult)
            {
                Logger.WriteLine($"..deleting root logs: Extra try-outs on shutdown..");
                while (true)
                {
                    delResult = DeleteRootLogs();
                    if (delResult) break;
                    await Task.Delay(finalTick);
                }
                Logger.WriteLine($"..deleting root logs: Done!");
            }

            hasLogs = !delResult;
            busy = false;
        }

        private static bool DeleteRootLogs()
        {
            bool totalResult = true;
            var RootPaths = GetRootPaths();

            foreach (string path in RootPaths)
            {
                if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path).Where(fullname => Path.GetFileName(fullname).Contains(".log"));
                    foreach (var file in files)
                        if (file != null)
                        {
                            bool delResult = SafeDelete.SecureDelete(file!);
                            Logger.WriteLine($"  ..deleting {file} : {(delResult ? "OK" : "Failed")}");
                            totalResult &= delResult;
                        }
                }
                else
                {
                    Logger.WriteLine($"path '{path}' doesn't exist");
                }
            }
            return totalResult;
        }

        public static void AskTermination()
        {
            Logger.WriteLine("RootCleaner.AskTermination");
            try { _cts.Cancel(); }
            catch (Exception ex) { Logger.WriteLine($"RootCleaner.AskTermination:\n{ex}"); }
        }
    }
}
