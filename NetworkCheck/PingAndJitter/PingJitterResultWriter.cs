using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace NetworkScanner
{
    public class PingJitterResultWriter
    {
        private readonly string _outputDirectory;
        private readonly JsonSerializerOptions _jsonOptions;
        
        public PingJitterResultWriter(string outputDirectory = "NetworkTestResults")
        {
            _outputDirectory = outputDirectory;
            Directory.CreateDirectory(_outputDirectory);
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
        }
        
        public void WriteResults(List<PingResult> results)
        {
            var timestamp = DateTime.Now;
            var timestampStr = timestamp.ToString("yyyyMMdd_HHmmss");
            
            // Categorize results
            var external = results.Where(r => IsExternal(r.Host)).ToList();
            var internalAes = results.Where(r => IsInternalAes(r.Host)).ToList();
            var internalOther = results.Where(r => IsInternal(r.Host) && !IsInternalAes(r.Host)).ToList();
            
            // Write ping results
            WritePingResultsJson("external", external, timestamp, timestampStr);
            WritePingResultsJson("internalaes", internalAes, timestamp, timestampStr);
            WritePingResultsJson("internal", internalOther, timestamp, timestampStr);
            
            // Write jitter results
            WriteJitterResultsJson("external", external, timestamp, timestampStr);
            WriteJitterResultsJson("internalaes", internalAes, timestamp, timestampStr);
            WriteJitterResultsJson("internal", internalOther, timestamp, timestampStr);
            
            // Also write CSV format for easier import into Excel/other tools
            WritePingResultsCsv("external", external, timestampStr);
            WritePingResultsCsv("internalaes", internalAes, timestampStr);
            WritePingResultsCsv("internal", internalOther, timestampStr);
            
            WriteJitterResultsCsv("external", external, timestampStr);
            WriteJitterResultsCsv("internalaes", internalAes, timestampStr);
            WriteJitterResultsCsv("internal", internalOther, timestampStr);
        }
        
        private void WritePingResultsJson(string category, List<PingResult> results, DateTime testTime, string timestampStr)
        {
            if (results.Count == 0) return;
            
            var filename = Path.Combine(_outputDirectory, $"ping_{category}_{timestampStr}.json");
            
            var fileData = new PingJitterFileData
            {
                Category = category,
                TestType = "ping",
                TestTimestamp = testTime,
                Results = results.Select(r => new TestResult
                {
                    Host = r.Host,
                    Value = r.MedianPing,
                    Success = r.Success,
                    Timestamp = r.Timestamp
                }).ToList()
            };
            
            var json = JsonSerializer.Serialize(fileData, _jsonOptions);
            File.WriteAllText(filename, json);
            FileLogger.Info($"Ping results for {category} written to: {filename}");
        }
        
        private void WriteJitterResultsJson(string category, List<PingResult> results, DateTime testTime, string timestampStr)
        {
            if (results.Count == 0) return;
            
            var filename = Path.Combine(_outputDirectory, $"jitter_{category}_{timestampStr}.json");
            
            var fileData = new PingJitterFileData
            {
                Category = category,
                TestType = "jitter",
                TestTimestamp = testTime,
                Results = results.Select(r => new TestResult
                {
                    Host = r.Host,
                    Value = r.Jitter,
                    Success = r.Success,
                    Timestamp = r.Timestamp
                }).ToList()
            };
            
            var json = JsonSerializer.Serialize(fileData, _jsonOptions);
            File.WriteAllText(filename, json);
            FileLogger.Info($"Jitter results for {category} written to: {filename}");
        }
        
        private void WritePingResultsCsv(string category, List<PingResult> results, string timestampStr)
        {
            if (results.Count == 0) return;
            
            var filename = Path.Combine(_outputDirectory, $"ping_{category}_{timestampStr}.csv");
            var csv = new StringBuilder();
            
            // Header
            csv.AppendLine("Timestamp,Host,MedianPing(ms),Success");
            
            // Data
            foreach (var result in results)
            {
                csv.AppendLine($"{result.Timestamp:yyyy-MM-dd HH:mm:ss},{result.Host},{(result.Success ? result.MedianPing.ToString("F2") : "FAIL")},{result.Success}");
            }
            
            File.WriteAllText(filename, csv.ToString());
            FileLogger.Info($"Ping CSV for {category} written to: {filename}");
        }
        
        private void WriteJitterResultsCsv(string category, List<PingResult> results, string timestampStr)
        {
            if (results.Count == 0) return;
            
            var filename = Path.Combine(_outputDirectory, $"jitter_{category}_{timestampStr}.csv");
            var csv = new StringBuilder();
            
            // Header
            csv.AppendLine("Timestamp,Host,Jitter(ms),Success");
            
            // Data
            foreach (var result in results)
            {
                csv.AppendLine($"{result.Timestamp:yyyy-MM-dd HH:mm:ss},{result.Host},{(result.Success ? result.Jitter.ToString("F2") : "FAIL")},{result.Success}");
            }
            
            File.WriteAllText(filename, csv.ToString());
            FileLogger.Info($"Jitter CSV for {category} written to: {filename}");
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
    }
}