using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NetworkScanner
{
    public class CategorizedPingJitterResultWriter
    {
        private readonly string _outputDirectory;
        private readonly string _networkCategory;
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
        
        // Network category patterns (matching NetworkIpAddress.cs and CategorizedIpAddressResultWriter.cs)
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
        
        public CategorizedPingJitterResultWriter(NetworkScanResult scanResult, string outputDirectory = "NetworkTestResults")
        {
            _outputDirectory = outputDirectory;
            Directory.CreateDirectory(_outputDirectory);
            _networkCategory = DetermineNetworkCategory(scanResult);
        }
        
        public void WriteResults(List<PingResult> results)
        {
            // Categorize results by server type (same as original)
            var external = results.Where(r => IsExternal(r.Host)).ToList();
            var internalAes = results.Where(r => IsInternalAes(r.Host)).ToList();
            var internalOther = results.Where(r => IsInternal(r.Host) && !IsInternalAes(r.Host)).ToList();
            
            // Write categorized CSV files with network category suffix
            WritePingResultsCsv("external", external);
            WritePingResultsCsv("internalaes", internalAes);
            WritePingResultsCsv("internal", internalOther);
            
            WriteJitterResultsCsv("external", external);
            WriteJitterResultsCsv("internalaes", internalAes);
            WriteJitterResultsCsv("internal", internalOther);
        }
        
        private void WritePingResultsCsv(string category, List<PingResult> results)
        {
            if (results.Count == 0) return;
            
            var filename = Path.Combine(_outputDirectory, $"ping_{category}_{_networkCategory}.csv");
            
            // Check if file needs rotation
            if (File.Exists(filename) && new FileInfo(filename).Length > MaxFileSizeBytes)
            {
                RotateFile(filename);
            }
            
            var fileExists = File.Exists(filename);
            
            // Calculate current median from existing data before writing new values
            double? currentMedian = null;
            if (fileExists)
            {
                currentMedian = CalculateMedianFromCsv(filename, "MedianPing(ms)");
            }
            
            using (var writer = new StreamWriter(filename, append: true))
            {
                // Write header only if file doesn't exist
                if (!fileExists)
                {
                    writer.WriteLine("Timestamp,Host,MedianPing(ms),Success,NetworkCategory");
                }
                
                // Write data and compare last value to median
                for (int i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    writer.WriteLine($"{result.Timestamp:yyyy-MM-dd HH:mm:ss},{result.Host},{(result.Success ? result.MedianPing.ToString("F2") : "FAIL")},{result.Success},{_networkCategory}");
                    
                    // Compare last value to current median
                    if (i == results.Count - 1 && result.Success && currentMedian.HasValue)
                    {
                        var comparison = result.MedianPing > currentMedian.Value ? "above" : 
                                       result.MedianPing < currentMedian.Value ? "below" : "equal to";
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write($"Last ping value for {category} on ");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{_networkCategory}");
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write($" (");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{result.MedianPing:F2} ms");
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write($") is {comparison} current median (");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{currentMedian.Value:F2} ms");
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine(")");
                        Console.ResetColor();
                    }
                }
            }
            
            FileLogger.Info($"Categorized ping results for {category} on {_networkCategory} appended to: {filename}");
            
            // Calculate and display median after file is closed
            var median = CalculateMedianFromCsv(filename, "MedianPing(ms)");
            if (median.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write($"Current median ping for {category} on ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{_networkCategory}");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write(": ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"{median.Value:F2} ms");
                Console.ResetColor();
            }
        }
        
        private void WriteJitterResultsCsv(string category, List<PingResult> results)
        {
            if (results.Count == 0) return;
            
            var filename = Path.Combine(_outputDirectory, $"jitter_{category}_{_networkCategory}.csv");
            
            // Check if file needs rotation
            if (File.Exists(filename) && new FileInfo(filename).Length > MaxFileSizeBytes)
            {
                RotateFile(filename);
            }
            
            var fileExists = File.Exists(filename);
            
            // Calculate current median from existing data before writing new values
            double? currentMedian = null;
            if (fileExists)
            {
                currentMedian = CalculateMedianFromCsv(filename, "Jitter(ms)");
            }
            
            using (var writer = new StreamWriter(filename, append: true))
            {
                // Write header only if file doesn't exist
                if (!fileExists)
                {
                    writer.WriteLine("Timestamp,Host,Jitter(ms),Success,NetworkCategory");
                }
                
                // Write data and compare last value to median
                for (int i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    writer.WriteLine($"{result.Timestamp:yyyy-MM-dd HH:mm:ss},{result.Host},{(result.Success ? result.Jitter.ToString("F2") : "FAIL")},{result.Success},{_networkCategory}");
                    
                    // Compare last value to current median
                    if (i == results.Count - 1 && result.Success && currentMedian.HasValue)
                    {
                        var comparison = result.Jitter > currentMedian.Value ? "above" : 
                                       result.Jitter < currentMedian.Value ? "below" : "equal to";
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"Last jitter value for {category} on ");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{_networkCategory}");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($" (");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{result.Jitter:F2} ms");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($") is {comparison} current median (");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{currentMedian.Value:F2} ms");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine(")");
                        Console.ResetColor();
                    }
                }
            }
            
            FileLogger.Info($"Categorized jitter results for {category} on {_networkCategory} appended to: {filename}");
            
            // Calculate and display median after file is closed
            var median = CalculateMedianFromCsv(filename, "Jitter(ms)");
            if (median.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write($"Current median jitter for {category} on ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{_networkCategory}");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write(": ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{median.Value:F2} ms");
                Console.ResetColor();
            }
        }
        
        private string DetermineNetworkCategory(NetworkScanResult scanResult)
        {
            if (scanResult == null) return "unknown";
            
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
                FileLogger.Info($"Rotated categorized ping/jitter files: deleted {Path.GetFileName(secondFile)}, moved {Path.GetFileName(firstFile)} to {Path.GetFileName(secondFile)}");
            }
            else
            {
                File.Move(firstFile, secondFile);
                FileLogger.Info($"Created second categorized ping/jitter file: moved {Path.GetFileName(firstFile)} to {Path.GetFileName(secondFile)}");
            }
        }
        
        private bool IsExternal(string host)
        {
            return host.Contains("google.com") || 
                   host.Contains("cloudflare.com") || 
                   host.Contains("8.8.8.8") ||
                   (!host.Contains(".corp.") && !host.Contains(".rockfin.") && !host.Contains(".mi."));
        }
        
        private bool IsInternalAes(string host)
        {
            return host.Contains("AES") || 
                   host.Contains("aes") ||
                   (host.Contains("RCD") && host.Contains("601"));
        }
        
        private bool IsInternal(string host)
        {
            return host.Contains(".corp.") || 
                   host.Contains(".rockfin.") ||
                   host.Contains(".mi.") ||
                   host.Contains("git.rockfin.com");
        }
        
        private double? CalculateMedianFromCsv(string filename, string valueColumnName)
        {
            try
            {
                if (!File.Exists(filename))
                    return null;
                
                var lines = File.ReadAllLines(filename);
                if (lines.Length < 2) // Header + at least one data row
                    return null;
                
                // Parse header to find the column index
                var headers = lines[0].Split(',');
                var valueColumnIndex = Array.IndexOf(headers, valueColumnName);
                if (valueColumnIndex == -1)
                    return null;
                
                var values = new List<double>();
                
                // Parse data rows
                for (int i = 1; i < lines.Length; i++)
                {
                    var columns = lines[i].Split(',');
                    if (columns.Length > valueColumnIndex)
                    {
                        var valueStr = columns[valueColumnIndex];
                        if (double.TryParse(valueStr, out double value))
                        {
                            values.Add(value);
                        }
                    }
                }
                
                if (values.Count == 0)
                    return null;
                
                // Calculate median
                values.Sort();
                int count = values.Count;
                
                if (count % 2 == 0)
                {
                    return (values[count / 2 - 1] + values[count / 2]) / 2.0;
                }
                else
                {
                    return values[count / 2];
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Error calculating median from categorized CSV {filename}: {ex.Message}");
                return null;
            }
        }
    }
}