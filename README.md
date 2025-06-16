# NetworkCheck - Network Scanner and Monitoring Tool

A comprehensive C# .NET 9.0 console application for network interface scanning, IP address detection, VPN status monitoring, and network performance measurement.

## Features

- **IP Address Detection**: Scans all network interfaces to identify active IP addresses
- **VPN Detection**: Automatically detects VPN connections (GlobalProtect and Ivanti)
- **Work Location Determination**: Identifies whether you're working from home (VPN) or office
- **Network Performance Monitoring**: Measures ping times and jitter to various hosts
- **Continuous Monitoring**: Runs every 3 minutes and logs results to files
- **WSL Interface Filtering**: Automatically excludes WSL network interfaces

## Prerequisites

- .NET 9.0 SDK
- Windows OS (due to Windows Registry access for VPN detection)
- Administrator privileges may be required for some network operations

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
1. Scan network interfaces every 3 minutes
2. Display IP addresses and VPN status
3. Run ping tests to multiple hosts
4. Save results to text files in the `results` directory

### Output Files

The application creates two CSV files in the `results` directory:
- `median_ping_results.txt`: Contains median ping times for each host
- `jitter_results.txt`: Contains jitter measurements for each host

Format:
```
Timestamp,Host,Value(ms)
2024-01-16 14:30:00,google.com,25.50
```

## Architecture

### Main Components

1. **NetworkScanner**: Core IP address scanning and detection
2. **VpnDetection**: Windows Registry-based VPN status detection
   - GlobalProtectVPNStatus
   - IvantiVPNStatus
   - WindowsRegistryReader
3. **NetworkPingAndJitterTest**: Network performance measurement
4. **FileLogger**: Dual console/file logging system

### IP Address Classification

The application recognizes:
- VPN ranges: 10.93.x.x (Detroit), 10.94.x.x (Troy), 10.95.x.x (Palo Alto)
- Office networks: 10.x.x.x and 172.16.x.x ranges
- DHCP failure: 169.254.x.x (APIPA)

## Logging

Logs are saved to:
- Primary location: `<app-directory>/logs/network_scan_YYYYMMDD_HHMMSS.log`
- Fallback location: `%TEMP%/NetworkCheckLogs/` (if no write permissions)

## Configuration

### Hosts Tested
- External: google.com
- Internal AES: RCD2AES601.mi.corp.rockfin.com, RCD1AES601.mi.corp.rockfin.com
- Internal: git.rockfin.com

### Timing
- Scan interval: 3 minutes
- Ping count per host: 10
- Ping interval: 1 second
- Ping timeout: 5 seconds

## Stopping the Application

Press `Ctrl+C` to gracefully shut down the application.

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
    ├── NetworkPingAndJitterTest.cs
    ├── ConnectivityCheck.cs
    ├── PingResult.cs
    ├── FileLogger.cs
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

## Known Issues

- VPN detection only works on Windows due to registry access
- Some network operations may require administrator privileges
- WSL interfaces are automatically excluded

## License

[Specify your license here]

## Contributing

[Specify contribution guidelines here]