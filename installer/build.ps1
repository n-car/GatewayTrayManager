# Build and Create Installer Script for Gateway Tray Manager
# Requires: .NET 8 SDK, Inno Setup 6.x

param(
    [switch]$SkipBuild,
    [switch]$SkipInstaller,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir
$ProjectDir = Join-Path $RootDir "src\GatewayTrayManager"
$ProjectFile = Join-Path $ProjectDir "GatewayTrayManager.csproj"
$PublishDir = Join-Path $ProjectDir "bin\$Configuration\net8.0-windows\win-x64\publish"
$InstallerScript = Join-Path $ScriptDir "GatewayTrayManager.iss"
$OutputDir = Join-Path $ScriptDir "output"

# Colors for output
function Write-Step($message) {
    Write-Host "`n=== $message ===" -ForegroundColor Cyan
}

function Write-Success($message) {
    Write-Host "✓ $message" -ForegroundColor Green
}

function Write-Error($message) {
    Write-Host "✗ $message" -ForegroundColor Red
}

# Banner
Write-Host @"

╔══════════════════════════════════════════════════════════╗
║          Gateway Tray Manager - Build Script            ║
╚══════════════════════════════════════════════════════════╝

"@ -ForegroundColor Yellow

# Step 1: Build and Publish
if (-not $SkipBuild) {
    Write-Step "Building and Publishing Application"
    
    # Clean previous publish
    if (Test-Path $PublishDir) {
        Write-Host "Cleaning previous publish output..."
        Remove-Item -Path $PublishDir -Recurse -Force
    }
    
    # Restore packages
    Write-Host "Restoring NuGet packages..."
    dotnet restore $ProjectFile
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to restore packages"
        exit 1
    }
    
    # Build and publish self-contained
    Write-Host "Publishing self-contained application..."
    dotnet publish $ProjectFile `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $PublishDir
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish application"
        exit 1
    }
    
    Write-Success "Application published to: $PublishDir"
    
    # List published files
    Write-Host "`nPublished files:"
    Get-ChildItem $PublishDir | ForEach-Object {
        $size = "{0:N2} MB" -f ($_.Length / 1MB)
        Write-Host "  - $($_.Name) ($size)"
    }
}
else {
    Write-Host "Skipping build step..." -ForegroundColor Yellow
}

# Step 2: Create Installer
if (-not $SkipInstaller) {
    Write-Step "Creating Installer"
    
    # Check if Inno Setup is installed
    $InnoSetupPath = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    
    if (-not $InnoSetupPath) {
        Write-Host "`nInno Setup 6 not found!" -ForegroundColor Yellow
        Write-Host "Please install Inno Setup 6 from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
        Write-Host "`nAlternatively, you can manually run the installer script:" -ForegroundColor Yellow
        Write-Host "  1. Open Inno Setup Compiler" -ForegroundColor White
        Write-Host "  2. Open file: $InstallerScript" -ForegroundColor White
        Write-Host "  3. Click 'Compile' or press Ctrl+F9" -ForegroundColor White
        exit 0
    }
    
    Write-Host "Found Inno Setup at: $InnoSetupPath"
    
    # Create output directory
    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir | Out-Null
    }
    
    # Check if published files exist
    if (-not (Test-Path $PublishDir)) {
        Write-Error "Published files not found at: $PublishDir"
        Write-Host "Run the script without -SkipBuild first" -ForegroundColor Yellow
        exit 1
    }
    
    # Run Inno Setup Compiler
    Write-Host "Compiling installer..."
    Push-Location $RootDir
    & $InnoSetupPath $InstallerScript
    $InnoResult = $LASTEXITCODE
    Pop-Location
    
    if ($InnoResult -ne 0) {
        Write-Error "Failed to create installer"
        exit 1
    }
    
    Write-Success "Installer created successfully!"
    
    # Show output file
    $InstallerFile = Get-ChildItem $OutputDir -Filter "*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($InstallerFile) {
        $size = "{0:N2} MB" -f ($InstallerFile.Length / 1MB)
        Write-Host "`nInstaller file:"
        Write-Host "  📦 $($InstallerFile.FullName)" -ForegroundColor Green
        Write-Host "  Size: $size"
    }
}
else {
    Write-Host "Skipping installer creation..." -ForegroundColor Yellow
}

# Done
Write-Host @"

╔══════════════════════════════════════════════════════════╗
║                    Build Complete!                       ║
╚══════════════════════════════════════════════════════════╝

"@ -ForegroundColor Green

if (-not $SkipInstaller -and (Test-Path $OutputDir)) {
    Write-Host "Output directory: $OutputDir"
    explorer $OutputDir
}
