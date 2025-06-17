using System;
using System.Net.NetworkInformation;

namespace NetworkScanner
{
    public class ConnectivityCheck
    {
        public static void CheckInternetConnection()
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    // Try to ping a reliable server
                    PingReply reply = ping.Send("8.8.8.8", 5000); // Google DNS
                    
                    if (reply.Status == IPStatus.Success)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("✓ Internet Connection: ");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write("Available ");
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine($"({reply.RoundtripTime}ms)");
                        Console.ResetColor();
                        FileLogger.Info("Internet connection is available");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("✗ Internet Connection: ");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Failed - {reply.Status}");
                        Console.ResetColor();
                        FileLogger.Warn($"Internet connection check failed: {reply.Status}");
                    }
                }
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("✗ Internet Connection: ");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Error - {e.Message}");
                Console.ResetColor();
                FileLogger.Warn($"Failed to check internet connection: {e.Message}");
            }
        }
    }
}