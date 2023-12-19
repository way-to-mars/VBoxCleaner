using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using VBoxCleaner.IO;
using VBoxCleaner.Utils;
using System.Collections.Generic;

namespace VBoxCleaner.Scanners
{

    public static class ProcessesScaner
    {
        private static readonly List<Process> VirtualBoxVMArray = [];
        private static readonly List<Process> VBoxSDSArray = [];
        private static readonly CancellationTokenSource _cts = new();
        private static bool Active => !_cts.IsCancellationRequested;

        public static event Action<List<Process>> OnVMsAdd = delegate { };
        public static event Action<List<Process>> OnVMsGone = delegate { };
        public static event Action OnVBoxSDSAppeared = delegate { };
        public static event Action OnVBoxSDSGone = delegate { };

        public static async Task StartAsync(int tick, int finalTick)
        {
            // ArgumentOutOfRangeException.ThrowIfLessThan(tick, finalTick);
            if (tick < finalTick) throw new ArgumentOutOfRangeException();
            while (Active || VirtualBoxVMArray.Count > 0 || VBoxSDSArray.Count > 0)
            {
                UpdateVirtualBoxVMArray(Process.GetProcessesByName("VirtualBoxVM"));
                UpdateVBoxSDSArray(Process.GetProcessesByName("VBoxSDS"));
                await CancelableDelay.Delay(tick, finalTick, _cts.Token);
            }
            Logger.WriteLine("ProcessesScaner.StartAsync has stopped");
        }

        private static void UpdateVirtualBoxVMArray(Process[] newArray)
        {
            List<Process> Added = ListAdded(VirtualBoxVMArray, newArray);
            List<Process> Gone = ListGone(VirtualBoxVMArray, newArray);
            VirtualBoxVMArray.Replace(newArray);
            if (Added.Count > 0 || Gone.Count > 0) Debug.WriteLine(string.Join("; ", Added) + " <-Added//Gone-> " + string.Join("; ", Gone));

            if (Gone.Count > 0)
            {
                OnVMsGone(Gone);
            }

            if (Added.Count > 0)
            {
                OnVMsAdd(Added);
            }
        }

        private static void UpdateVBoxSDSArray(Process[] newArray)
        {
            List<Process> Added = ListAdded(VBoxSDSArray, newArray);
            List<Process> Gone = ListGone(VBoxSDSArray, newArray);
            if (Added.Count > 0 || Gone.Count > 0) Debug.WriteLine(string.Join("; ", Added) + " <-Added//Gone-> " + string.Join("; ", Gone));

            if (VBoxSDSArray.Count == 0 && Added.Count > 0)
            {
                OnVBoxSDSAppeared();
            }

            if (VBoxSDSArray.Count > 0 && newArray.Length == 0)
            {
                OnVBoxSDSGone();
            }

            VBoxSDSArray.Replace(newArray);
        }

        private static List<Process> ListAdded(List<Process> oldList, in Process[] newArray)
        {
            List<Process> Added = [];
            foreach (Process newProcess in newArray)
            {
                bool newby = true;
                foreach (Process oldProcess in oldList)
                    if (oldProcess.Id == newProcess.Id) { newby = false; break; }
                if (newby) Added.Add(newProcess);
            }
            return Added;
        }

        private static List<Process> ListGone(List<Process> oldList, in Process[] newArray)
        {
            List<Process> Gone = [];
            foreach (Process oldProcess in oldList)
            {
                bool gone = true;
                foreach (Process newProcess in newArray)
                    if (newProcess.Id == oldProcess.Id) { gone = false; break; }
                if (gone) Gone.Add(oldProcess);
            }
            return Gone;
        }

        private static void Replace(this List<Process> oldList, in Process[] newArray)
        {
            oldList.Clear();
            oldList.AddRange(newArray);
        }

        public static void AskTermination()
        {
            Logger.WriteLine("ProcessesScaner.AskTermination");
            try { _cts.Cancel(); }
            catch (Exception ex) { Logger.WriteLine($"ProcessesScaner.AskTermination:\n{ex}"); }
        }
    }
}
