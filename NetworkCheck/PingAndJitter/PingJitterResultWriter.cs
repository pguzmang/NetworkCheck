using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NetworkScanner
{
    public class PingJitterResultWriter
    {
        private readonly string _outputDirectory;
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
        
        public PingJitterResultWriter(string outputDirectory = "NetworkTestResults")
        {
            _outputDirectory = outputDirectory;
            Directory.CreateDirectory(_outputDirectory);
        }
        
        public void WriteResults(List<PingResult> results)
        {
            // Categorize results
            var external = results.Where(r => IsExternal(r.Host)).ToList();
            var internalAes = results.Where(r => IsInternalAes(r.Host)).ToList();
            var internalOther = results.Where(r => IsInternal(r.Host) && !IsInternalAes(r.Host)).ToList();
            
            // Write CSV files
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
            
            var filename = Path.Combine(_outputDirectory, $"ping_{category}.csv");
            
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
                    writer.WriteLine("Timestamp,Host,MedianPing(ms),Success");
                }
                
                // Write data and compare last value to median
                for (int i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    writer.WriteLine($"{result.Timestamp:yyyy-MM-dd HH:mm:ss},{result.Host},{(result.Success ? result.MedianPing.ToString("F2") : "FAIL")},{result.Success}");
                    
                    // Compare last value to current median
                    if (i == results.Count - 1 && result.Success && currentMedian.HasValue)
                    {
                        var comparison = result.MedianPing > currentMedian.Value ? "above" : 
                                       result.MedianPing < currentMedian.Value ? "below" : "equal to";
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write($"Last ping value for {category} (");
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
            
            FileLogger.Info($"Ping results for {category} appended to: {filename}");
            
            // Calculate and display median after file is closed
            var median = CalculateMedianFromCsv(filename, "MedianPing(ms)");
            if (median.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write($"Current median ping for {category}: ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"{median.Value:F2} ms");
                Console.ResetColor();
            }
        }
        
        private void WriteJitterResultsCsv(string category, List<PingResult> results)
        {
            if (results.Count == 0) return;
            
            var filename = Path.Combine(_outputDirectory, $"jitter_{category}.csv");
            
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
                    writer.WriteLine("Timestamp,Host,Jitter(ms),Success");
                }
                
                // Write data and compare last value to median
                for (int i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    writer.WriteLine($"{result.Timestamp:yyyy-MM-dd HH:mm:ss},{result.Host},{(result.Success ? result.Jitter.ToString("F2") : "FAIL")},{result.Success}");
                    
                    // Compare last value to current median
                    if (i == results.Count - 1 && result.Success && currentMedian.HasValue)
                    {
                        var comparison = result.Jitter > currentMedian.Value ? "above" : 
                                       result.Jitter < currentMedian.Value ? "below" : "equal to";
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"Last jitter value for {category} (");
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
            
            FileLogger.Info($"Jitter results for {category} appended to: {filename}");
            
            // Calculate and display median after file is closed
            var median = CalculateMedianFromCsv(filename, "Jitter(ms)");
            if (median.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write($"Current median jitter for {category}: ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{median.Value:F2} ms");
                Console.ResetColor();
            }
        }
        
        private void RotateFile(string filename)
        {
            var directory = Path.GetDirectoryName(filename) ?? _outputDirectory;
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            var extension = Path.GetExtension(filename);
            
            var firstFile = filename; // e.g., ping_external.csv
            var secondFile = Path.Combine(directory, $"{nameWithoutExtension}_2{extension}"); // e.g., ping_external_2.csv
            
            if (File.Exists(secondFile))
            {
                // Both files exist and first is full
                // Delete the second file and move first to second
                File.Delete(secondFile);
                File.Move(firstFile, secondFile);
                FileLogger.Info($"Rotated files: deleted {Path.GetFileName(secondFile)}, moved {Path.GetFileName(firstFile)} to {Path.GetFileName(secondFile)}");
            }
            else
            {
                // Only first file exists and is full
                // Move first to second
                File.Move(firstFile, secondFile);
                FileLogger.Info($"Created second file: moved {Path.GetFileName(firstFile)} to {Path.GetFileName(secondFile)}");
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
                    // Even number of values - average of two middle values
                    return (values[count / 2 - 1] + values[count / 2]) / 2.0;
                }
                else
                {
                    // Odd number of values - middle value
                    return values[count / 2];
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Error calculating median from CSV {filename}: {ex.Message}");
                return null;
            }
        }
    }
}