using System;
using System.Diagnostics;
using System.Management;

namespace NetworkScanner
{
    public static class WiFiNetworkInfo
    {
        public static string GetCurrentWiFiSSID()
        {
            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    return "WiFi SSID detection only supported on Windows";
                }

                // First try netsh command approach (most reliable)
                string ssidFromNetsh = GetSSIDFromNetsh();
                if (!string.IsNullOrWhiteSpace(ssidFromNetsh) && !ssidFromNetsh.Contains("Error"))
                {
                    return ssidFromNetsh;
                }

                // Fallback to WMI approach
                string ssidFromWMI = GetSSIDFromWMI();
                if (!string.IsNullOrWhiteSpace(ssidFromWMI) && !ssidFromWMI.Contains("Error"))
                {
                    return ssidFromWMI;
                }

                return "Not connected to Wi-Fi";
            }
            catch (Exception ex)
            {
                return $"Error retrieving Wi-Fi SSID: {ex.Message}";
            }
        }

        public static bool IsConnectedToWiFi()
        {
            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    return false;
                }

                string ssid = GetCurrentWiFiSSID();
                return !string.IsNullOrWhiteSpace(ssid) && 
                       !ssid.Contains("Error") && 
                       !ssid.Contains("Not connected") &&
                       !ssid.Contains("only supported");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the current WiFi SSID using netsh command.
        /// </summary>
        private static string GetSSIDFromNetsh()
        {
            try
            {
                var processInfo = new ProcessStartInfo("netsh", "wlan show interfaces")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string line in lines)
                        {
                            if (line.Trim().StartsWith("SSID", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = line.Split(':');
                                if (parts.Length > 1)
                                {
                                    string ssid = parts[1].Trim();
                                    if (!string.IsNullOrWhiteSpace(ssid))
                                    {
                                        return ssid;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Debug($"Error getting SSID from netsh: {ex.Message}", ex);
            }
            
            return string.Empty;
        }

        /// <summary>
        /// Gets the current WiFi SSID using WMI as fallback.
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static string GetSSIDFromWMI()
        {
            try
            {
                // Query for active wireless adapters
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus = 2 AND AdapterTypeId = 9"))
                {
                    foreach (ManagementObject adapter in searcher.Get())
                    {
                        string? adapterName = adapter["Name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(adapterName))
                        {
                            // Try to get SSID from wireless profile
                            try
                            {
                                using (var profileSearcher = new ManagementObjectSearcher($"SELECT * FROM MSNdis_80211_ServiceSetIdentifier WHERE InstanceName LIKE '%{adapterName}%'"))
                                {
                                    foreach (ManagementObject profile in profileSearcher.Get())
                                    {
                                        var ssidBytes = profile["Ndis80211SsId"] as byte[];
                                        if (ssidBytes != null && ssidBytes.Length > 0)
                                        {
                                            string ssid = System.Text.Encoding.UTF8.GetString(ssidBytes).TrimEnd('\0');
                                            if (!string.IsNullOrWhiteSpace(ssid))
                                            {
                                                return ssid;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception innerEx)
                            {
                                FileLogger.Debug($"Error querying wireless profile for adapter {adapterName}: {innerEx.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Debug($"Error getting WiFi SSID from WMI: {ex.Message}", ex);
            }
            
            return string.Empty;
        }
    }
}