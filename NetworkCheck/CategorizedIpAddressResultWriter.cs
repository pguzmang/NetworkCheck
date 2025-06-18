using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace NetworkScanner
{
    public class CategorizedIpAddressResultWriter
    {
        private readonly string _outputDirectory;
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
        
        // Network category patterns (matching NetworkIpAddress.cs)
        private static readonly Dictionary<Regex, string> VpnPatterns = new Dictionary<Regex, string>
        {
            { new Regex(@"^10\.93\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$", RegexOptions.Compiled), "vpn_detroit" },
            { new Regex(@"^10\.94\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$", RegexOptions.Compiled), "vpn_troy" },
            { new Regex(@"^10\.95\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$", RegexOptions.Compiled), "vpn_palo_alto" }
        };
        
        private static readonly Dictionary<Regex, string> OfficePatterns = new Dictionary<Regex, string>
        {
            { new Regex(@"^10\.5\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$", RegexOptions.Compiled), "office_campus_wireless" },
            { new Regex(@"^10\.4\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$", RegexOptions.Compiled), "office_internet_wireless" },
            { new Regex(@"^10\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$", RegexOptions.Compiled), "office_corporate_network" },
            { new Regex(@"^172\.16\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$", RegexOptions.Compiled), "office_internal_network" }
        };
        
        public CategorizedIpAddressResultWriter(string outputDirectory = "NetworkTestResults")
        {
            _outputDirectory = outputDirectory;
            Directory.CreateDirectory(_outputDirectory);
        }
        
        public void WriteIpAddressResult(NetworkScanResult scanResult)
        {
            if (scanResult == null)
            {
                FileLogger.Warn("Cannot write categorized IP address result - NetworkScanResult is null");
                return;
            }
            
            string category = DetermineNetworkCategory(scanResult);
            string filename = Path.Combine(_outputDirectory, $"ip_log_{category}.csv");
            
            FileLogger.Debug($"Categorized IP log file: {Path.GetFullPath(filename)} (category: {category})");
            
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
                    writer.WriteLine("Timestamp,PrimaryIP,WorkingFromHome,WiFiSSID,EthernetDnsSuffix,VpnDetected,VpnIP,NetworkCategory");
                }
                
                // Write data
                var timestamp = DateTime.Now;
                var primaryIp = scanResult.PrimaryIpAddress ?? "N/A";
                var workingFromHome = scanResult.IsConsideredWorkingFromHome;
                var wifiSSID = scanResult.WiFiSSID ?? "N/A";
                var ethernetDnsSuffix = scanResult.EthernetDnsSuffix ?? "N/A";
                var vpnDetected = scanResult.VpnDetectedDuringScan;
                var vpnIp = scanResult.VpnIpAddressFound ?? "N/A";
                
                writer.WriteLine($"{timestamp:yyyy-MM-dd HH:mm:ss},{primaryIp},{workingFromHome},{wifiSSID},{ethernetDnsSuffix},{vpnDetected},{vpnIp},{category}");
            }
            
            FileLogger.Info($"Categorized IP address result logged to: {filename} (category: {category})");
            
            // Display colored console output with category
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("IP logged to ");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write($"{category}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(" file: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(scanResult.PrimaryIpAddress ?? "N/A");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(" | Location: ");
            Console.ForegroundColor = scanResult.IsConsideredWorkingFromHome ? ConsoleColor.Yellow : ConsoleColor.Blue;
            Console.WriteLine(scanResult.IsConsideredWorkingFromHome ? "Home" : "Office");
            Console.ResetColor();
        }
        
        private string DetermineNetworkCategory(NetworkScanResult scanResult)
        {
            string primaryIp = scanResult.PrimaryIpAddress ?? "";
            bool isHome = scanResult.IsConsideredWorkingFromHome;
            
            // Check VPN patterns first (home categories)
            foreach (var pattern in VpnPatterns)
            {
                if (pattern.Key.IsMatch(primaryIp))
                {
                    return "home_vpn";
                }
            }
            
            // Use WiFi SSID for WiFi connections
            if (!string.IsNullOrWhiteSpace(scanResult.WiFiSSID))
            {
                string cleanSSID = SanitizeFileName(scanResult.WiFiSSID);
                return isHome ? $"home_{cleanSSID}" : $"office_{cleanSSID}";
            }
            
            // Use Ethernet DNS suffix for ethernet connections
            if (!string.IsNullOrWhiteSpace(scanResult.EthernetDnsSuffix))
            {
                string cleanDNS = SanitizeFileName(scanResult.EthernetDnsSuffix);
                return isHome ? $"home_{cleanDNS}" : $"office_{cleanDNS}";
            }
            
            // Default categorization based on working from home status
            return isHome ? "home_unknown" : "office_unknown";
        }
        
        private string SanitizeFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "unknown";
            
            // Remove invalid file name characters and convert to lowercase
            string sanitized = input.ToLowerInvariant()
                .Replace(" ", "_")
                .Replace(".", "_")
                .Replace("-", "_")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("[", "")
                .Replace("]", "")
                .Replace("{", "")
                .Replace("}", "")
                .Replace("/", "_")
                .Replace("\\", "_")
                .Replace(":", "_")
                .Replace("*", "_")
                .Replace("?", "_")
                .Replace("\"", "_")
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace("|", "_");
            
            // Limit length to avoid very long file names
            if (sanitized.Length > 20)
                sanitized = sanitized.Substring(0, 20);
            
            return sanitized;
        }
        
        private void RotateFile(string filename)
        {
            var directory = Path.GetDirectoryName(filename) ?? _outputDirectory;
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            var extension = Path.GetExtension(filename);
            
            var firstFile = filename;
            var secondFile = Path.Combine(directory, $"{nameWithoutExtension}_2{extension}");
            
            if (File.Exists(secondFile))
            {
                File.Delete(secondFile);
                File.Move(firstFile, secondFile);
                FileLogger.Info($"Rotated categorized IP log files: deleted {Path.GetFileName(secondFile)}, moved {Path.GetFileName(firstFile)} to {Path.GetFileName(secondFile)}");
            }
            else
            {
                File.Move(firstFile, secondFile);
                FileLogger.Info($"Created second categorized IP log file: moved {Path.GetFileName(firstFile)} to {Path.GetFileName(secondFile)}");
            }
        }
    }
}