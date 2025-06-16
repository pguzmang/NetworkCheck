using System;
using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;

namespace NetworkScanner
{
    public static class FileLogger
    {
        private static readonly ILog _logger;
        
        static FileLogger()
        {
            // Configure log4net using the config file
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            var configFile = new FileInfo(Path.Combine("Config", "log4net.config"));
            XmlConfigurator.Configure(logRepository, configFile);
            
            _logger = LogManager.GetLogger(typeof(FileLogger));
            _logger.Info("Logger initialized using log4net");
        }

        public static void Debug(string message)
        {
            _logger.Debug(message);
        }

        public static void Debug(string message, Exception ex)
        {
            _logger.Debug(message, ex);
        }

        public static void Info(string message)
        {
            _logger.Info(message);
        }

        public static void Warn(string message)
        {
            _logger.Warn(message);
        }

        public static void Error(string message)
        {
            _logger.Error(message);
        }

        public static void Error(string message, Exception ex)
        {
            _logger.Error(message, ex);
        }

        public static void Fatal(string message)
        {
            _logger.Fatal(message);
        }

        public static void Fatal(string message, Exception ex)
        {
            _logger.Fatal(message, ex);
        }

        public static void Close()
        {
            // log4net handles cleanup automatically, but we'll shutdown for good measure
            LogManager.Shutdown();
        }
    }
}