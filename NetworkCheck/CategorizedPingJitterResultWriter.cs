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
                    
                    // Compare last value to current median and show alerts
                    if (i == results.Count - 1 && result.Success && currentMedian.HasValue)
                    {
                        CheckAndDisplayPingAlert(result, currentMedian.Value, category, result.Host);
                    }
                }
            }
            
            FileLogger.Info($"Categorized ping results for {category} on {_networkCategory} appended to: {filename}");
            
            // Calculate and display median with confidence level after file is closed
            var (median, dataPointCount) = CalculateMedianWithCountFromCsv(filename, "MedianPing(ms)");
            if (median.HasValue)
            {
                var confidenceInfo = GetConfidenceLevel(dataPointCount);
                
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write($"Current median ping for {category} on ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{_networkCategory}");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write(": ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{median.Value:F2} ms");
                
                // Display confidence level
                Console.ForegroundColor = confidenceInfo.Color;
                Console.Write($" ({confidenceInfo.Message})");
                Console.ResetColor();
                Console.WriteLine();
                
                // Show data gathering status if needed
                if (dataPointCount < 50)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"   📊 Data points: {dataPointCount} - {confidenceInfo.DetailMessage}");
                    Console.ResetColor();
                }
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
                    
                    // Compare last value to current median and show alerts
                    if (i == results.Count - 1 && result.Success && currentMedian.HasValue)
                    {
                        CheckAndDisplayJitterAlert(result, currentMedian.Value, category, result.Host);
                    }
                }
            }
            
            FileLogger.Info($"Categorized jitter results for {category} on {_networkCategory} appended to: {filename}");
            
            // Calculate and display median with confidence level after file is closed
            var (median, dataPointCount) = CalculateMedianWithCountFromCsv(filename, "Jitter(ms)");
            if (median.HasValue)
            {
                var confidenceInfo = GetConfidenceLevel(dataPointCount);
                
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write($"Current median jitter for {category} on ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{_networkCategory}");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write(": ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"{median.Value:F2} ms");
                
                // Display confidence level
                Console.ForegroundColor = confidenceInfo.Color;
                Console.Write($" ({confidenceInfo.Message})");
                Console.ResetColor();
                Console.WriteLine();
                
                // Show data gathering status if needed
                if (dataPointCount < 50)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"   📊 Data points: {dataPointCount} - {confidenceInfo.DetailMessage}");
                    Console.ResetColor();
                }
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
            var (median, _) = CalculateMedianWithCountFromCsv(filename, valueColumnName);
            return median;
        }
        
        private (double? median, int dataPointCount) CalculateMedianWithCountFromCsv(string filename, string valueColumnName)
        {
            try
            {
                if (!File.Exists(filename))
                    return (null, 0);
                
                var lines = File.ReadAllLines(filename);
                if (lines.Length < 2) // Header + at least one data row
                    return (null, 0);
                
                // Parse header to find the column index
                var headers = lines[0].Split(',');
                var valueColumnIndex = Array.IndexOf(headers, valueColumnName);
                if (valueColumnIndex == -1)
                    return (null, 0);
                
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
                    return (null, 0);
                
                // Calculate median
                values.Sort();
                int count = values.Count;
                
                double median;
                if (count % 2 == 0)
                {
                    median = (values[count / 2 - 1] + values[count / 2]) / 2.0;
                }
                else
                {
                    median = values[count / 2];
                }
                
                return (median, count);
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Error calculating median from categorized CSV {filename}: {ex.Message}");
                return (null, 0);
            }
        }
        
        private (ConsoleColor Color, string Message, string DetailMessage) GetConfidenceLevel(int dataPointCount)
        {
            return dataPointCount switch
            {
                < 10 => (ConsoleColor.Red, "Insufficient Data", "Still gathering baseline data, measurements not reliable yet"),
                >= 10 and < 20 => (ConsoleColor.Yellow, "Preliminary", "Basic median available but may fluctuate significantly"),
                >= 20 and < 50 => (ConsoleColor.Cyan, "Reliable", "Good baseline established for current network conditions"),
                >= 50 => (ConsoleColor.Green, "High Confidence", "Excellent baseline with comprehensive data")
            };
        }
        
        private void CheckAndDisplayPingAlert(PingResult result, double median, string category, string host)
        {
            var (alertLevel, message) = GetPingAlertLevel(result.MedianPing, median, category, host);
            
            if (alertLevel == AlertLevel.None)
            {
                // Show normal comparison without alert
                var comparison = result.MedianPing > median ? "above" : 
                               result.MedianPing < median ? "below" : "equal to";
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"   Last ping: {result.MedianPing:F2} ms ({comparison} median {median:F2} ms)");
                Console.ResetColor();
            }
            else
            {
                // Show alert
                var alertColor = alertLevel == AlertLevel.Warning ? ConsoleColor.Yellow : ConsoleColor.Red;
                var alertSymbol = alertLevel == AlertLevel.Warning ? "🟡 WARNING" : "🔴 CRITICAL";
                
                Console.ForegroundColor = alertColor;
                Console.Write($"{alertSymbol}: ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{category} ping to {host} is ");
                Console.ForegroundColor = alertColor;
                Console.Write($"{result.MedianPing:F2} ms");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($" (median: {median:F2} ms) - ");
                Console.ForegroundColor = alertColor;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }
        
        private void CheckAndDisplayJitterAlert(PingResult result, double median, string category, string host)
        {
            var (alertLevel, message) = GetJitterAlertLevel(result.Jitter, median, category, host);
            
            if (alertLevel == AlertLevel.None)
            {
                // Show normal comparison without alert
                var comparison = result.Jitter > median ? "above" : 
                               result.Jitter < median ? "below" : "equal to";
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"   Last jitter: {result.Jitter:F2} ms ({comparison} median {median:F2} ms)");
                Console.ResetColor();
            }
            else
            {
                // Show alert
                var alertColor = alertLevel == AlertLevel.Warning ? ConsoleColor.Yellow : ConsoleColor.Red;
                var alertSymbol = alertLevel == AlertLevel.Warning ? "🟡 WARNING" : "🔴 CRITICAL";
                
                Console.ForegroundColor = alertColor;
                Console.Write($"{alertSymbol}: ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{category} jitter to {host} is ");
                Console.ForegroundColor = alertColor;
                Console.Write($"{result.Jitter:F2} ms");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($" (median: {median:F2} ms) - ");
                Console.ForegroundColor = alertColor;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }
        
        private (AlertLevel level, string message) GetPingAlertLevel(double currentPing, double median, string category, string host)
        {
            // Determine server type for appropriate thresholds
            bool isExternal = IsExternal(host);
            bool isAes = IsInternalAes(host);
            bool isVpn = _networkCategory.Contains("home") || _networkCategory.Contains("vpn");
            
            double warningMultiplier, criticalMultiplier;
            double warningAbsolute, criticalAbsolute;
            
            if (isAes)
            {
                // AES servers - strictest thresholds
                warningMultiplier = 1.2;
                criticalMultiplier = 1.5;
                warningAbsolute = median + 2;
                criticalAbsolute = median + 5;
            }
            else if (isExternal)
            {
                // External servers - more lenient due to internet variability
                warningMultiplier = 1.5;
                criticalMultiplier = 2.5;
                warningAbsolute = median + 15;
                criticalAbsolute = median + 30;
            }
            else
            {
                // Internal servers - moderate thresholds
                warningMultiplier = 1.3;
                criticalMultiplier = 2.0;
                warningAbsolute = median + 5;
                criticalAbsolute = median + 10;
            }
            
            // Adjust for VPN connections (add tolerance)
            if (isVpn)
            {
                warningAbsolute += 10;
                criticalAbsolute += 20;
            }
            
            // Check thresholds (use whichever is higher)
            double warningThreshold = Math.Max(median * warningMultiplier, warningAbsolute);
            double criticalThreshold = Math.Max(median * criticalMultiplier, criticalAbsolute);
            
            if (currentPing >= criticalThreshold)
            {
                double multiplier = currentPing / median;
                return (AlertLevel.Critical, $"{multiplier:F1}x median, significant performance impact");
            }
            else if (currentPing >= warningThreshold)
            {
                double multiplier = currentPing / median;
                return (AlertLevel.Warning, $"{multiplier:F1}x median, performance degraded");
            }
            
            return (AlertLevel.None, "");
        }
        
        private (AlertLevel level, string message) GetJitterAlertLevel(double currentJitter, double median, string category, string host)
        {
            // Jitter thresholds - generally more lenient as it's naturally more variable
            double warningMultiplier = 2.0;
            double criticalMultiplier = 3.0;
            double warningAbsolute = median + 5;
            double criticalAbsolute = median + 10;
            
            // Check thresholds (use whichever is higher)
            double warningThreshold = Math.Max(median * warningMultiplier, warningAbsolute);
            double criticalThreshold = Math.Max(median * criticalMultiplier, criticalAbsolute);
            
            if (currentJitter >= criticalThreshold)
            {
                double multiplier = currentJitter / median;
                return (AlertLevel.Critical, $"{multiplier:F1}x median, network instability detected");
            }
            else if (currentJitter >= warningThreshold)
            {
                double multiplier = currentJitter / median;
                return (AlertLevel.Warning, $"{multiplier:F1}x median, increased network variability");
            }
            
            return (AlertLevel.None, "");
        }
        
        private enum AlertLevel
        {
            None,
            Warning,
            Critical
        }
    }
}