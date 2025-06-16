using System;
using System.Runtime.Versioning;

namespace NetworkScanner.VpnDetection
{
    public class IvantiVPNStatus
    {
        private const string REGISTRY_PATH = "SOFTWARE\\Pulse Secure\\Pulse\\State";
        private const string REGISTRY_VALUE_NAME = "VpnTunnelEstablished";

        [SupportedOSPlatform("windows")]
        public static bool IsIvantiTunnelEstablished()
        {
            return WindowsRegistryReader.IsVPNTunnelEstablished(REGISTRY_PATH, REGISTRY_VALUE_NAME, "1",
                "Ivanti registry value: ", "Ivanti VPN Tunnel Status: ");
        }
    }
}