using System;
using System.IO;
using System.Text;

namespace NetworkScanner
{
    public static class FileLogger
    {
        private static readonly object _lock = new object();
        private static string? _logFilePath;
        private static StreamWriter? _logWriter;

        static FileLogger()
        {
            InitializeLogger();
        }

        private static void InitializeLogger()
        {
            try
            {
                // Try to create logs directory in the application directory first
                string logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                
                // If we can't write to the application directory, use temp directory
                try
                {
                    Directory.CreateDirectory(logsDirectory);
                }
                catch (UnauthorizedAccessException)
                {
                    logsDirectory = Path.Combine(Path.GetTempPath(), "NetworkCheckLogs");
                    Directory.CreateDirectory(logsDirectory);
                    Console.WriteLine($"[INFO] Using temp directory for logs: {logsDirectory}");
                }

                // Create log file with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string logFileName = $"network_scan_{timestamp}.log";
                _logFilePath = Path.Combine(logsDirectory, logFileName);

                // Open file for writing (append mode)
                _logWriter = new StreamWriter(_logFilePath, append: true, encoding: Encoding.UTF8)
                {
                    AutoFlush = true // Ensure logs are written immediately
                };

                // Write initial log entry
                WriteLog("INFO", $"Logger initialized. Log file: {_logFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to initialize file logger: {ex.Message}");
                // Continue without file logging if initialization fails
                _logWriter = null;
            }
        }

        private static void WriteLog(string level, string message)
        {
            lock (_lock)
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string logEntry = $"[{timestamp}] [{level}] {message}";

                    // Write to console
                    Console.WriteLine($"[{level}] {message}");

                    // Write to file
                    _logWriter?.WriteLine(logEntry);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to write log: {ex.Message}");
                }
            }
        }

        public static void Debug(string message)
        {
            WriteLog("DEBUG", message);
        }

        public static void Debug(string message, Exception ex)
        {
            WriteLog("DEBUG", $"{message}\n{ex}");
        }

        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public static void Warn(string message)
        {
            WriteLog("WARN", message);
        }

        public static void Fatal(string message)
        {
            WriteLog("FATAL", message);
        }

        public static void Close()
        {
            lock (_lock)
            {
                try
                {
                    _logWriter?.Close();
                    _logWriter?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to close logger: {ex.Message}");
                }
            }
        }
    }
}