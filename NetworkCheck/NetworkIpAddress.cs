using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks; // For asynchronous operations
using NetworkScanner.VpnDetection;

namespace NetworkScanner
{

    /// <summary>
    /// Represents the result of a network scan, containing identified IP addresses
    /// and location status (VPN/Office).
    /// </summary>
    public class NetworkScanResult
    {
        public string PrimaryIpAddress { get; set; } = string.Empty;
        public bool IsConsideredWorkingFromHome { get; set; }
        public Dictionary<string, string> FinalUserIpAddressMap { get; private set; } = new Dictionary<string, string>();
        public List<string> SkippedItems { get; private set; } = new List<string>();

        // VPN specific findings during the raw scan
        public bool VpnDetectedDuringScan { get; private set; }
        public string VpnIpAddressFound { get; private set; } = string.Empty;
        public string VpnInterfaceTypeFound { get; private set; } = string.Empty;
        public string VpnMessageFound { get; private set; } = string.Empty;

        /// <summary>
        /// Stores all relevant IP addresses found during the scan, mapped to their interface types.
        /// This is for raw findings before final determination.
        /// </summary>
        private Dictionary<string, string> _allRelevantIpAddresses = new Dictionary<string, string>();

        public void AddSkippedItem(string item)
        {
            SkippedItems.Add(item);
        }

        public void SetVpnInfoFound(string ipAddress, string interfaceType, string message)
        {
            VpnDetectedDuringScan = true;
            VpnIpAddressFound = ipAddress;
            VpnInterfaceTypeFound = interfaceType;
            VpnMessageFound = message;
        }

        public void AddRelevantIpAddressFound(string ipAddress, string interfaceType)
        {
            if (!_allRelevantIpAddresses.ContainsKey(ipAddress))
            {
                _allRelevantIpAddresses.Add(ipAddress, interfaceType);
            }
        }

        public Dictionary<string, string> GetAllRelevantIpAddresses()
        {
            return new Dictionary<string, string>(_allRelevantIpAddresses); // Return a copy
        }

        public void SetPrimaryIpAddress(string ip) => PrimaryIpAddress = ip;
        public void SetIsConsideredWorkingFromHome(bool status) => IsConsideredWorkingFromHome = status;
        public void SetFinalUserIpAddressMap(Dictionary<string, string> map) => FinalUserIpAddressMap = map;
    }

    /// <summary>
    /// The purpose of this class is to perform a network interface scan and identify
    /// relevant IP addresses, prioritizing a private VPN IP address if found.
    /// It encapsulates the scan results into a <see cref="NetworkScanResult"/> object
    /// which is returned to the caller. The caller is responsible for using this
    /// result to update application state.
    /// <para>
    /// The class identifies different types of IP addresses:
    /// 1. ISP public IP address (not retrieved by this class).
    /// 2. Private local network IP address (e.g., 192.168.x.x, 172.x.x.x).
    /// 3. Private VPN IP Address (specifically 10.93.x.x, 10.94.x.x, 10.95.x.x).
    /// </para>
    /// <para>
    /// It handles scenarios where multiple interfaces (Wi-Fi, Ethernet) are active
    /// and distinguishes between IPv4 and IPv6 addresses.
    /// The primary goal is to identify the VPN IP if connected, otherwise identify
    /// a standard office/local network IP.
    /// The results are logged for information purposes.
    /// </para>
    /// </summary>
    public static class NetworkIpAddress
    {
        // Regex patterns for known IP ranges
        private const string DETROIT = "^10\\.93\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";
        private const string TROY = "^10\\.94\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";
        private const string PALO_ALTO = "^10\\.95\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";
        private const string DHCP_NOT_FOUND = "^169\\.254\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";
        private const string CAMPUS_AMAZE_WIRELESS = "^10\\.5\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";
        private const string INTERNET_ONLY_WIRELESS = "^10\\.4\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";
        private const string BIG_OFFICE_NETWORK = "^10\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";
        private const string SMALL_OFFICE_NETWORK = "^172\\.16\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\\.(?:[0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";

        // Compiled Regex patterns for efficiency
        private static readonly IReadOnlyDictionary<Regex, string> OfficePatterns;
        private static readonly IReadOnlyDictionary<Regex, string> NotDhcpPatterns;
        private static readonly IReadOnlyDictionary<Regex, string> VpnPatterns;

        static NetworkIpAddress()
        {
            OfficePatterns = CreateCompiledPatternMap(GetStringOfficeMap());
            NotDhcpPatterns = CreateCompiledPatternMap(GetStringNotDhcpMap());
            VpnPatterns = CreateCompiledPatternMap(GetStringVpnMap());
        }

        /// <summary>
        /// Helper method to compile regex patterns into an immutable dictionary.
        /// </summary>
        /// <param name="stringMap">Map of regex strings to messages.</param>
        /// <returns>An immutable dictionary of compiled Regex objects to messages.</returns>
        private static IReadOnlyDictionary<Regex, string> CreateCompiledPatternMap(Dictionary<string, string> stringMap)
        {
            var compiledMap = new Dictionary<Regex, string>();
            foreach (var entry in stringMap)
            {
                try
                {
                    compiledMap.Add(new Regex(entry.Key, RegexOptions.Compiled), entry.Value);
                }
                catch (ArgumentException e)
                {
                    FileLogger.Debug($"Invalid regex pattern during compilation: {entry.Key}", e);
                }
            }
            return compiledMap;
        }

        /// <summary>
        /// Performs a network scan to identify the computer's relevant IP addresses
        /// and determines the primary IP address and location status (VPN/Office).
        /// This method encapsulates all findings and the final determined state
        /// into a <see cref="NetworkScanResult"/> object, which it returns.
        /// </summary>
        /// <returns>A <see cref="NetworkScanResult"/> object containing details of the scan,
        /// the determined primary IP, the location status, and a map of relevant IPs.
        /// Returns a result object with default/empty values if errors occur during the scan.</returns>
        public static NetworkScanResult GetComputerIpAddress()
        {
            var result = new NetworkScanResult();
            try
            {
                FileLogger.Debug("Starting network IP address scan.");
                PerformNetworkScan(result);
                LogAndSummarizeScanResults(result);
            }
            catch (Exception ex) // Catching general Exception for broader error handling
            {
                FileLogger.Warn($"Exception during network interface iteration: {ex.Message}");
            }
            finally
            {
                FileLogger.Debug("Finished network IP address scan.");
            }
            return result;
        }

        /// <summary>
        /// Performs the actual scanning of network interfaces and collects raw findings.
        /// It iterates through active network interfaces and their IP addresses (primarily IPv4),
        /// identifying VPN and other relevant IPs and adding them to the provided result object.
        /// Skipped interfaces or addresses are also noted in the result.
        /// </summary>
        /// <param name="result">The <see cref="NetworkScanResult"/> object to populate with raw scan findings.
        /// This object is modified by this method and subsequent calls.</param>
        private static void PerformNetworkScan(NetworkScanResult result)
        {
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            if (networkInterfaces == null || !networkInterfaces.Any())
            {
                FileLogger.Warn("No network interfaces found.");
                result.AddSkippedItem("No network interfaces found.");
                return;
            }

            foreach (NetworkInterface networkInterface in networkInterfaces)
            {
                try
                {
                    // Check if the interface is operational, not loopback, and has addresses
                    bool isOperational = networkInterface.OperationalStatus == OperationalStatus.Up;
                    bool isLoopback = networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback;
                    bool hasAddresses = networkInterface.GetIPProperties().UnicastAddresses.Any();

                    // C# NetworkInterface doesn't have a direct equivalent to isVirtual or MTU for active use check
                    // We rely on OperationalStatus.Up and address presence.
                    if (isOperational && !isLoopback && hasAddresses)
                    {
                        IteratingInetAddresses(networkInterface, result);
                    }
                    else
                    {
                        result.AddSkippedItem($"Skipped interface: {networkInterface.Description} (Operational: {isOperational}, Loopback: {isLoopback}, HasAddresses: {hasAddresses})");
                    }
                }
                catch (Exception exception)
                {
                    FileLogger.Debug($"Exception getting interface properties for {networkInterface.Description}", exception);
                    result.AddSkippedItem($"Exception getting properties for interface: {networkInterface.Description} - {exception.Message}");
                }
            }
        }

        /// <summary>
        /// Iterates through all IP addresses associated with a given network interface.
        /// Processes IPv4 addresses by calling <see cref="GetIPv4Info(IPAddress, NetworkInterface, NetworkScanResult)"/>
        /// and logs relevant IPv6 addresses. Skips loopback addresses.
        /// </summary>
        /// <param name="networkInterface">The network interface to process.</param>
        /// <param name="result">The <see cref="NetworkScanResult"/> object to populate.</param>
        private static void IteratingInetAddresses(NetworkInterface networkInterface, NetworkScanResult result)
        {
            foreach (UnicastIPAddressInformation ip in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) // IPv4
                {
                    if (!IPAddress.IsLoopback(ip.Address) && !ip.Address.Equals(IPAddress.Any))
                    {
                        GetIPv4Info(ip.Address, networkInterface, result);
                    }
                    else
                    {
                        result.AddSkippedItem($"Skipped loopback IPv4 address: {ip.Address}");
                    }
                }
                else if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) // IPv6
                {
                    if (!IPAddress.IsLoopback(ip.Address) && !ip.Address.Equals(IPAddress.IPv6Any))
                    {
                        LogIPv6Info(ip.Address, networkInterface, result);
                    }
                    else
                    {
                        result.AddSkippedItem($"Skipped loopback IPv6 address: {ip.Address}");
                    }
                }
                else
                {
                    result.AddSkippedItem($"Skipped non-IPv4/IPv6 address: {ip.Address}");
                }
            }
        }

        /// <summary>
        /// Processes a single IPv4 address found during the scan.
        /// It identifies if the address is a known VPN IP range or a DHCP failure (APIPA) address.
        /// Valid, non-APIPA addresses (including VPN) are added to the list of
        /// <see cref="NetworkScanResult.GetAllRelevantIpAddresses"/>. If a VPN IP is detected,
        /// its specific details are also stored in the result object.
        /// Logs relevant information about the address and its interface.
        /// </summary>
        /// <param name="ipAddress">The IPv4 address to process.</param>
        /// <param name="networkInterface">The network interface associated with the address.</param>
        /// <param name="result">The <see cref="NetworkScanResult"/> object to populate with raw findings.</param>
        private static void GetIPv4Info(IPAddress ipAddress, NetworkInterface networkInterface,
                                        NetworkScanResult result)
        {
            var logMessages = new System.Text.StringBuilder();

            string ipAddressString = ipAddress.ToString();
            string name = networkInterface.Name;
            string description = networkInterface.Description; // C# uses Description
            string interfaceType = GetInterfaceType(name, description);

            logMessages.AppendLine($"Processing IPv4 address: {ipAddressString} , Interface {name} , description: {description} , type: {interfaceType}");

            // Check for WSL interface and skip it
            if (name.ToLowerInvariant().Contains("wsl") || description.ToLowerInvariant().Contains("wsl"))
            {
                logMessages.AppendLine($"Skipped WSL interface IP address: {ipAddressString}");
                result.AddSkippedItem($"Skipped WSL interface IP address: {ipAddressString} (Interface: {name}, Description: {description})");
                FileLogger.Debug(logMessages.ToString());
                return;
            }

            // Check for DHCP not found (APIPA) first
            if (!string.IsNullOrWhiteSpace(LogComputerLocation(ipAddressString, NotDhcpPatterns)))
            {
                logMessages.AppendLine($"Skipped DHCP not found IP address: {ipAddressString}");
                result.AddSkippedItem($"Skipped DHCP not found IP address: {ipAddressString}");
                FileLogger.Debug(logMessages.ToString());
                return;
            }

            // If it's not APIPA, check if it's a VPN IP
            if (IsVpnIpAddress(ipAddressString))
            {
                logMessages.AppendLine($"Identified as a VPN IP address: {ipAddressString}");
                
                // Check VPN status using VPNStatusChecker
                bool vpnIsActive = false;
                if (OperatingSystem.IsWindows())
                {
                    vpnIsActive = VPNStatusChecker.VPNIsTurnedOn();
                }
                logMessages.AppendLine($"VPN Status Check Result: {vpnIsActive}");
                
                if (!result.VpnDetectedDuringScan)
                {
                    string vpnMsg = LogComputerLocation(ipAddressString, VpnPatterns);
                    result.SetVpnInfoFound(ipAddressString, interfaceType, vpnMsg);
                    logMessages.AppendLine($"Set primary VPN IP found during scan to: {ipAddressString}");
                }
                else
                {
                    logMessages.AppendLine($"Another VPN IP address ({ipAddressString}) found, primary VPN already recorded.");
                }
                result.AddRelevantIpAddressFound(ipAddressString, interfaceType);
            }
            else
            {
                result.AddRelevantIpAddressFound(ipAddressString, interfaceType);
                LogComputerLocation(ipAddressString, OfficePatterns);
                logMessages.AppendLine($"Added non-VPN IP {ipAddressString} to all relevant IPs.");
            }

            FileLogger.Debug(logMessages.ToString());
        }

        /// <summary>
        /// Logs information about an IPv6 address and its associated network interface.
        /// This method is primarily for debugging and informational purposes. It filters out
        /// loopback addresses, link-local IPv6 addresses (fe80::), and hostnames starting with "local".
        /// Logs skipped status to the result object.
        /// </summary>
        /// <param name="ipAddress">The IPv6 <see cref="IPAddress"/> to log.</param>
        /// <param name="networkInterface">The <see cref="NetworkInterface"/> associated with the IPv6 address.</param>
        /// <param name="result">The <see cref="NetworkScanResult"/> object (used here for logging skipped items).</param>
        private static void LogIPv6Info(IPAddress ipAddress, NetworkInterface networkInterface, NetworkScanResult result)
        {
            var logMessages = new System.Text.StringBuilder();

            // Check if the address is IPv6 and not loopback
            if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && !IPAddress.IsLoopback(ipAddress))
            {
                string subIpAddress = ipAddress.ToString();
                // Avoid link-local IPv6 addresses (fe80::) which are common and not useful for location
                if (!subIpAddress.StartsWith("fe80:", StringComparison.OrdinalIgnoreCase))
                {
                    logMessages.AppendLine($"This is an IPv6 Address: {ipAddress}");

                    string name = networkInterface.Name;
                    string description = networkInterface.Description;
                    string interfaceType = GetInterfaceType(name, description);
                    logMessages.AppendLine($"Interface {name}, description: {description}, type: {interfaceType}");
                }
                else
                {
                    result.AddSkippedItem($"Skipped link-local IPv6 address: {ipAddress}");
                }
            }
            else
            {
                result.AddSkippedItem($"Skipped non-IPv6 or loopback address in LogIPv6Info: {ipAddress}");
            }

            if (logMessages.Length > 0)
            {
                FileLogger.Debug(logMessages.ToString());
            }
        }

        /// <summary>
        /// Summarizes the raw findings collected in the <see cref="NetworkScanResult"/>,
        /// determines the final primary IP address, the location status (VPN/Office),
        /// and the map of relevant IPs to be used by the caller.
        /// These determined values are populated back into the provided <see cref="NetworkScanResult"/> object.
        /// This method also logs the summary and details about skipped items.
        /// </summary>
        /// <param name="result">The <see cref="NetworkScanResult"/> object containing the raw scan findings,
        /// which will be updated with the summarized and final results.</param>
        private static void LogAndSummarizeScanResults(NetworkScanResult result)
        {
            string determinedPrimaryIp = string.Empty;
            bool determinedIsWorkingFromHome = false;
            var determinedFinalUserIpMap = new Dictionary<string, string>();

            if (result.VpnDetectedDuringScan)
            {
                determinedIsWorkingFromHome = true;
                determinedPrimaryIp = result.VpnIpAddressFound;

                FileLogger.Info($"VPN IP address found during scan: {result.VpnIpAddressFound} ({result.VpnInterfaceTypeFound})");
                FileLogger.Info(result.VpnMessageFound);

                foreach (var entry in result.GetAllRelevantIpAddresses())
                {
                    determinedFinalUserIpMap[entry.Key] = entry.Value;
                }
            }
            else
            {
                var otherRelevantIpAddresses = result.GetAllRelevantIpAddresses();
                if (!otherRelevantIpAddresses.Any())
                {
                    FileLogger.Fatal("No valid IP address (VPN or Office) was found during scan.");
                }
                else
                {
                    FileLogger.Info("No VPN detected. Assuming Working from the Office.");
                    foreach (var entry in otherRelevantIpAddresses)
                    {
                        determinedFinalUserIpMap[entry.Key] = entry.Value;
                    }

                    determinedPrimaryIp = otherRelevantIpAddresses.Keys.FirstOrDefault() ?? string.Empty;

                    foreach (string ipAddress in otherRelevantIpAddresses.Keys)
                    {
                        LogComputerLocation(ipAddress, OfficePatterns);
                    }
                }
            }

            result.SetPrimaryIpAddress(determinedPrimaryIp);
            result.SetIsConsideredWorkingFromHome(determinedIsWorkingFromHome);
            result.SetFinalUserIpAddressMap(determinedFinalUserIpMap);

            FileLogger.Info($"Summary Complete - Determined Primary IP: {result.PrimaryIpAddress}");
            FileLogger.Info($"Summary Complete - Determined Working From Home: {result.IsConsideredWorkingFromHome}");
            FileLogger.Info($"Summary Complete - Final IP Map Size: {result.FinalUserIpAddressMap.Count}");
            FileLogger.Info($"Summary Complete - Final IP Map: {string.Join(", ", result.FinalUserIpAddressMap.Select(kv => $"{kv.Key}: {kv.Value}"))}");

            LogSkippedItems(result);
        }

        /// <summary>
        /// Logs items that were skipped during the network scan process.
        /// This is primarily for debugging and transparency.
        /// </summary>
        /// <param name="result">The <see cref="NetworkScanResult"/> containing the list of skipped items.</param>
        private static void LogSkippedItems(NetworkScanResult result)
        {
            List<string> skippedItems = result.SkippedItems;
            if (skippedItems != null && skippedItems.Any())
            {
                FileLogger.Debug("**** Skipped items during network scan:  *****");
                foreach (string item in skippedItems)
                {
                    FileLogger.Debug($"  - {item}");
                }
                FileLogger.Debug("**** End of Skipped items ****");
            }
        }

        /// <summary>
        /// Determines the type of network interface based on its programmatic name and description.
        /// It checks for common prefixes associated with Ethernet, Wi-Fi, and known VPN interface names.
        /// </summary>
        /// <param name="name">The programmatic name of the network interface (e.g., "eth0", "en0", "wlan0").</param>
        /// <param name="description">The user-friendly description of the network interface.</param>
        /// <returns>A string describing the interface type (e.g., "This is an Ethernet interface.",
        /// "This is a Wi-Fi interface.", "This is a VPN interface.") or "Unknown" if the
        /// name/description does not match known patterns.</returns>
        private static string GetInterfaceType(string name, string description)
        {
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(description))
            {
                return "Unknown (Name/Description Blank)";
            }

            // Use ToLowerInvariant for case-insensitive matching
            string lowerName = (name ?? string.Empty).ToLowerInvariant();
            string lowerDescription = (description ?? string.Empty).ToLowerInvariant();

            // Check for Ethernet
            if (lowerName.StartsWith("eth") && lowerName != "eth9" || lowerName.StartsWith("en") || lowerDescription.Contains("ethernet"))
            {
                return "This is an Ethernet interface.";
            }
            // Check for Wi-Fi
            else if (lowerName.StartsWith("wlan") || lowerName.StartsWith("wifi") || lowerDescription.Contains("wi-fi") || lowerDescription.Contains("wireless"))
            {
                return "This is a Wi-Fi interface.";
            }
            // Check for VPN
            else if (lowerName == "eth9" || lowerName.Contains("vpn") || lowerName.Contains("tun") || lowerName.Contains("tap") || lowerDescription.Contains("vpn"))
            {
                return "This is a VPN interface.";
            }

            return "Unknown interface type.";
        }

        /// <summary>
        /// Checks an IP address against a map of regex patterns and their corresponding messages.
        /// Logs the message if a match is found. Primarily used for logging locations/statuses.
        /// </summary>
        /// <param name="ipAddress">The IP address string to check.</param>
        /// <param name="compiledIpMessagesMap">A map where keys are compiled regular expression patterns and values are
        /// the messages to log and return if the IP address matches the key's pattern.</param>
        /// <returns>The descriptive message associated with the first matching pattern,
        /// or an empty string if the IP address is blank or no pattern matches.</returns>
        private static string LogComputerLocation(string ipAddress, IReadOnlyDictionary<Regex, string> compiledIpMessagesMap)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || compiledIpMessagesMap == null || !compiledIpMessagesMap.Any())
            {
                return string.Empty;
            }

            foreach (var entry in compiledIpMessagesMap)
            {
                if (entry.Key.IsMatch(ipAddress))
                {
                    FileLogger.Debug($"IP address {ipAddress} matches pattern. Location/Status: {entry.Value}");
                    return entry.Value;
                }
            }
            return string.Empty;
        }

        private static Dictionary<string, string> GetStringOfficeMap()
        {
            return new Dictionary<string, string>
            {
                { CAMPUS_AMAZE_WIRELESS, "Campus/Amaze Wireless" },
                { INTERNET_ONLY_WIRELESS, "Internet Only Wireless" },
                { BIG_OFFICE_NETWORK, "Big Office Network" },
                { SMALL_OFFICE_NETWORK, "Small Office Network" }
            };
        }

        private static Dictionary<string, string> GetStringNotDhcpMap()
        {
            return new Dictionary<string, string>
            {
                { DHCP_NOT_FOUND, "Computer is unable to detect DHCP server" }
            };
        }

        private static Dictionary<string, string> GetStringVpnMap()
        {
            return new Dictionary<string, string>
            {
                { DETROIT, "Working from Home (Detroit)" },
                { TROY, "Working from Home (Troy)" },
                { PALO_ALTO, "Working from Home (Palo Alto)" }
            };
        }

        /// <summary>
        /// Checks if a given IP address string matches any of the defined VPN IP range patterns.
        /// </summary>
        /// <param name="ipAddress">The IP address string to check.</param>
        /// <returns>true if the IP address matches a VPN pattern, false otherwise. Returns false
        /// if the input IP address is blank.</returns>
        private static bool IsVpnIpAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return false;
            }
            foreach (Regex pattern in VpnPatterns.Keys)
            {
                if (pattern.IsMatch(ipAddress))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Creates and starts a new Task to asynchronously get the computer's IP address.
        /// The result of the scan (a <see cref="NetworkScanResult"/> object) is produced by this task.
        /// </summary>
        /// <returns>A <see cref="Task{NetworkScanResult}"/> that represents the asynchronous operation
        /// and can be awaited to get the scan result.</returns>
        public static Task<NetworkScanResult> CreateNewTaskToGetIpAddress()
        {
            FileLogger.Debug("Created new Task to get IP address.");
            // SfProperties is not available, so this line is modified.
            // FileLogger.Debug("Ip address get when loading: " + SfProperties.NETWORK_SCAN_RESULT.PrimaryIpAddress);
            return Task.Run(() => GetComputerIpAddress());
        }

        // Example usage placeholder for SfProperties.NETWORK_SCAN_RESULT
        // In a real C# application, this would likely be handled via dependency injection
        // or a global application state management.
        public static class SfProperties
        {
            public static NetworkScanResult NETWORK_SCAN_RESULT = new NetworkScanResult();
        }
    }
}
