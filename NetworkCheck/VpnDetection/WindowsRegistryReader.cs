using Microsoft.Win32;
using System;
using System.Runtime.Versioning;
using NetworkScanner;

namespace NetworkScanner.VpnDetection
{
    public class WindowsRegistryReader
    {
        [SupportedOSPlatform("windows")]
        private static string? GetRegistryValue(string registryPath, string registryValueName)
        {
            try
            {
                using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(registryPath))
                {
                    if (key != null)
                    {
                        object? value = key.GetValue(registryValueName);
                        if (value != null && value is int intValue)
                        {
                            return intValue.ToString("x");
                        }
                    }
                    return null;
                }
            }
            catch (Exception e)
            {
                FileLogger.Warn($"{e.Message}");
                return null;
            }
        }

        [SupportedOSPlatform("windows")]
        public static bool IsVPNTunnelEstablished(string registryPath, string registryValueName,
                                                   string validValue, string msg1, string msg2)
        {
            string? value = WindowsRegistryReader.GetRegistryValue(registryPath, registryValueName);
            bool status = string.Equals(validValue, value);
            FileLogger.Debug($"{msg1}{value}");
            FileLogger.Debug($"{msg2}{(status ? "On" : "Off")}");
            return status;
        }
    }
}