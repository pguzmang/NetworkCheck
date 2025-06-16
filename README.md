# NetworkCheck - Network Scanner and Location Detection Tool

A C# .NET 9.0 console application that performs network interface scanning to identify IP addresses and determine work location status. The main purpose is to distinguish between VPN connections (indicating remote work) and office network connections.

## Features

- **Network Interface Scanning**: Scans all active network interfaces to identify IP addresses
- **VPN Detection**: Automatically detects VPN connections using IP range analysis
- **Work Location Determination**: Identifies whether you're working from home (VPN) or office (local network)
- **IP Classification**: Uses regex patterns to identify specific IP ranges for different locations
- **DHCP Failure Detection**: Detects APIPA addresses (169.254.x.x) indicating network issues

## Prerequisites

- .NET 9.0 SDK
- Windows OS (recommended for full VPN detection capabilities)

## Installation

1. Clone the repository:
```bash
git clone <repository-url>
cd NetworkCheck
```

2. Build the project:
```bash
dotnet build
```

## Usage

### Basic Run
```bash
dotnet run --project NetworkCheck/NetworkCheck.csproj
```

The application will:
1. Scan all active network interfaces
2. Identify IP addresses and classify them by range
3. Detect VPN connections and determine work location
4. Display results including primary IP address and location status

## Architecture

The application consists of two main classes in the `NetworkScanner` namespace:

- **NetworkScanResult**: Data container that holds scan results including IP addresses, VPN detection status, and work location determination
- **NetworkIpAddress**: Static utility class that performs the actual network interface scanning and analysis

### Main Components

1. **NetworkIpAddress**: Core IP address scanning and location detection
2. **VpnDetection**: Windows Registry-based VPN status detection
   - GlobalProtectVPNStatus
   - IvantiVPNStatus
   - WindowsRegistryReader
   - VPNStatusChecker
3. **ConnectivityCheck**: Network connectivity testing
4. **FileLogger**: Console-based logging system
5. **PingAndJitter**: Network performance measurement utilities

### IP Address Classification

The application recognizes:
- VPN ranges: 10.93.x.x (Detroit), 10.94.x.x (Troy), 10.95.x.x (Palo Alto)
- Office networks: Various 10.x.x.x and 172.16.x.x ranges
- DHCP failure: 169.254.x.x (APIPA)

### Core Method

`NetworkIpAddress.GetComputerIpAddress()` returns a `NetworkScanResult` containing:
- Primary IP address
- Work location status (home vs office)
- Map of all relevant IP addresses found
- VPN detection details if applicable

The application prioritizes VPN IP addresses when multiple interfaces are active and handles both IPv4 and IPv6 addresses (though focuses primarily on IPv4 for location determination).

## Development

### Project Structure
```
NetworkCheck/
├── NetworkScanner.sln
├── CLAUDE.md
├── README.md
└── NetworkCheck/
    ├── NetworkCheck.csproj
    ├── Program.cs
    ├── NetworkIpAddress.cs
    ├── ConnectivityCheck.cs
    ├── FileLogger.cs
    ├── Config/
    │   ├── appsettings.json
    │   └── log4net.config
    ├── PingAndJitter/
    │   ├── NetworkPingSettings.cs
    │   ├── PingJitterFileData.cs
    │   ├── PingJitterResultReader.cs
    │   ├── PingJitterResultWriter.cs
    │   └── PingResult.cs
    ├── Tests/
    │   └── PingAndJItter/
    │       └── NetworkPingAndJitterTest.cs
    └── VpnDetection/
        ├── WindowsRegistryReader.cs
        ├── GlobalProtectVPNStatus.cs
        ├── IvantiVPNStatus.cs
        └── VPNStatusChecker.cs
```

### Building in Release Mode
```bash
dotnet build --configuration Release
```

### Running Tests
```bash
dotnet test
```

## License

[Specify your license here]

## Contributing

[Specify contribution guidelines here]