# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a C# .NET 9.0 console application that performs network interface scanning to identify IP addresses and determine work location status. The main purpose is to distinguish between VPN connections (indicating remote work) and office network connections.

## Build and Run Commands

```bash
# Build the project
dotnet build

# Run the application
dotnet run --project NetworkCheck/NetworkCheck.csproj

# Build in release mode
dotnet build --configuration Release

# Run tests (if any exist)
dotnet test
```

## Architecture

The application consists of two main classes in the `NetworkScanner` namespace:

- **NetworkScanResult**: Data container that holds scan results including IP addresses, VPN detection status, and work location determination
- **NetworkIpAddress**: Static utility class that performs the actual network interface scanning and analysis

### Key Components

1. **Network Interface Scanning**: Iterates through all active network interfaces, filtering out loopback and non-operational interfaces
2. **IP Classification**: Uses regex patterns to identify specific IP ranges:
   - VPN ranges: 10.93.x.x (Detroit), 10.94.x.x (Troy), 10.95.x.x (Palo Alto)
   - Office networks: Various 10.x.x.x and 172.16.x.x ranges
   - DHCP failure detection: 169.254.x.x (APIPA)
3. **Location Detection**: Determines if user is working from home (VPN detected) or office (local network)
4. **Logging**: Custom console-based logger for debugging and information output

### Core Method

`NetworkIpAddress.GetComputerIpAddress()` returns a `NetworkScanResult` containing:
- Primary IP address
- Work location status (home vs office)
- Map of all relevant IP addresses found
- VPN detection details if applicable

The application prioritizes VPN IP addresses when multiple interfaces are active and handles both IPv4 and IPv6 addresses (though focuses primarily on IPv4 for location determination).