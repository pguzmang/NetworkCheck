using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace NetworkScanner
{
    /// <summary>
    /// Provides functionality for performing ping and jitter tests against external network hosts.
    /// This class is designed to test connectivity to public internet hosts.
    /// </summary>
    public static class ExternalPingJitter
    {
        // Default external hosts for testing
        private static readonly string[] DefaultExternalHosts = 
        {
            "8.8.8.8",        // Google DNS
            "1.1.1.1",        // Cloudflare DNS
            "4.2.2.2",        // Level3 DNS
            "208.67.222.222"  // OpenDNS
        };

        private const int DefaultPingCount = 10;
        private const int DefaultTimeout = 5000; // 5 seconds

        /// <summary>
        /// Result class specific to external ping/jitter testing.
        /// </summary>
        public class ExternalPingJitterResult
        {
            public string TargetHost { get; set; } = string.Empty;
            public bool IsReachable { get; set; }
            public double AveragePingMs { get; set; }
            public double MinPingMs { get; set; }
            public double MaxPingMs { get; set; }
            public double JitterMs { get; set; }
            public double PacketLossPercent { get; set; }
            public int SuccessfulPings { get; set; }
            public int TotalPings { get; set; }
            public DateTime TestTimestamp { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public List<double> RoundTripTimes { get; set; } = new List<double>();
        }

        /// <summary>
        /// Performs ping and jitter testing against a specific external host.
        /// </summary>
        /// <param name="hostAddress">The host address to ping (IP or hostname)</param>
        /// <param name="pingCount">Number of ping attempts (default: 10)</param>
        /// <param name="timeoutMs">Timeout for each ping in milliseconds (default: 5000)</param>
        /// <returns>An ExternalPingJitterResult containing the test results</returns>
        public static async Task<ExternalPingJitterResult> TestExternalHost(string hostAddress, int pingCount = DefaultPingCount, int timeoutMs = DefaultTimeout)
        {
            var result = new ExternalPingJitterResult
            {
                TargetHost = hostAddress,
                TestTimestamp = DateTime.UtcNow,
                TotalPings = pingCount
            };

            try
            {
                FileLogger.Debug($"Starting external ping test to {hostAddress} with {pingCount} pings");
                
                using (var ping = new Ping())
                {
                    var roundTripTimes = new List<double>();
                    int successfulPings = 0;

                    for (int i = 0; i < pingCount; i++)
                    {
                        try
                        {
                            PingReply reply = await ping.SendPingAsync(hostAddress, timeoutMs);
                            
                            if (reply.Status == IPStatus.Success)
                            {
                                successfulPings++;
                                roundTripTimes.Add(reply.RoundtripTime);
                                FileLogger.Debug($"Ping {i + 1} to {hostAddress}: {reply.RoundtripTime}ms");
                            }
                            else
                            {
                                FileLogger.Debug($"Ping {i + 1} to {hostAddress} failed: {reply.Status}");
                            }

                            // Small delay between pings
                            await Task.Delay(100);
                        }
                        catch (Exception ex)
                        {
                            FileLogger.Debug($"Exception during ping {i + 1} to {hostAddress}: {ex.Message}");
                        }
                    }

                    result.SuccessfulPings = successfulPings;
                    result.RoundTripTimes = roundTripTimes;

                    if (roundTripTimes.Count > 0)
                    {
                        result.IsReachable = true;
                        result.AveragePingMs = Math.Round(roundTripTimes.Average(), 2);
                        result.MinPingMs = roundTripTimes.Min();
                        result.MaxPingMs = roundTripTimes.Max();
                        result.PacketLossPercent = Math.Round(((double)(pingCount - successfulPings) / pingCount) * 100, 2);
                        
                        // Calculate jitter (standard deviation)
                        result.JitterMs = CalculateJitter(roundTripTimes);
                    }
                    else
                    {
                        result.IsReachable = false;
                        result.PacketLossPercent = 100;
                        result.ErrorMessage = "No successful pings";
                    }
                }

                FileLogger.Info($"External ping test to {hostAddress} completed. Avg: {result.AveragePingMs}ms, Jitter: {result.JitterMs}ms, Loss: {result.PacketLossPercent}%");
            }
            catch (Exception ex)
            {
                result.IsReachable = false;
                result.ErrorMessage = ex.Message;
                FileLogger.Warn($"External ping test to {hostAddress} failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Tests connectivity to all default external hosts.
        /// </summary>
        /// <returns>A dictionary of host addresses to their test results</returns>
        public static async Task<Dictionary<string, ExternalPingJitterResult>> TestAllDefaultHosts()
        {
            var results = new Dictionary<string, ExternalPingJitterResult>();
            var tasks = new List<Task<ExternalPingJitterResult>>();

            FileLogger.Debug("Starting external ping tests to all default hosts");

            foreach (var host in DefaultExternalHosts)
            {
                tasks.Add(TestExternalHost(host));
            }

            var completedResults = await Task.WhenAll(tasks);

            for (int i = 0; i < DefaultExternalHosts.Length; i++)
            {
                results[DefaultExternalHosts[i]] = completedResults[i];
            }

            FileLogger.Info($"Completed external ping tests to {results.Count} hosts");
            return results;
        }

        /// <summary>
        /// Calculates jitter (standard deviation) from a list of round trip times.
        /// </summary>
        /// <param name="roundTripTimes">List of RTT values in milliseconds</param>
        /// <returns>Jitter value in milliseconds</returns>
        private static double CalculateJitter(List<double> roundTripTimes)
        {
            if (roundTripTimes.Count < 2)
                return 0;

            double average = roundTripTimes.Average();
            double sumOfSquares = roundTripTimes.Sum(rtt => Math.Pow(rtt - average, 2));
            double variance = sumOfSquares / (roundTripTimes.Count - 1);
            double standardDeviation = Math.Sqrt(variance);

            return Math.Round(standardDeviation, 2);
        }

        /// <summary>
        /// Gets a summary of external network connectivity.
        /// </summary>
        /// <returns>A string summary of external connectivity status</returns>
        public static async Task<string> GetExternalConnectivitySummary()
        {
            var results = await TestAllDefaultHosts();
            var reachableHosts = results.Values.Count(r => r.IsReachable);
            
            if (reachableHosts == 0)
            {
                return "No external connectivity detected";
            }
            else if (reachableHosts < results.Count)
            {
                return $"Partial external connectivity ({reachableHosts}/{results.Count} hosts reachable)";
            }
            else
            {
                var avgPing = results.Values.Average(r => r.AveragePingMs);
                var avgJitter = results.Values.Average(r => r.JitterMs);
                return $"Full external connectivity (Avg ping: {avgPing:F2}ms, Avg jitter: {avgJitter:F2}ms)";
            }
        }
    }
}