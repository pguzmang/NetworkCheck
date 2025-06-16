using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NetworkScanner
{
    public class PingJitterResultReader
    {
        private readonly string _resultsDirectory;
        
        public PingJitterResultReader(string resultsDirectory = "NetworkTestResults")
        {
            _resultsDirectory = resultsDirectory;
        }
        
        // Read all JSON files for a specific category and test type
        public List<PingJitterFileData> ReadResults(string testType, string category)
        {
            var results = new List<PingJitterFileData>();
            
            if (!Directory.Exists(_resultsDirectory))
            {
                FileLogger.Warn($"Results directory does not exist: {_resultsDirectory}");
                return results;
            }
            
            var pattern = $"{testType}_{category}_*.json";
            var files = Directory.GetFiles(_resultsDirectory, pattern);
            
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var data = JsonSerializer.Deserialize<PingJitterFileData>(json);
                    if (data != null)
                    {
                        results.Add(data);
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.Error($"Error reading file {file}: {ex.Message}");
                }
            }
            
            return results.OrderBy(r => r.TestTimestamp).ToList();
        }
        
        // Read the most recent result for a specific category and test type
        public PingJitterFileData? ReadLatestResult(string testType, string category)
        {
            var allResults = ReadResults(testType, category);
            return allResults.LastOrDefault();
        }
        
        // Read all results within a date range
        public List<PingJitterFileData> ReadResultsInDateRange(string testType, string category, DateTime start, DateTime end)
        {
            var allResults = ReadResults(testType, category);
            return allResults.Where(r => r.TestTimestamp >= start && r.TestTimestamp <= end).ToList();
        }
        
        // Get average values for a specific host across multiple tests
        public HostStatistics? GetHostStatistics(string testType, string category, string host)
        {
            var allResults = ReadResults(testType, category);
            var hostResults = new List<double>();
            
            foreach (var fileData in allResults)
            {
                var hostResult = fileData.Results.FirstOrDefault(r => r.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
                if (hostResult != null && hostResult.Success)
                {
                    hostResults.Add(hostResult.Value);
                }
            }
            
            if (hostResults.Count == 0)
                return null;
            
            return new HostStatistics
            {
                Host = host,
                TestType = testType,
                Category = category,
                AverageValue = hostResults.Average(),
                MinValue = hostResults.Min(),
                MaxValue = hostResults.Max(),
                SampleCount = hostResults.Count,
                StandardDeviation = CalculateStandardDeviation(hostResults)
            };
        }
        
        // Read CSV files for compatibility
        public List<Dictionary<string, string>> ReadCsvResults(string testType, string category)
        {
            var results = new List<Dictionary<string, string>>();
            
            if (!Directory.Exists(_resultsDirectory))
                return results;
            
            var pattern = $"{testType}_{category}_*.csv";
            var files = Directory.GetFiles(_resultsDirectory, pattern);
            
            foreach (var file in files)
            {
                try
                {
                    var lines = File.ReadAllLines(file);
                    if (lines.Length < 2) continue;
                    
                    var headers = lines[0].Split(',');
                    
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var values = lines[i].Split(',');
                        var row = new Dictionary<string, string>();
                        
                        for (int j = 0; j < headers.Length && j < values.Length; j++)
                        {
                            row[headers[j]] = values[j];
                        }
                        
                        results.Add(row);
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.Error($"Error reading CSV file {file}: {ex.Message}");
                }
            }
            
            return results;
        }
        
        // Get all available test categories
        public List<string> GetAvailableCategories()
        {
            var categories = new HashSet<string>();
            
            if (!Directory.Exists(_resultsDirectory))
                return new List<string>();
            
            var files = Directory.GetFiles(_resultsDirectory, "*.json");
            
            foreach (var file in files)
            {
                var filename = Path.GetFileNameWithoutExtension(file);
                var parts = filename.Split('_');
                if (parts.Length >= 2)
                {
                    categories.Add(parts[1]); // Category is the second part
                }
            }
            
            return categories.ToList();
        }
        
        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count <= 1)
                return 0;
            
            var average = values.Average();
            var sumOfSquares = values.Sum(v => Math.Pow(v - average, 2));
            return Math.Sqrt(sumOfSquares / (values.Count - 1));
        }
    }
    
    public class HostStatistics
    {
        public string Host { get; set; } = string.Empty;
        public string TestType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double AverageValue { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public int SampleCount { get; set; }
        public double StandardDeviation { get; set; }
    }
}