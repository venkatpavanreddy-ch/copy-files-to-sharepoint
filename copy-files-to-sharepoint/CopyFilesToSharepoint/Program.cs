using CopyFilesToSharePoint.Common;
using log4net;
using System;
using System.ServiceProcess;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]
namespace CopyFilesToSharePoint
{
    public class Program
    {
        private static readonly ILog _logger = ConfigureLogging.For<Program>();

        public static void Main(string[] args)
        {
            if (ServiceHelper.IsRunningAsService)
            {
                _logger.Info("Starting as service");
                ServiceBase.Run(new Runner());
                return;
            }

            //_logger.Info("Starting as console");
            Console.Title = "Copy Files To SharePoint";
            new Runner().ConsoleStart();

            while (true)
            {
                Console.WriteLine("Press any key to exit");
                Console.ReadKey(true);

                Console.WriteLine("Are you sure you want to exit? Type 'yes' to close this.");
                var typedLine = Console.ReadLine();

                if (string.Equals(typedLine, "yes", StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }
    }
}
