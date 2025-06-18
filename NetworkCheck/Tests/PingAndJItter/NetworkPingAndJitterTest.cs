using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NetEscapades.Configuration.Yaml;
using System.IO;

namespace NetworkScanner
{
    public class NetworkPingAndJitterTest
    {
        private static NetworkCheck.NetworkPingSettings _settings = null!;
        
        static NetworkPingAndJitterTest()
        {
            LoadConfiguration();
        }
        
        private static void LoadConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddYamlFile("Config/appsettings.yml", optional: false, reloadOnChange: true)
                .Build();
                
            _settings = new NetworkCheck.NetworkPingSettings();
            configuration.GetSection("NetworkPingAndJitterTest").Bind(_settings);
            
            // Add colored console output for configuration loading
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("📋 Configuration loaded from YAML file");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"   • External servers: {_settings.Servers.External?.Count ?? 0}");
            Console.WriteLine($"   • Internal servers: {_settings.Servers.Internal?.Count ?? 0}");
            Console.WriteLine($"   • AES servers: {_settings.Servers.AesServers?.Count ?? 0}");
            Console.WriteLine($"   • Ping count: {_settings.PingSettings.PingCount}");
            Console.WriteLine($"   • Timeout: {_settings.PingSettings.TimeoutMilliseconds}ms");
            Console.ResetColor();
        }

        public static List<PingResult> RunAllTests()
        {
            return RunAllTests(null);
        }
        
        public static List<PingResult> RunAllTests(NetworkScanResult? networkScanResult)
        {
            var results = new List<PingResult>();
            
            // External tests
            if (_settings.Servers.External?.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n🌍 Testing External Servers ({_settings.Servers.External.Count})");
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine(new string('─', 50));
                Console.ResetColor();
                
                foreach (var server in _settings.Servers.External)
                {
                    results.Add(TestPingAndJitter(server));
                }
            }
            
            // Internal AES tests
            if (_settings.Servers.AesServers?.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"\n🔒 Testing AES Servers ({_settings.Servers.AesServers.Count})");
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine(new string('─', 50));
                Console.ResetColor();
                
                foreach (var server in _settings.Servers.AesServers)
                {
                    results.Add(TestPingAndJitter(server));
                }
            }
            
            // Internal tests
            if (_settings.Servers.Internal?.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n🏢 Testing Internal Servers ({_settings.Servers.Internal.Count})");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine(new string('─', 50));
                Console.ResetColor();
                
                foreach (var server in _settings.Servers.Internal)
                {
                    results.Add(TestPingAndJitter(server));
                }
            }
            
            // Write results to categorized files if network scan result is available
            if (networkScanResult != null)
            {
                var categorizedWriter = new CategorizedPingJitterResultWriter(networkScanResult);
                categorizedWriter.WriteResults(results);
            }
            else
            {
                // Fallback to original writer if no network scan result available
                var writer = new PingJitterResultWriter();
                writer.WriteResults(results);
            }
            
            return results;
        }

        private static void ExternalTest()
        {
            foreach (var server in _settings.Servers.External)
            {
                TestPingAndJitter(server);
            }
        }

        private static void InternalAesTest()
        {
            foreach (var server in _settings.Servers.AesServers)
            {
                TestPingAndJitter(server);
            }
        }

        private static void InternalTest()
        {
            foreach (var server in _settings.Servers.Internal)
            {
                TestPingAndJitter(server);
            }
        }

        public static PingResult TestPingAndJitter(string host)
        {
            // Display test header with colors
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"\n📊 Testing: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(host);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($" (sending {_settings.PingSettings.PingCount} pings)");
            Console.ResetColor();
            
            int pingCount = _settings.PingSettings.PingCount; // Number of pings to send
            List<long> pingTimes = new List<long>();
            long pingTime;
            StringBuilder logMessages = new StringBuilder();
            
            // Display progress bar setup
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("   Progress: [");

            for (int i = 0; i < pingCount; i++)
            {
                try
                {
                    pingTime = Ping(host);
                    if (pingTime != -1)
                    {
                        pingTimes.Add(pingTime);
                        logMessages.AppendLine($"Ping {i + 1}: {pingTime} ms to {host}");
                        
                        // Color-coded progress indicator
                        if (pingTime < 50)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write("█"); // Green for fast pings
                        }
                        else if (pingTime < 100)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write("█"); // Yellow for medium pings
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write("█"); // Red for slow pings
                        }
                    }
                    else
                    {
                        logMessages.AppendLine($"Ping {i + 1}: Request timed out to {host}");
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.Write("░"); // Dark pattern for timeouts
                    }
                    Thread.Sleep(_settings.PingSettings.DelayBetweenPingsMilliseconds); // Wait between pings
                }
                catch (Exception e)
                {
                    logMessages.AppendLine($"Error during ping to {host}: {e.Message}");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("X"); // X for errors
                }
            }

            // Close progress bar
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("]");
            Console.ResetColor();
            
            PingResult result = new PingResult
            {
                Host = host,
                Timestamp = DateTime.Now,
                Success = pingTimes.Count > 0
            };

            if (pingTimes.Count > 0)
            {
                double averagePing = pingTimes.Average();
                double medianPing = CalculateMedian(pingTimes);
                double jitter = CalculateJitter(pingTimes);
                
                result.MedianPing = medianPing;
                result.Jitter = jitter;
                
                // Display colored statistics
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("   ✓ Results: ");
                
                // Average ping with color coding
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("Avg: ");
                if (averagePing < 50)
                    Console.ForegroundColor = ConsoleColor.Green;
                else if (averagePing < 100)
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else
                    Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"{averagePing:F1}ms ");
                
                // Median ping
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("| Med: ");
                if (medianPing < 50)
                    Console.ForegroundColor = ConsoleColor.Green;
                else if (medianPing < 100)
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else
                    Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"{medianPing:F1}ms ");
                
                // Jitter
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("| Jitter: ");
                if (jitter < 10)
                    Console.ForegroundColor = ConsoleColor.Green;
                else if (jitter < 25)
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else
                    Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"{jitter:F1}ms ");
                
                // Success rate
                double successRate = (double)pingTimes.Count / pingCount * 100;
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("| Success: ");
                if (successRate == 100)
                    Console.ForegroundColor = ConsoleColor.Green;
                else if (successRate >= 80)
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else
                    Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{successRate:F0}%");
                Console.ResetColor();

                logMessages.AppendLine($"Average Ping: {averagePing:F2} ms to {host}");
                logMessages.AppendLine($"Median Ping: {medianPing:F2} ms to {host}");
                logMessages.AppendLine($"Jitter: {jitter:F2} ms to {host}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"   ✗ FAILED: No successful pings to {host}");
                Console.ResetColor();
                
                logMessages.AppendLine($"No successful pings to {host}");
                result.MedianPing = -1;
                result.Jitter = -1;
            }
            
            FileLogger.Debug(logMessages.ToString());
            return result;
        }

        private static long Ping(string host)
        {
            try
            {
                using (Ping pingSender = new Ping())
                {
                    PingReply reply = pingSender.Send(host, _settings.PingSettings.TimeoutMilliseconds); // Configurable timeout

                    if (reply.Status == IPStatus.Success)
                    {
                        return reply.RoundtripTime;
                    }
                    else
                    {
                        return -1;
                    }
                }
            }
            catch (Exception e)
            {
                FileLogger.Warn($"Ping error for {host}: {e.Message}");
                return -1;
            }
        }

        private static double CalculateMedian(List<long> pingTimes)
        {
            var sortedPings = pingTimes.OrderBy(x => x).ToList();
            int count = sortedPings.Count;
            
            if (count % 2 == 0)
            {
                // Even number of elements - return average of two middle elements
                return (sortedPings[count / 2 - 1] + sortedPings[count / 2]) / 2.0;
            }
            else
            {
                // Odd number of elements - return the middle element
                return sortedPings[count / 2];
            }
        }

        private static double CalculateJitter(List<long> pingTimes)
        {
            double averagePing = pingTimes.Average();
            double sumOfSquares = pingTimes.Sum(ping => Math.Pow(ping - averagePing, 2));
            return Math.Sqrt(sumOfSquares / pingTimes.Count);
        }

        public static void CheckAllNetworks()
        {
            ConnectivityCheck.CheckInternetConnection();
            ExternalTest();
            InternalAesTest();
            InternalTest();
        }

        public static void CreateNewThreadToTestNetwork()
        {
            Thread thread = new Thread(new ThreadStart(MyRunnable.Run));
            thread.Start();
        }

        private class MyRunnable
        {
            public static void Run()
            {
                CheckAllNetworks();
            }
        }
    }
}