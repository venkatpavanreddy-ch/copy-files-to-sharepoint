using log4net;
using log4net.Appender;
using System;
using System.Linq;

namespace CopyFilesToSharePoint.Common
{
    public static class ConfigureLogging
    {
        private static readonly AppSettings _appSettings = new AppSettings();

        public static ILog For<T>()
        {
            var logger = LogManager.GetLogger(typeof(T));

            var loggerRepositories = LogManager.GetAllRepositories();

            if (!loggerRepositories.Any())
                throw new Exception("Missing log4net repositories, check log4net.config file is correctly configured");

            foreach (var loggerRepository in loggerRepositories)
            {
                var appenders = loggerRepository.GetAppenders().OfType<AdoNetAppender>();

                foreach (var adoNetAppender in appenders)
                {
                    adoNetAppender.ConnectionString = _appSettings.LeberDWSConnectionString;
                    adoNetAppender.ActivateOptions();
                }
            }
            return logger;
        }
    }
}
