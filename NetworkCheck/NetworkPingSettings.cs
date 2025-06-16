using System.Collections.Generic;

namespace NetworkCheck
{
    public class NetworkPingSettings
    {
        public ServerSettings Servers { get; set; } = new ServerSettings();
        public PingSettings PingSettings { get; set; } = new PingSettings();
    }

    public class ServerSettings
    {
        public List<string> External { get; set; } = new List<string>();
        public List<string> Internal { get; set; } = new List<string>();
        public List<string> AesServers { get; set; } = new List<string>();
    }

    public class PingSettings
    {
        public int PingCount { get; set; } = 10;
        public int TimeoutMilliseconds { get; set; } = 5000;
        public int DelayBetweenPingsMilliseconds { get; set; } = 1000;
    }
}