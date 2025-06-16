using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkScanner
{
    public class NetworkPingAndJitterTest
    {
        // Since we don't have SoftPhoneConfig.aesServerSet, we'll use a static list
        private static readonly List<string> AesServerSet = new List<string>
        {
            "RCD2AES601.mi.corp.rockfin.com",
            "RCD1AES601.mi.corp.rockfin.com"
        };

        public static List<PingResult> RunAllTests()
        {
            var results = new List<PingResult>();
            
            // External test
            results.Add(TestPingAndJitter("google.com"));
            
            // Internal AES tests
            foreach (var server in AesServerSet)
            {
                results.Add(TestPingAndJitter(server));
            }
            
            // Internal test
            results.Add(TestPingAndJitter("git.rockfin.com"));
            
            return results;
        }

        private static void ExternalTest()
        {
            TestPingAndJitter("google.com");
        }

        private static void InternalAesTest()
        {
            foreach (var server in AesServerSet)
            {
                TestPingAndJitter(server);
            }
        }

        private static void InternalTest()
        {
            TestPingAndJitter("git.rockfin.com");
        }

        public static PingResult TestPingAndJitter(string host)
        {
            int pingCount = 10; // Number of pings to send
            List<long> pingTimes = new List<long>();
            long pingTime;
            StringBuilder logMessages = new StringBuilder();

            for (int i = 0; i < pingCount; i++)
            {
                try
                {
                    pingTime = Ping(host);
                    if (pingTime != -1)
                    {
                        pingTimes.Add(pingTime);
                        logMessages.AppendLine($"Ping {i + 1}: {pingTime} ms to {host}");
                    }
                    else
                    {
                        logMessages.AppendLine($"Ping {i + 1}: Request timed out to {host}");
                    }
                    Thread.Sleep(1000); // Wait 1 second between pings
                }
                catch (Exception e)
                {
                    logMessages.AppendLine($"Error during ping to {host}: {e.Message}");
                }
            }

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

                logMessages.AppendLine($"Average Ping: {averagePing:F2} ms to {host}");
                logMessages.AppendLine($"Median Ping: {medianPing:F2} ms to {host}");
                logMessages.AppendLine($"Jitter: {jitter:F2} ms to {host}");
            }
            else
            {
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
                    PingReply reply = pingSender.Send(host, 5000); // 5 second timeout

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