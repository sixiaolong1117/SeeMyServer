# AGENTS.md — SeeMyServer (ServerDash)

## 项目概述

基于 WinUI 3 (Windows App SDK 1.6) 的 Windows 桌面应用，通过 SSH 连接远程 Linux 主机实现实时监控（CPU / 内存 / 磁盘 / 网络 / 进程）。单项目解决方案，无多包边界。

## 构建与运行

```powershell
# 必须使用 Visual Studio（非 dotnet CLI）打开
# 用 VS 打开 SeeMyServer.sln，选择 x64/x86/ARM64 平台后 F5 运行

# 或通过命令行构建（需要 MSBuild 或 dotnet build）
dotnet build SeeMyServer.sln -p:Platform=x64 -p:Configuration=Debug
```

- **目标框架**: `net8.0-windows10.0.22621.0`，最低 Windows 10 1809 (10.0.17763.0)
- **平台**: x86 / x64 / ARM64，必须指定平台构建
- **打包方式**: MSIX（`EnableMsixTooling=true`），也可 Unpackaged 运行
- **无 CI/CD 配置**，无自动测试、lint 或格式化工具

## 关键依赖

| 包 | 用途 |
|---|---|
| `SSH.NET` 2024.2.0 | SSH 连接远程主机 |
| `Microsoft.Data.Sqlite` 9.0.0 | 本地数据库存储服务器配置和 SSH 密钥 |
| `Newtonsoft.Json` 13.0.3 | JSON 序列化（配置导入导出） |
| `Windows App SDK` 1.6.241114003 | WinUI 3 框架 |
| `CommunityToolkit.WinUI` 7.1.2 | RadialGauge 等 UI 控件 |
| `PInvoke.User32` 0.7.124 | Win32 API 调用（窗口操作） |

## 项目结构

```
SeeMyServer/
├── App.xaml.cs              # 入口，单实例守护，日志初始化
├── MainWindow.xaml.cs       # 主窗口，Mica/Acrylic 背景，NavigationView 导航
├── Pages/
│   ├── HomePage.xaml.cs     # 首页：服务器卡片看板，轮询刷新
│   ├── DetailPage.xaml.cs   # 详情页：单台服务器完整监控
│   ├── SettingsPage.xaml.cs # 设置页：背景材质切换
│   ├── About.xaml.cs        # 关于页
│   └── Dialogs/
│       ├── AddServer.xaml.cs       # 添加/编辑服务器对话框
│       └── ManageSSHKeys.xaml.cs   # SSH 密钥管理对话框
├── Methods/
│   ├── Method.cs            # 核心业务：SSH 命令发送、CPU/内存/磁盘/网络解析、配置导入导出
│   ├── SSHKeyMethod.cs      # SSH 密钥导入、解析、保存到数据库
│   └── SSHKeyProtection.cs  # SSH 密钥加解密保护
├── Helper/
│   ├── SQLiteHelper.cs      # SQLite 数据库操作（CMSTable + SSHKeyTable），含版本迁移
│   ├── LoggerHelper.cs      # 日志记录（最大 1MB）
│   └── WindowsHelloHelper.cs # Windows Hello 集成
├── Models/
│   ├── CMSModel.cs          # 服务器数据模型
│   ├── SSHKeyModel.cs       # SSH 密钥模型
│   ├── MountInfo.cs         # 磁盘挂载信息模型
│   └── NetworkInterfaceInfo.cs # 网卡信息模型
├── Language/                # 国际化资源
│   ├── en-US/Resources.resw
│   └── zh-CN/Resources.resw
└── Assets/                  # 应用图标和图片资源
```

## 架构要点

- **数据流**: SSH 轮询 (1秒间隔) → 解析 /proc/stat、/proc/meminfo、/proc/net/dev、df、top → 更新 CMSModel → UI 绑定
- **单实例**: `App.xaml.cs` 通过 `AppInstance.FindOrRegisterForKey` 保证只运行一个实例
- **数据库**: SQLite `cms.db`，自动建表和版本迁移（当前版本 2），存储服务器配置和 SSH 密钥
- **SSH 密钥**: 私钥存入数据库时经过 `SSHKeyProtection` 加密，不再依赖文件路径
- **密码加密**: 使用 AES 对称加密存储 SSH 密码，密钥从 `ApplicationData.Current.LocalSettings` 读取
- **背景材质**: 支持 Mica / Mica Alt / Acrylic 三种 Windows 系统背景，通过设置页切换

## 注意事项

- **SSH 命令**: 远程执行的命令硬编码在 `Method.cs` 的 `GetLinuxCPUUsageAsync` 中（`cat /proc/stat`、`top -bn1` 等），修改需同步两组采样
- **OpenWRT 兼容**: `top` 输出格式不同，负载解析和主机名获取有特殊分支处理
- **`AllowUnsafeBlocks`**: 项目启用了 unsafe 代码
- **`SeeMyServer.Datas` 命名空间**: `SQLiteHelper.cs` 实际位于 `Helper/` 目录但使用 `SeeMyServer.Datas` 命名空间，勿混淆
- **无单元测试**: 项目无测试项目或测试框架，修改核心解析逻辑需手动验证
