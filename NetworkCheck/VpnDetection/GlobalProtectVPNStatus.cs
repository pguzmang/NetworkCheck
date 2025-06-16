using System;
using System.Runtime.Versioning;

namespace NetworkScanner.VpnDetection
{
    public class GlobalProtectVPNStatus
    {
        private const string REGISTRY_PATH = "SOFTWARE\\Palo Alto Networks\\GlobalProtect\\Settings\\";
        private const string REGISTRY_VALUE_NAME = "disable-globalprotect";
         
        [SupportedOSPlatform("windows")]
        public static bool IsGlobalProtectTunnelEstablished()
        {
            return WindowsRegistryReader.IsVPNTunnelEstablished(REGISTRY_PATH, REGISTRY_VALUE_NAME, "0",
                "Global Protect disabled registry value: ", "Global Protect Status: ");
        }
    }
}

