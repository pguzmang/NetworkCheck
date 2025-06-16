using System;
using System.Runtime.Versioning;

namespace NetworkScanner.VpnDetection
{
    public class VPNStatusChecker
    {
        [SupportedOSPlatform("windows")]
        public static bool VPNIsTurnedOn()
        {
            bool status = true;
            status = GlobalProtectVPNStatus.IsGlobalProtectTunnelEstablished();
            if (!status)
            {
                status = IvantiVPNStatus.IsIvantiTunnelEstablished();
            }
            return status;
        }
    }
}