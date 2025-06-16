using System;

namespace NetworkScanner
{
    public class PingResult
    {
        public string Host { get; set; } = string.Empty;
        public double MedianPing { get; set; }
        public double Jitter { get; set; }
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
    }
}