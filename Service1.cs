using System.Management;
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using VBoxCleaner.Cleaners;
using VBoxCleaner.IO;
using VBoxCleaner.Scanners;
using VBoxCleaner.Utils;

namespace VBoxCleaner
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        protected override async void OnStart(string[] args)
        {
            Logger.WriteLine("OnStart");
            Thread.CurrentThread.Name = "VBoxCleanerMainThread";

            Start();

            VMsCleaner.Subscribe();
            RootCleaner.Subscribe();

            _ = DrivesScanner.RoutineAsync(startDelay: 10.Seconds(), scanDelay: 3.Minutes());
            _ = ProcessesScaner.StartAsync(tick: 500, finalTick: 10);

            await Task.Delay(1);

            Logger.WriteLine($"OnStart finishes here");
        }

        protected override void OnStop()
        {
            Logger.WriteLine("OnStop");
            Logger.Dispose();
        }

        protected override void OnShutdown()
        {
            Logger.WriteLine("OnShutdown started");

            ProcessesScaner.AskTermination();
            VMsCleaner.AskTermination();
            RootCleaner.AskTermination();
            _ = DropCleaner.DisposeAsync();

            DrivesScanner.Terminate();
            VMsCleaner.WaitTermination();
            RootCleaner.WaitTermination();

            Logger.WriteLine($"OnShutdown finished");
            Logger.Dispose();

            base.OnShutdown();
        }

        private void Start() {
            SelectQuery query = new SelectQuery("Win32_UserAccount");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject envVar in searcher.Get())
            {
                Logger.WriteLine($"Username : {envVar["Name"]}");
            }
        }

    }
}
