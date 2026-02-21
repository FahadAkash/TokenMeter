$version = "0.1.0"
$buildDir = "bin/publish"

Write-Host "Publishing TokenMeter v$version..." -ForegroundColor Green

if (Test-Path $buildDir) { Remove-Item -Recurse -Force $buildDir }

dotnet publish src/TokenMeter.UI/TokenMeter.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $buildDir /p:DebugType=None /p:DebugSymbols=false

Write-Host "Build complete. Creating ZIP..." -ForegroundColor Cyan
if (Test-Path "TokenMeter-v$version-win-x64.zip") { Remove-Item "TokenMeter-v$version-win-x64.zip" }
Compress-Archive -Path "$buildDir/*" -DestinationPath "TokenMeter-v$version-win-x64.zip"

Write-Host "Artifact created: TokenMeter-v$version-win-x64.zip" -ForegroundColor Green
