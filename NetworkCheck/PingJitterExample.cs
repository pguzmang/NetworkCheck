using System;
using System.Linq;

namespace NetworkScanner
{
    public class PingJitterExample
    {
        public static void DemonstrateUsage()
        {
            // Example 1: Reading latest results
            var reader = new PingJitterResultReader();
            
            Console.WriteLine("=== Reading Latest Ping Results ===");
            
            // Read latest external ping results
            var latestExternalPing = reader.ReadLatestResult("ping", "external");
            if (latestExternalPing != null)
            {
                Console.WriteLine($"Latest External Ping Test at: {latestExternalPing.TestTimestamp}");
                foreach (var result in latestExternalPing.Results)
                {
                    Console.WriteLine($"  {result.Host}: {result.Value:F2} ms (Success: {result.Success})");
                }
            }
            
            // Read latest internal AES jitter results
            var latestAesJitter = reader.ReadLatestResult("jitter", "internalaes");
            if (latestAesJitter != null)
            {
                Console.WriteLine($"\nLatest Internal AES Jitter Test at: {latestAesJitter.TestTimestamp}");
                foreach (var result in latestAesJitter.Results)
                {
                    Console.WriteLine($"  {result.Host}: {result.Value:F2} ms (Success: {result.Success})");
                }
            }
            
            Console.WriteLine("\n=== Getting Host Statistics ===");
            
            // Get statistics for google.com ping
            var googleStats = reader.GetHostStatistics("ping", "external", "google.com");
            if (googleStats != null)
            {
                Console.WriteLine($"Google.com Ping Statistics:");
                Console.WriteLine($"  Average: {googleStats.AverageValue:F2} ms");
                Console.WriteLine($"  Min: {googleStats.MinValue:F2} ms");
                Console.WriteLine($"  Max: {googleStats.MaxValue:F2} ms");
                Console.WriteLine($"  Std Dev: {googleStats.StandardDeviation:F2} ms");
                Console.WriteLine($"  Samples: {googleStats.SampleCount}");
            }
            
            Console.WriteLine("\n=== Reading Results in Date Range ===");
            
            // Read all results from the last 24 hours
            var yesterday = DateTime.Now.AddDays(-1);
            var today = DateTime.Now;
            var recentResults = reader.ReadResultsInDateRange("ping", "internal", yesterday, today);
            
            Console.WriteLine($"Internal ping tests in last 24 hours: {recentResults.Count}");
            foreach (var test in recentResults)
            {
                var avgPing = test.Results.Where(r => r.Success).Average(r => r.Value);
                Console.WriteLine($"  Test at {test.TestTimestamp}: Avg ping = {avgPing:F2} ms");
            }
            
            Console.WriteLine("\n=== Available Categories ===");
            var categories = reader.GetAvailableCategories();
            Console.WriteLine($"Categories found: {string.Join(", ", categories)}");
            
            Console.WriteLine("\n=== Reading CSV Data ===");
            
            // Read CSV data for compatibility
            var csvData = reader.ReadCsvResults("jitter", "external");
            Console.WriteLine($"CSV records found: {csvData.Count}");
            if (csvData.Count > 0)
            {
                Console.WriteLine("First few records:");
                foreach (var record in csvData.Take(3))
                {
                    Console.WriteLine($"  {record["Timestamp"]} - {record["Host"]}: {record["Jitter(ms)"]}");
                }
            }
        }
    }
}