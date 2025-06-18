using NetworkScanner;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\nShutting down...");
    Console.ResetColor();
    FileLogger.Close();
    Environment.Exit(0);
};

// Display startup banner
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                          🌐 Network Monitor Tool                            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("🚀 Starting network monitoring. Press Ctrl+C to stop.\n");
Console.ResetColor();

// Create results directory if it doesn't exist
string resultsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "results");
Directory.CreateDirectory(resultsDirectory);

string medianPingFile = Path.Combine(resultsDirectory, "median_ping_results.txt");
string jitterFile = Path.Combine(resultsDirectory, "jitter_results.txt");

// Write headers to files
File.WriteAllText(medianPingFile, "Timestamp,Host,MedianPing(ms)\n");
File.WriteAllText(jitterFile, "Timestamp,Host,Jitter(ms)\n");

Console.ForegroundColor = ConsoleColor.DarkCyan;
Console.WriteLine($"📁 Results Directory: {resultsDirectory}");
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine($"   📄 Median Ping: {Path.GetFileName(medianPingFile)}");
Console.WriteLine($"   📄 Jitter Data: {Path.GetFileName(jitterFile)}");
Console.ResetColor();

RunMainLoop();

void RunMainLoop()
{
    try
    {
        while (true)
        {
        var testStartTime = Stopwatch.StartNew();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Running network scan...");
        Console.ResetColor();
        
        // Run IP address scan
        var result = NetworkIpAddress.GetComputerIpAddress();
        
        // Log IP address result to categorized files
        var categorizedIpWriter = new CategorizedIpAddressResultWriter();
        categorizedIpWriter.WriteIpAddressResult(result);

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("🌐 Primary IP Address: ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(result.PrimaryIpAddress);
        
        // Add IP type classification with colors
        if (result.PrimaryIpAddress.StartsWith("10."))
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(" [Private Network]");
        }
        else if (result.PrimaryIpAddress.StartsWith("192.168."))
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine(" [Local Network]");
        }
        else if (result.PrimaryIpAddress.StartsWith("172."))
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(" [Corporate Network]");
        }
        else if (result.PrimaryIpAddress.StartsWith("169.254."))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(" [APIPA - No DHCP]");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(" [Public IP]");
        }
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Working from Home: ");
        Console.ForegroundColor = result.IsConsideredWorkingFromHome ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(result.IsConsideredWorkingFromHome);
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("VPN Detected: ");
        Console.ForegroundColor = result.VpnDetectedDuringScan ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(result.VpnDetectedDuringScan);
        Console.ResetColor();


        // Display WiFi SSID if available
        if (!string.IsNullOrWhiteSpace(result.WiFiSSID))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("📶 WiFi SSID: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(result.WiFiSSID);
            Console.ResetColor();
        }

        // Display Ethernet DNS suffix if available (only when connected via Ethernet)
        if (!string.IsNullOrWhiteSpace(result.EthernetDnsSuffix))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("🔌 Ethernet DNS Suffix: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(result.EthernetDnsSuffix);
            Console.ResetColor();
        }

        if (result.VpnDetectedDuringScan)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("VPN IP: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(result.VpnIpAddressFound);
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("VPN Message: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(result.VpnMessageFound);
            Console.ResetColor();
        }

        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"🔍 Network Interfaces Detected: {result.FinalUserIpAddressMap.Count}");
        Console.ResetColor();
        
        int interfaceCount = 0;
        foreach (var ip in result.FinalUserIpAddressMap)
        {
            interfaceCount++;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"   [{interfaceCount}] ");
            
            // Color code IP addresses by type
            if (ip.Key.StartsWith("10.93.") || ip.Key.StartsWith("10.94.") || ip.Key.StartsWith("10.95."))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("🔐 VPN: ");
                Console.ForegroundColor = ConsoleColor.Cyan;
            }
            else if (ip.Key.StartsWith("10."))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("🏢 Corporate: ");
                Console.ForegroundColor = ConsoleColor.White;
            }
            else if (ip.Key.StartsWith("192.168."))
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write("🏠 Local: ");
                Console.ForegroundColor = ConsoleColor.White;
            }
            else if (ip.Key.StartsWith("172."))
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.Write("🏗️ Internal: ");
                Console.ForegroundColor = ConsoleColor.White;
            }
            else if (ip.Key.StartsWith("169.254."))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("⚠️ APIPA: ");
                Console.ForegroundColor = ConsoleColor.DarkRed;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("🌍 Public: ");
                Console.ForegroundColor = ConsoleColor.Cyan;
            }
            
            Console.Write(ip.Key);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($" ({ip.Value})");
            Console.ResetColor();
        }

        // Run network ping and jitter tests
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n🌐 Running network ping and jitter tests...");
        Console.ResetColor();
        ConnectivityCheck.CheckInternetConnection();
        
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("📊 Starting ping tests for all servers...");
        Console.ResetColor();
        var pingResults = NetworkPingAndJitterTest.RunAllTests(result);
        
        // Display summary of ping results
        var successCount = pingResults.Count(r => r.Success);
        var failCount = pingResults.Count - successCount;
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("📈 Test Summary: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{successCount} Success");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(" | ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"{failCount} Failed");
        Console.ResetColor();
        
        // Save results to categorized files with confidence level system
        var categorizedPingWriter = new CategorizedPingJitterResultWriter(result);
        categorizedPingWriter.WriteResults(pingResults);
        
        // Also save to simple results files for backward compatibility
        SavePingResults(pingResults, medianPingFile, jitterFile);
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nResults saved to:");
        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine($"  - {medianPingFile}");
        Console.WriteLine($"  - {jitterFile}");
        Console.ResetColor();
        
        
        testStartTime.Stop();
        var elapsedSeconds = testStartTime.Elapsed.TotalSeconds;
        
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("\n" + new string('═', 80));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("⚡ Test completed in ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"{elapsedSeconds:F2}");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(" seconds");
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(new string('═', 80));
        Console.ResetColor();
        
        // Add a countdown display
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        for (int i = 180; i > 0; i -= 30)
        {
            Console.Write($"\r⏳ Next scan in: {i} seconds...");
            Thread.Sleep(30000);
        }
            Console.WriteLine("\r" + new string(' ', 50)); // Clear the countdown line
            Console.ResetColor();
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error occurred: {ex.Message}");
        Console.ResetColor();
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
    
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"💾 Saving {results.Count} ping results to files...");
    Console.ResetColor();
    
    foreach (var result in results)
    {
        if (result.Success)
        {
            medianBuilder.AppendLine($"{timestamp},{result.Host},{result.MedianPing:F2}");
            jitterBuilder.AppendLine($"{timestamp},{result.Host},{result.Jitter:F2}");
            
            // Show individual result status
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write("  ✓ ");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"{result.Host}: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{result.MedianPing:F1}ms");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($" (jitter: {result.Jitter:F1}ms)");
            Console.ResetColor();
        }
        else
        {
            medianBuilder.AppendLine($"{timestamp},{result.Host},FAIL");
            jitterBuilder.AppendLine($"{timestamp},{result.Host},FAIL");
            
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write("  ✗ ");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"{result.Host}: ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILED");
            Console.ResetColor();
        }
    }
    
        File.AppendAllText(medianFile, medianBuilder.ToString());
        File.AppendAllText(jitterFile, jitterBuilder.ToString());
    }
}

