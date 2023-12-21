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

        protected override void OnStart(string[] args)
        {
            Logger.WriteLine("OnStart");
            Thread.CurrentThread.Name = "VBoxCleanerMainThread";

            string MyUser = System.Environment.UserName;
            string dropPath = $@"C:\Users\{MyUser}\AppData\Local\Temp\VirtualBox Dropped Files";
            Logger.WriteLine($"Current User's dropPath: {dropPath}");

            VMsCleaner.Subscribe();
            RootCleaner.Subscribe();

            _ = DrivesScanner.RoutineAsync(startDelay: 10.Seconds(), scanDelay: 3.Minutes());
            _ = ProcessesScaner.StartAsync(tick: 500, finalTick: 10);

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

    }
}
