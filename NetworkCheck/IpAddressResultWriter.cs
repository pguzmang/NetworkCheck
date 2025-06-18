using System;
using System.IO;

namespace NetworkScanner
{
    public class IpAddressResultWriter
    {
        private readonly string _outputDirectory;
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
        
        public IpAddressResultWriter(string outputDirectory = "NetworkTestResults")
        {
            _outputDirectory = outputDirectory;
            Directory.CreateDirectory(_outputDirectory);
        }
        
        public void WriteIpAddressResult(NetworkScanResult scanResult)
        {
            if (scanResult == null)
            {
                FileLogger.Warn("Cannot write IP address result - NetworkScanResult is null");
                return;
            }
            
            var filename = Path.Combine(_outputDirectory, "primary_ip_log.csv");
            FileLogger.Debug($"IP address log file path: {Path.GetFullPath(filename)}");
            
            // Check if file needs rotation
            if (File.Exists(filename) && new FileInfo(filename).Length > MaxFileSizeBytes)
            {
                RotateFile(filename);
            }
            
            var fileExists = File.Exists(filename);
            
            using (var writer = new StreamWriter(filename, append: true))
            {
                // Write header only if file doesn't exist
                if (!fileExists)
                {
                    writer.WriteLine("Timestamp,PrimaryIP,WorkingFromHome,WiFiSSID,EthernetDnsSuffix,VpnDetected,VpnIP");
                }
                
                // Write data
                var timestamp = DateTime.Now;
                var primaryIp = scanResult.PrimaryIpAddress ?? "N/A";
                var workingFromHome = scanResult.IsConsideredWorkingFromHome;
                var wifiSSID = scanResult.WiFiSSID ?? "N/A";
                var ethernetDnsSuffix = scanResult.EthernetDnsSuffix ?? "N/A";
                var vpnDetected = scanResult.VpnDetectedDuringScan;
                var vpnIp = scanResult.VpnIpAddressFound ?? "N/A";
                
                writer.WriteLine($"{timestamp:yyyy-MM-dd HH:mm:ss},{primaryIp},{workingFromHome},{wifiSSID},{ethernetDnsSuffix},{vpnDetected},{vpnIp}");
            }
            
            FileLogger.Info($"IP address result logged to: {filename}");
            
            // Display colored console output
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Primary IP logged: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(scanResult.PrimaryIpAddress ?? "N/A");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(" | Location: ");
            Console.ForegroundColor = scanResult.IsConsideredWorkingFromHome ? ConsoleColor.Yellow : ConsoleColor.Blue;
            Console.WriteLine(scanResult.IsConsideredWorkingFromHome ? "Home" : "Office");
            Console.ResetColor();
        }
        
        private void RotateFile(string filename)
        {
            var directory = Path.GetDirectoryName(filename) ?? _outputDirectory;
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            var extension = Path.GetExtension(filename);
            
            var firstFile = filename; // e.g., primary_ip_log.csv
            var secondFile = Path.Combine(directory, $"{nameWithoutExtension}_2{extension}"); // e.g., primary_ip_log_2.csv
            
            if (File.Exists(secondFile))
            {
                // Both files exist and first is full
                // Delete the second file and move first to second
                File.Delete(secondFile);
                File.Move(firstFile, secondFile);
                FileLogger.Info($"Rotated IP log files: deleted {Path.GetFileName(secondFile)}, moved {Path.GetFileName(firstFile)} to {Path.GetFileName(secondFile)}");
            }
            else
            {
                // Only first file exists and is full
                // Move first to second
                File.Move(firstFile, secondFile);
                FileLogger.Info($"Created second IP log file: moved {Path.GetFileName(firstFile)} to {Path.GetFileName(secondFile)}");
            }
        }
    }
}