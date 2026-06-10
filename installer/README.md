# Gateway Tray Manager - Installer

This folder contains the files needed to build the installer for Gateway Tray Manager.

## Prerequisites

1. **.NET 8 SDK** - https://dotnet.microsoft.com/download/dotnet/8.0
2. **Inno Setup 6** - https://jrsoftware.org/isdl.php (free)

## Building the Installer

### Option 1: Using PowerShell Script (Recommended)

```powershell
# From the repository root
cd installer
.\build.ps1
```

This will:
1. Build and publish the application as self-contained
2. Create the installer using Inno Setup
3. Output the installer to `installer/output/`

### Option 2: Manual Build

1. **Publish the application from the repository root:**
   ```powershell
   dotnet publish src\GatewayTrayManager\GatewayTrayManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```

2. **Open Inno Setup Compiler** and compile `installer\GatewayTrayManager.iss`

## Installer Features

- ✅ Self-contained (no .NET runtime required on target machine)
- ✅ x64-compatible installer
- ✅ Requires administrator privileges (for Program Files and HKLM startup registry)
- ✅ Creates Start Menu shortcuts
- ✅ Optional Desktop shortcut
- ✅ Optional Windows startup entry
- ✅ Preserves existing `appsettings.json` during upgrades
- ✅ Clean uninstall
- ✅ Multi-language support (English, Italian)

## Output

The installer will be created in `installer/output/`:
- `GatewayTrayManager_Setup_1.1.2.exe`
