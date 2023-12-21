using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VBoxCleaner.IO;
using VBoxCleaner.Utils;

namespace VBoxCleaner.Cleaners
{
    /// <summary>
    /// Drag-n-drop temp files cleaner
    /// </summary>
    public static class DropCleaner
    {
        private const int DeleteIterationLimit = 100;  // Limits the number of iteration in deleting loop
        private const int AdditionalLifeTime = 60_000;  // time in milliseconds before deleting tmp files

        private static ConcurrentHashSet<string> pathPool = new ConcurrentHashSet<string>();
        private static readonly CancellationTokenSource _cts = new();
        private static int InnerDelay { get => _cts.IsCancellationRequested ? 10 : 1000; }

        public static void Clean()
        {
            // if (Disposed) return;
            Logger.WriteLine("DropCleaning.Clean is invoked");
            IEnumerable<string> drops;
            try
            {
                string windrive = Path.GetPathRoot(Environment.SystemDirectory);
                // <windrive>:\Users\<EveryUser>\AppData\Local\Temp\VirtualBox Dropped Files
                drops = Directory
                    .GetDirectories(Path.Combine(windrive, "users"))
                    .Select(path => Path.Combine(path, @"AppData\Local\Temp\VirtualBox Dropped Files"))
                    .Where(Directory.Exists);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"DropCleaning.Clean() exception:\n{ex}");
                return;
            }

            foreach (string drop in drops)
                try
                {
                    Logger.WriteLine($"dropPath: {drop}");
                    IEnumerable<string> subs = Directory.GetDirectories(drop);
                    foreach (string path in subs) _ = AddTask(path);  // start a bunch of tasks and let them run independently
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"DropCleaning.Clean() dropPath={drop} exception:\n{ex}");
                }
            // exit while tasks still run
        }

        private static async Task AddTask(string path)
        {
            if (pathPool.Add(path))
            {
                Task<bool> task = TryDeletePath(path);
                Logger.WriteLine($"AddTask started. Id={task.Id}, path={path}");
                bool res = await task;
                pathPool.Remove(path);
                Logger.WriteLine($"AddTask finished with {(res ? "OK" : "False")}. Id={task.Id}, path={path}");
            }
            else
            {
                Logger.WriteLine($"DropCleaning.AddTask ignored '{path}' because it's already in pathPool");
            }
        }

        private static async Task<bool> TryDeletePath(string path)
        {
            // waits until every file in path becomes free
            while (!IsEveryFileFree(path))
                await CancelableDelay.DelayOnceCancel(InnerDelay, _cts.Token);

            // After copying files from VM to temp folder OS copies them to the final destination (with a tiny delay)
            // It may cause a big delay while asking User about overwriting of existing files
            await CancelableDelay.Delay(AdditionalLifeTime, _cts.Token);

            for (int c = DeleteIterationLimit; Directory.Exists(path) && c > 0; c--, await CancelableDelay.DelayOnceCancel(InnerDelay, _cts.Token))
            {
                try
                {
                    string[] files = Directory.GetFiles(path);

                    if (files.Length == 0)
                        Directory.Delete(path);
                    else
                        foreach (var file in files)
                        {
                            FileInfo fi = new(file);
                            string hiddenName = fi.Name.AsHiddenFileName();
                            double sizeMbytes = (double)fi.Length / 1024 / 1024;
                            bool delResult = SafeDelete.SecureDelete(file);
                            Logger.WriteLine($"..deleting '{hiddenName}', size = {sizeMbytes:f2} MB: {(delResult ? "OK" : "Failed")}");
                        }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"TryDeletePath [{path}]:\n{ex}");
                }
            }

            return !Directory.Exists(path);
        }

        private static bool IsEveryFileFree(string path)
        {
            if (!Directory.Exists(path)) return true;
            try
            {
                var files = Directory.GetFiles(path);
                foreach (var file in files)
                    if (FileUsage.CheckState(file) == FileUsage.State.BUSY) return false;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"DropCleaning.IsEveryFileFree: {ex}");
                return true;  // let them think it's true
            }
            return true;
        }

        public static async Task DisposeAsync()
        {
            Logger.WriteLine("DropCleaning started disposing");
            Clean();
            _cts.Cancel();
            while (pathPool.Count > 0) await Task.Delay(InnerDelay);
            Logger.WriteLine("DropCleaning is disposed");
        }
    }
}
