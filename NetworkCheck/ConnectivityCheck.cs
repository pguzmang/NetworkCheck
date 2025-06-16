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
                        FileLogger.Info("Internet connection is available");
                    }
                    else
                    {
                        FileLogger.Warn($"Internet connection check failed: {reply.Status}");
                    }
                }
            }
            catch (Exception e)
            {
                FileLogger.Warn($"Failed to check internet connection: {e.Message}");
            }
        }
    }
}