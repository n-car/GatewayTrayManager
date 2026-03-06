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

1. **Publish the application:**
   ```powershell
   dotnet publish src\GatewayTrayManager\GatewayTrayManager.csproj -c Release -r win-x64 --self-contained true
   ```

2. **Open Inno Setup Compiler** and compile `GatewayTrayManager.iss`

## Installer Features

- ✅ Self-contained (no .NET runtime required on target machine)
- ✅ Requires administrator privileges (for service management)
- ✅ Creates Start Menu shortcuts
- ✅ Optional Desktop shortcut
- ✅ Optional Windows startup entry
- ✅ Clean uninstall
- ✅ Multi-language support (English, Italian)

## Output

The installer will be created in `installer/output/`:
- `GatewayTrayManager_Setup_1.0.0.exe`

## Customization

Edit `GatewayTrayManager.iss` to change:
- `MyAppVersion` - Application version
- `MyAppPublisher` - Your company name
- `MyAppURL` - Your website/repository URL
- `AppId` - Unique GUID for the application

## Icon

Place your application icon (`app.ico`) in:
- `src\GatewayTrayManager\Resources\app.ico`

If not provided, the installer will use the default icon.
