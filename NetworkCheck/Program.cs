using NetworkScanner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nShutting down...");
    FileLogger.Close();
    Environment.Exit(0);
};

Console.WriteLine("Starting network monitoring. Press Ctrl+C to stop.\n");

// Create results directory if it doesn't exist
string resultsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "results");
Directory.CreateDirectory(resultsDirectory);

string medianPingFile = Path.Combine(resultsDirectory, "median_ping_results.txt");
string jitterFile = Path.Combine(resultsDirectory, "jitter_results.txt");

// Write headers to files
File.WriteAllText(medianPingFile, "Timestamp,Host,MedianPing(ms)\n");
File.WriteAllText(jitterFile, "Timestamp,Host,Jitter(ms)\n");

try
{
    while (true)
    {
        Console.WriteLine($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Running network scan...");
        
        // Run IP address scan
        var result = NetworkIpAddress.GetComputerIpAddress();

        Console.WriteLine($"Primary IP Address: {result.PrimaryIpAddress}");
        Console.WriteLine($"Working from Home: {result.IsConsideredWorkingFromHome}");
        Console.WriteLine($"VPN Detected: {result.VpnDetectedDuringScan}");

        if (result.VpnDetectedDuringScan)
        {
            Console.WriteLine($"VPN IP: {result.VpnIpAddressFound}");
            Console.WriteLine($"VPN Message: {result.VpnMessageFound}");
        }

        Console.WriteLine($"All IP Addresses Found: {result.FinalUserIpAddressMap.Count}");
        foreach (var ip in result.FinalUserIpAddressMap)
        {
            Console.WriteLine($"  {ip.Key} ({ip.Value})");
        }

        // Run network ping and jitter tests
        Console.WriteLine("\nRunning network ping and jitter tests...");
        ConnectivityCheck.CheckInternetConnection();
        var pingResults = NetworkPingAndJitterTest.RunAllTests();
        
        // Save results to files
        SavePingResults(pingResults, medianPingFile, jitterFile);
        
        Console.WriteLine($"\nResults saved to:");
        Console.WriteLine($"  - {medianPingFile}");
        Console.WriteLine($"  - {jitterFile}");
        
        Console.WriteLine("\n" + new string('-', 80));
        Console.WriteLine("Waiting 3 minutes before next scan...");
        
        Thread.Sleep(30000); // 3 minutes = 180 seconds = 180000 milliseconds
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error occurred: {ex.Message}");
    FileLogger.Fatal($"Fatal error in main loop: {ex}");
}
finally
{
    // Ensure the logger is properly closed
    FileLogger.Close();
}

void SavePingResults(List<PingResult> results, string medianFile, string jitterFile)
{
    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    var medianBuilder = new StringBuilder();
    var jitterBuilder = new StringBuilder();
    
    foreach (var result in results)
    {
        if (result.Success)
        {
            medianBuilder.AppendLine($"{timestamp},{result.Host},{result.MedianPing:F2}");
            jitterBuilder.AppendLine($"{timestamp},{result.Host},{result.Jitter:F2}");
        }
        else
        {
            medianBuilder.AppendLine($"{timestamp},{result.Host},FAIL");
            jitterBuilder.AppendLine($"{timestamp},{result.Host},FAIL");
        }
    }
    
    File.AppendAllText(medianFile, medianBuilder.ToString());
    File.AppendAllText(jitterFile, jitterBuilder.ToString());
}
