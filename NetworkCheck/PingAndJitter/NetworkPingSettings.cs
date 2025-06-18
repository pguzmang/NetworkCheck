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
        
        // Smart adaptive ping counts for enterprise networks
        public AdaptivePingCounts AdaptivePingCounts { get; set; } = new AdaptivePingCounts();
        
        // Random delay to stagger network load across multiple computers
        public RandomDelaySettings RandomDelay { get; set; } = new RandomDelaySettings();
    }
    
    public class AdaptivePingCounts
    {
        public bool Enabled { get; set; } = true;
        public int ExternalServerPings { get; set; } = 7; // Internet variability needs more samples
        public int InternalServerPings { get; set; } = 3; // Stable corporate network
        public int AesServerPings { get; set; } = 3; // Critical servers - minimize load
    }
    
    public class RandomDelaySettings
    {
        public bool Enabled { get; set; } = true;
        public int MinDelaySeconds { get; set; } = 0;
        public int MaxDelaySeconds { get; set; } = 180; // 3 minutes staggered start
    }
}