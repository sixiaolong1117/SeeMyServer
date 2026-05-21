# ServerDash

<div align="center">

<img src="SeeMyServer/Assets/StoreLogo.scale-200.png" alt="ServerDash" width="128">

**A WinUI 3 server monitoring dashboard for Windows<br/>Monitor CPU · memory · disk · network · process status on Linux hosts over SSH**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](SeeMyServer/SeeMyServer.csproj)
[![WinUI 3](https://img.shields.io/badge/WinUI-3-0078D4)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.6-0078D4)](https://aka.ms/windowsappsdk)

[简体中文](README.md) | **English**

</div>

---

## 📖 Introduction

ServerDash is a Windows desktop app for monitoring server status. It connects to remote Linux hosts over SSH and shows real-time status for multiple servers in a modern WinUI 3 interface, without requiring any extra agent on the server side.

It is useful for quickly checking personal servers, development machines, NAS devices, cloud servers, OpenWrt devices, and other Linux-based hosts.

## 🖼️ Preview

![ServerDash preview](README/1.png)

## ✨ Features

### 🖥️ Server Monitoring

- **Multi-server dashboard**: Display CPU, memory, network, and disk throughput for all servers as cards on the home page.
- **Real-time SSH polling**: Refresh monitoring data once per second by default while avoiding duplicate concurrent requests to the same server.
- **Linux metrics collection**: Read data from `/proc/stat`, `/proc/meminfo`, `/proc/net/dev`, `/proc/diskstats`, `df`, `top`, and related commands.
- **Failure protection**: Enter a retry countdown after repeated SSH failures to avoid constantly polling unavailable hosts.

### 📊 Detail Page

- **System information**: Show host name, uptime, Linux kernel version, and distribution information.
- **CPU details**: Show total CPU usage, per-core usage, User / Sys / Idle / IO percentages, and 1 / 5 / 15 minute load averages.
- **Memory and Swap**: Display memory usage, available memory, cache, and Swap usage.
- **Mounts and disk I/O**: List mount points, capacity, usage, accumulated read/write values, and real-time read/write speeds.
- **Network interfaces**: List RX / TX traffic totals and real-time speeds for each interface.
- **TOP output**: View the raw `top -bn1` output from the remote host to quickly locate load sources.

### 📋 Configuration Management

- **Add / edit / delete servers**: Save display name, host address, port, OS type, and SSH login information.
- **Import / export configurations**: Back up and migrate server configurations with `.cmsconfig` files.
- **Drag-and-drop ordering**: Server list order is saved in local settings.
- **Context menu actions**: Right-click a server card to open a terminal, edit, delete, or export the configuration.

### 🔐 SSH and Security

- **Password login**: Supports SSH password authentication, with passwords encrypted locally.
- **SSH key login**: Supports importing or pasting private keys and managing them in the built-in key store.
- **Key protection**: Imported private keys are encrypted with the current Windows user's data protection scope.
- **Key metadata**: Automatically extracts public key, fingerprint, and creation time for easier identification.
- **Open terminal**: Launch PowerShell and run the corresponding `ssh` command to enter the remote host.

### 🪟 App Experience

- **WinUI 3 style**: Supports Mica, Mica Alt, and Acrylic background materials.
- **Chinese and English UI**: Built-in Simplified Chinese and English resources.
- **Foreground policy**: Optionally pause SSH requests when the app loses focus to reduce background resource usage.
- **Log management**: Built-in logging, with settings to open the log directory or clear logs.
- **Single-instance startup**: Launching the app again brings the existing window to the foreground.

## 🚀 Quick Start

### Requirements

- Windows 10 version 1809 or later
- Network access to the target server over SSH
- A Linux target host with common `/proc`, `df`, and `top` outputs
- A usable local `ssh` command if you want to use the "Open Terminal" feature

### Installation

#### 🛒 Get It from Microsoft Store

[<img src="https://get.microsoft.com/images/en-us%20light.svg" width="220" alt="Get it from Microsoft Store">](https://apps.microsoft.com/detail/9NCVCFL005ZQ)

#### 🛠️ Build from Source

1. Clone the repository:

```powershell
git clone https://github.com/SIXiaolong1117/SeeMyServer.git
```

2. Open `SeeMyServer.sln` with Visual Studio.
3. Restore NuGet packages.
4. Select `x64`, `x86`, or `ARM64`, then run or package the app.

## 📖 Usage Guide

### ➕ Add a Server

1. Click the **Add** button in the lower-right corner of the home page.
2. Enter the display name, domain or IP address, and SSH port.
3. Select the OS type. The app currently mainly targets Linux.
4. Enter the SSH user name.
5. Choose password authentication or SSH key authentication.
6. Click **Add**. The server will appear on the home page and start refreshing its status.

### 🔑 Use SSH Keys

| Action | Description |
|------|------|
| Import key | Select a private key file and import it into the built-in key store |
| Paste key | Paste private key text directly and optionally set a custom key name |
| Select key | Choose a private key from the key list when adding or editing a server |
| Delete key | Delete a key from the built-in key store and clear server records that reference it |

> After a private key is imported, SSH monitoring no longer depends on the original file path. When opening a terminal, ServerDash writes a temporary key file for the system `ssh` command as needed.

### 📈 View Server Status

The home page shows overview metrics for each server:

- CPU usage and load
- Memory usage
- Network upload / download speed
- Disk read / write speed
- SSH failure retry countdown

Click any server card to open the detail page, where you can view fuller CPU, memory, disk, network, TOP, and system information.

### ⚙️ Manage Configurations

| Action | Entry |
|------|------|
| Edit server | **Edit** at the bottom of the detail page, or right-click a server card and choose **Edit** |
| Delete server | Right-click a server card and choose **Delete** |
| Export configuration | Right-click a server card and choose **Export** |
| Import configuration | Click **Import** in the lower-right corner of the home page |
| Open SSH terminal | **Open Terminal** at the bottom of the detail page, or right-click a server card and choose **Open Terminal** |

## 🏗️ Architecture

- **UI framework**: [WinUI 3](https://learn.microsoft.com/windows/apps/winui/winui3/) / [Windows App SDK](https://aka.ms/windowsappsdk)
- **Target framework**: .NET 8.0 Windows
- **SSH client**: [SSH.NET](https://github.com/sshnet/SSH.NET)
- **Local database**: [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/)
- **Serialization**: [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/)
- **Controls**: [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- **Packaging**: MSIX, with x86 / x64 / ARM64 support

## 🔒 Privacy

ServerDash does not collect, use, or share personal information. See [PRIVACY](PRIVACY) for details.

## 🤝 Contributing

Issues and pull requests are welcome:

- Report inaccurate monitoring data
- Improve compatibility with command output from different Linux distributions or OpenWrt
- Improve the UI, localization, logging, and configuration management experience

## 📄 License

This project is open source under the [MIT License](LICENSE).

## 🙏 Acknowledgements

- [Windows App SDK](https://aka.ms/windowsappsdk) — Modern Windows desktop app development framework
- [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows) — WinUI controls and utilities
- [SSH.NET](https://github.com/sshnet/SSH.NET) — .NET SSH client library
- [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/) — SQLite data storage
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/) — JSON serialization support
