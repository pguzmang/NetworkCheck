# Simple PowerShell script to test WiFi detection
Write-Host "Testing WiFi Detection..." -ForegroundColor Cyan

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build NetworkCheck/NetworkCheck.csproj

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful!" -ForegroundColor Green
    
    # Test netsh command that our code uses
    Write-Host "`nTesting netsh wlan show interfaces:" -ForegroundColor Yellow
    netsh wlan show interfaces | Select-String "SSID"
    
    Write-Host "`nYou can now run the application with:" -ForegroundColor Cyan
    Write-Host "dotnet run --project NetworkCheck/NetworkCheck.csproj" -ForegroundColor White
} else {
    Write-Host "Build failed!" -ForegroundColor Red
}