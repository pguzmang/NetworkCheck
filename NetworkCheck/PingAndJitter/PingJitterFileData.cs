using System;
using System.Collections.Generic;

namespace NetworkScanner
{
    public class PingJitterFileData
    {
        public string Category { get; set; } = string.Empty; // external, internal, internalaes
        public string TestType { get; set; } = string.Empty; // ping or jitter
        public DateTime TestTimestamp { get; set; }
        public List<TestResult> Results { get; set; } = new List<TestResult>();
    }

    public class TestResult
    {
        public string Host { get; set; } = string.Empty;
        public double Value { get; set; } // ping or jitter value in ms
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; }
    }
}