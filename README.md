# 🔥 Gateway Tray Manager

**A Windows system tray utility for Inductive Automation Ignition® Gateway**

Monitor and manage your Ignition Gateway service directly from the Windows system tray.

![.NET 8](https://img.shields.io/badge/.NET-8.0-blue)
![Windows](https://img.shields.io/badge/Platform-Windows-lightgrey)
![License](https://img.shields.io/badge/License-MIT-green)
![Target](https://img.shields.io/badge/Target-Gateway%20Service-orange)
![SCADA](https://img.shields.io/badge/SCADA-IIoT-darkgreen)

> **Keywords:** Gateway, SCADA, IIoT, Windows Service, System Tray, Monitor, HMI

---

## 💡 Why This Tool Exists

If you've ever worked with Ignition Gateway, you know the drill: make changes to external configurations, scripts, or resources, and then... **restart the Gateway service**. Again. And again.

After one too many trips to `services.msc` → scroll → right-click → Restart → wait → refresh browser → repeat, I decided to scratch my own itch and build a simple tray utility.

What started as a quick "weekend project" turned out to be surprisingly more useful than I initially expected. Now I can't imagine working without it.

**If you're tired of the restart dance too, this tool is for you.**

---

## ✨ Features

- 🖥️ **Service Monitoring** - Real-time status of the Ignition Gateway service
- 🌐 **Gateway Health Check** - Monitors gateway availability via `/StatusPing` endpoint
- ▶️ **Service Control** - Start, Stop, and Restart the gateway service with progress feedback
- 🔔 **Notifications** - Balloon tips when service status changes
- 🔥 **Status Icons** - Dynamic tray icons showing current status
- ⚙️ **Configuration UI** - Easy configuration without editing files
- 🚀 **Auto-start** - Option to start with Windows
- 🔐 **Basic Auth Support** - Optional authentication for gateway API

## 📸 Tray Icon States

The icon changes color based on status:
| Icon | Status |
|------|--------|
| 🟢 Green flame | Service running, Gateway OK |
| 🟡 Orange flame | Service running but Gateway issue, or operation pending |
| ⚫ Gray flame | Service stopped |
| 🔴 Red flame | Error state |

## 🚀 Installation

### Option 1: Installer (Recommended)
1. Download `GatewayTrayManager_Setup_x.x.x.exe` from [Releases](../../releases)
2. Run the installer (requires Administrator)
3. Choose installation options:
   - Desktop shortcut
   - Start with Windows

### Option 2: Portable
1. Download the portable ZIP from [Releases](../../releases)
2. Extract to any folder
3. Run `GatewayTrayManager.exe` as Administrator

## ⚙️ Configuration

Right-click tray icon → **Configuration...**

| Setting | Description | Default |
|---------|-------------|---------|
| Service Name | Windows service name | `Ignition` |
| Gateway URL | Gateway base URL | `http://localhost:8088` |
| Poll Interval | Status check interval (ms) | `3000` |
| HTTP Timeout | API timeout (seconds) | `2` |
| Username | (Optional) Basic auth username | - |
| Password | (Optional) Basic auth password | - |
| Auto-start | Start with Windows | Off |

Configuration is saved to `appsettings.json`:

```json
{
  "Gateway": {
    "ServiceName": "Ignition",
    "GatewayBaseUrl": "http://localhost:8088",
    "PollIntervalMs": 3000,
    "HttpTimeoutSeconds": 2,
    "Username": null,
    "Password": null
  }
}
```

## 🔧 Building from Source

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11
- (Optional) [Inno Setup 6](https://jrsoftware.org/isdl.php) for building installer

### Build & Run
```powershell
# Clone repository
git clone https://github.com/n-car/GatewayTrayManager.git
cd GatewayTrayManager

# Restore and build
dotnet restore
dotnet build -c Release

# Run
dotnet run --project src/GatewayTrayManager/GatewayTrayManager.csproj
```

### Publish Self-Contained
```powershell
dotnet publish src/GatewayTrayManager/GatewayTrayManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Build Installer
```powershell
cd installer
.\build.ps1
# Or manually:
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" GatewayTrayManager.iss
```

Output: `installer/output/GatewayTrayManager_Setup_1.0.0.exe`

## 📁 Project Structure

```
GatewayTrayManager/
├── src/
│   ├── ServiceManager/              # Reusable service monitoring library
│   │   ├── ServiceMonitor.cs        # Base service monitoring
│   │   ├── ServiceOperationForm.cs  # UI for service operations
│   │   ├── ElevationHelper.cs       # UAC elevation support
│   │   ├── ServiceControlHelper.cs  # Elevated service control
│   │   ├── TrayIconGenerator.cs     # Dynamic icon generation
│   │   └── ServiceConfig.cs         # Base configuration
│   │
│   └── GatewayTrayManager/          # Main application
│       ├── Program.cs               # Entry point with service-control mode
│       ├── TrayAppContext.cs        # Tray icon and menu
│       ├── GatewayMonitor.cs        # Service + HTTP monitoring
│       ├── ConfigForm.cs            # Configuration dialog
│       ├── ServiceOperationForm.cs  # Gateway-specific operations
│       └── Resources/app.ico        # Application icon
│
├── installer/                       # Inno Setup installer
│   ├── GatewayTrayManager.iss
│   └── output/
│
└── README.md
```

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  GatewayTrayManager (Application)                           │
│  ├── TrayAppContext      → System tray icon & menu          │
│  ├── GatewayMonitor      → Service + HTTP health monitoring │
│  ├── GatewayServiceOperationForm → Operations with Gateway  │
│  │                          health check after start        │
│  └── ConfigForm          → User configuration               │
└─────────────────────────────────────────────────────────────┘
                              │ inherits
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  ServiceManager (Reusable Library)                          │
│  ├── ServiceMonitor       → Windows service monitoring      │
│  ├── ServiceOperationForm → Base UI for operations          │
│  ├── ElevationHelper      → UAC privilege detection/request │
│  ├── ServiceControlHelper → Elevated process for svc ctrl   │
│  └── TrayIconGenerator    → Dynamic icon generation         │
└─────────────────────────────────────────────────────────────┘
```

## 🔐 Permissions & UAC

The application uses **on-demand elevation** for a better user experience:

| Operation | Requires Admin | UAC Prompt |
|-----------|---------------|------------|
| View service status | ❌ No | No |
| View Gateway HTTP status | ❌ No | No |
| Start / Stop / Restart service | ✅ Yes | Yes, when needed |

**How it works:**
- The app runs as a **normal user** (no UAC at startup)
- When you request a service operation, it **automatically elevates** via UAC
- A separate elevated process performs the operation
- The main app remains at normal privileges

This approach is more secure than running the entire app as Administrator.

## 📝 Troubleshooting

If the application crashes, check `crash.log` in the installation directory for details.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## ⚠️ Disclaimer

**This is an independent, community-developed tool.**

- This project is **NOT affiliated with, endorsed by, or sponsored by Inductive Automation, LLC**.
- "Ignition" is a registered trademark of **Inductive Automation, LLC**.
- This tool is provided "as is" without warranty of any kind.
- For official Ignition products and support, visit [inductiveautomation.com](https://inductiveautomation.com).

---

## 🙏 Acknowledgments

- Built for use with [Ignition by Inductive Automation](https://inductiveautomation.com)
- Special thanks to the frustration of clicking through `services.msc` one too many times 😅

**This is a personal project born from a real need.** If you find it useful, have suggestions, or encounter any issues, feel free to open an issue or submit a PR. Any feedback is welcome!

---

<p align="center">
  <i>Made with ❤️ and a healthy dose of impatience</i>
</p>
