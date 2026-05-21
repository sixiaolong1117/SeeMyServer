# ServerDash

<div align="center">

<img src="SeeMyServer/Assets/StoreLogo.scale-200.png" alt="ServerDash" width="128">

**基于 WinUI 3 的 Windows 服务器监控面板<br/>通过 SSH 实时查看 Linux 主机的 CPU · 内存 · 磁盘 · 网络 · 进程状态**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](SeeMyServer/SeeMyServer.csproj)
[![WinUI 3](https://img.shields.io/badge/WinUI-3-0078D4)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.6-0078D4)](https://aka.ms/windowsappsdk)

[English](README_EN.md) | **简体中文**

</div>

---

## 📖 简介

ServerDash 是一款面向 Windows 桌面的服务器状态监控工具。它通过 SSH 连接远程 Linux 主机，无需在服务器端安装额外 Agent，即可在一个现代化的 WinUI 3 界面中查看多台服务器的实时运行状态。

适合用来快速巡检个人服务器、开发机、NAS、云主机或 OpenWrt 等 Linux 设备的基础负载情况。

## 🖼️ 界面预览

![ServerDash 界面预览](README/1.png)

## ✨ 功能特性

### 🖥️ 服务器监控

- **多服务器看板**：在首页以卡片形式展示所有服务器的 CPU、内存、网络与磁盘速率。
- **实时 SSH 轮询**：默认每秒刷新一次监控数据，并避免同一服务器重复并发请求。
- **Linux 指标采集**：读取 `/proc/stat`、`/proc/meminfo`、`/proc/net/dev`、`/proc/diskstats`、`df`、`top` 等信息。
- **失败保护**：连续 SSH 失败后进入倒计时重试，避免异常主机持续占用请求资源。

### 📊 详情页面

- **系统信息**：显示主机名、运行时长、Linux 内核版本与发行版信息。
- **CPU 详情**：显示整体 CPU 使用率、每核心使用率、User / Sys / Idle / IO 占比，以及 1 / 5 / 15 分钟负载。
- **内存与 Swap**：展示内存、可用内存、缓存、Swap 使用情况。
- **挂载与磁盘 I/O**：列出挂载点、容量、使用率、读写累计值与实时读写速率。
- **网络接口**：列出网卡 RX / TX 累计流量与实时速率。
- **TOP 输出**：直接查看远程主机的 `top -bn1` 输出，便于快速定位负载来源。

### 📋 配置管理

- **添加 / 编辑 / 删除服务器**：保存显示名称、主机地址、端口、系统类型与 SSH 登录信息。
- **导入 / 导出配置**：使用 `.cmsconfig` 文件备份和迁移服务器配置。
- **拖拽排序**：服务器列表顺序会保存到本地设置中。
- **右键快捷操作**：在服务器卡片上右键可打开终端、编辑、删除或导出配置。

### 🔐 SSH 与安全

- **密码登录**：支持 SSH 密码认证，密码会在本地加密保存。
- **SSH Key 登录**：支持导入或粘贴私钥，并通过应用内置密钥库统一管理。
- **密钥保护**：导入的私钥使用当前 Windows 用户的数据保护机制加密保存。
- **密钥元数据**：自动提取公钥、指纹与创建时间，便于识别和管理。
- **打开终端**：可一键启动 PowerShell 并执行对应的 `ssh` 命令进入远程主机。

### 🪟 应用体验

- **WinUI 3 视觉风格**：支持 Mica、Mica Alt、Acrylic 背景材质。
- **中英文界面**：内置简体中文与 English 资源。
- **前台策略**：可设置应用失焦时暂停 SSH 请求，降低后台资源占用。
- **日志管理**：内置日志记录，可在设置页打开日志目录或清除日志。
- **单实例启动**：重复启动时会唤起已有窗口。

## 🚀 快速开始

### 系统要求

- Windows 10 1809 或更高版本
- 可访问目标服务器的 SSH 网络环境
- 目标主机为 Linux 系统，并提供常见 `/proc`、`df`、`top` 等命令输出
- 如果使用“打开终端”功能，本机需要可用的 `ssh` 命令

### 安装

#### 🛒 从 Microsoft Store 获取

[<img src="https://get.microsoft.com/images/zh-cn%20light.svg" width="220" alt="从 Microsoft Store 获取">](https://apps.microsoft.com/detail/9NCVCFL005ZQ)

#### 🛠️ 从源码构建

1. 克隆仓库：

```powershell
git clone https://github.com/SIXiaolong1117/SeeMyServer.git
```

2. 使用 Visual Studio 打开 `SeeMyServer.sln`。
3. 还原 NuGet 包。
4. 选择 `x64`、`x86` 或 `ARM64` 平台后运行或打包。

## 📖 使用指南

### ➕ 添加服务器

1. 点击首页右下角的 **添加** 按钮。
2. 填写显示名称、域名或 IP、SSH 端口。
3. 选择系统类型，目前主要面向 Linux。
4. 填写 SSH 用户名。
5. 选择密码认证或 SSH Key 认证。
6. 点击 **添加**，服务器会出现在首页并开始刷新状态。

### 🔑 使用 SSH Key

| 操作 | 说明 |
|------|------|
| 导入密钥 | 从本地文件选择私钥，导入到应用内置密钥库 |
| 粘贴密钥 | 直接粘贴私钥文本，可自定义密钥名称 |
| 选择密钥 | 添加或编辑服务器时，从密钥列表中选择要使用的私钥 |
| 删除密钥 | 从内置密钥库删除密钥，并清除引用该密钥的服务器记录 |

> 私钥导入后，SSH 监控不再依赖原始文件路径；打开终端时会按需写入临时密钥文件供系统 `ssh` 使用。

### 📈 查看服务器状态

首页会展示每台服务器的概览指标：

- CPU 使用率与负载
- 内存使用率
- 网络上传 / 下载速率
- 磁盘读 / 写速率
- SSH 失败倒计时状态

点击任意服务器卡片可进入详情页，查看更完整的 CPU、内存、磁盘、网络、TOP 与系统信息。

### ⚙️ 管理配置

| 操作 | 入口 |
|------|------|
| 编辑服务器 | 详情页底部 **编辑**，或首页卡片右键 **编辑** |
| 删除服务器 | 首页卡片右键 **删除** |
| 导出配置 | 首页卡片右键 **导出** |
| 导入配置 | 首页右下角 **导入** |
| 打开 SSH 终端 | 详情页底部 **打开终端**，或首页卡片右键 **打开终端** |

## 🏗️ 技术架构

- **UI 框架**：[WinUI 3](https://learn.microsoft.com/windows/apps/winui/winui3/) / [Windows App SDK](https://aka.ms/windowsappsdk)
- **目标框架**：.NET 8.0 Windows
- **SSH 客户端**：[SSH.NET](https://github.com/sshnet/SSH.NET)
- **本地数据库**：[Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/)
- **序列化**：[Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/)
- **控件库**：[Windows Community Toolkit](https://github.com/CommunityToolkit/Windows)
- **打包方式**：MSIX，支持 x86 / x64 / ARM64

## 🔒 隐私

ServerDash 不会收集、使用或分享个人信息。更多说明请查看 [PRIVACY](PRIVACY)。

## 🤝 贡献

欢迎提交 Issue 和 Pull Request：

- 反馈监控数据显示异常
- 补充不同 Linux 发行版或 OpenWrt 的命令输出兼容性
- 改进界面、国际化、日志与配置管理体验

## 📄 许可证

本项目基于 [MIT 许可证](LICENSE) 开源。

## 🙏 致谢

- [Windows App SDK](https://aka.ms/windowsappsdk) — 现代 Windows 桌面应用开发框架
- [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows) — WinUI 控件与工具集
- [SSH.NET](https://github.com/sshnet/SSH.NET) — .NET SSH 客户端库
- [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/) — SQLite 数据存储
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/) — JSON 序列化支持
