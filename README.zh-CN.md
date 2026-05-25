<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

# [optimizerDuck](https://optimizerduck.vercel.app/)

**optimizerDuck 是一款免费开源的 Windows 优化工具，主打性能提升、隐私保护与简洁易用。**

[![Release](https://img.shields.io/github/release/itsfatduck/optimizerDuck?color=fed114&label=%E5%8F%91%E5%B8%83&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/itsfatduck/optimizerDuck/total?label=%E4%B8%8B%E8%BD%BD%E9%87%8F&style=flat-square&color=lightgreen)](https://github.com/itsfatduck/optimizerDuck/releases)
[![Stars](https://img.shields.io/endpoint?url=https://api.pinstudios.net/api/badges/stars/itsfatduck/optimizerDuck&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/stargazers)
[![License](https://img.shields.io/badge/%E6%8E%88%E6%9D%83-GPL_v3-red?style=flat-square)](./LICENSE)
[![Discord](https://img.shields.io/discord/1091675679994675240?color=5865f2&label=Discord&style=flat-square)](https://discord.gg/tDUBDCYw9Q)
<br>
[![CI](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml/badge.svg)](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml)
[![.NET Latest](https://img.shields.io/badge/.NET_Runtime-%E6%9C%80%E6%96%B0-ef99dd?style=flat-square)](https://dotnet.microsoft.com/en-us/download)
[![Supported OS](https://img.shields.io/badge/%E6%94%AF%E6%8C%81%E7%B3%BB%E7%BB%9F-Windows_10%2B_x64-0078d4?style=flat-square)](https://www.microsoft.com/en-us/software-download/)

**[快速上手](https://optimizerduck.vercel.app/docs/guides/getting-started) | [工作原理](https://optimizerduck.vercel.app/docs/guides/how-it-works) | [常见问题](https://optimizerduck.vercel.app/docs/faq/general)**

[English](README.md) | [Tiếng Việt](README.vi.md) | [繁體中文](README.zh-TW.md) | **简体中文** | [Русский](README.ru-RU.md)

<details>
<summary>⭐ 项目星标趋势</summary>

如果 optimizerDuck 帮你优化了电脑，不妨给项目点个 ⭐ Star，并分享给更多人。
每一个星标都是持续更新优化的动力。

<a href="https://www.star-history.com/#itsfatduck/optimizerDuck&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&legend=top-left" />
 </picture>
</a>

</details>

<img src="./.github/assets/app.png" alt="optimizerDuck 深色模式" title="optimizerDuck 深色模式" width="800"/>

</div>

---

## 快速开始

1. 前往 **[GitHub Releases](https://github.com/itsfatduck/optimizerDuck/releases/latest)** 下载
2. 直接运行 `.exe` 程序，**无需安装**
3. 挑选要应用的优化项目，套用后重启电脑

> [!TIP]
> 修改系统设置前，建议**手动创建系统还原点**。

> [!NOTE]
> 目前支持英语、越南语、繁体中文（由 [@abc0922001](https://github.com/abc0922001) 贡献）、简体中文（由 [@wcxu21](https://github.com/wcxu21) 贡献）和俄语（由 [@Foodhead](https://github.com/Foodhead) 贡献）。
> 想要添加其他语言？请查看[贡献指南](./CONTRIBUTING.md)。

---

## optimizerDuck 做什么

Windows 自带了不少你可能用不到的东西：后台服务、遥测数据、预装应用、开机启动程序以及计划任务，这些都在悄悄消耗系统资源。optimizerDuck 提供一个统一的界面来清理这些内容。

它通过有针对性的系统调整来减少资源占用和阻止不需要的行为，同时内置多款管理工具，让你清楚知道哪些程序在运行、哪些可以移除，并在出现问题时还原任何更改。

> [!NOTE]
> 所有优化均可手动执行。optimizerDuck 只是让你更轻松地套用这些优化。

### 系统优化

超过 30 项调整，分为 6 大类别。每项都有清晰说明和风险评级，让你在套用前就了解它会做什么。

| 类别               | 涵盖内容                                                                                                |
| :----------------- | :------------------------------------------------------------------------------------------------------ |
| **性能**           | 根据你的内存容量调整 Service Host、进程优先级调整、降低键盘延迟、多媒体调度器优化以获得更流畅的游戏体验 |
| **隐私**           | 关闭 Windows 遥测、错误报告、广告 ID、位置追踪、Cortana、Copilot 及内容推送建议                         |
| **显卡**           | 针对 AMD、NVIDIA、Intel 显卡的专属注册表调整，涵盖电源状态、时钟门控和显示延迟                          |
| **电源**           | 关闭休眠和快速启动、关闭 USB 选择性暂停、安装高性能自定义电源计划、关闭电源节流                         |
| **冗余软件与服务** | 阻止 OEM 应用重新安装行为，并精细调整 200 多项 Windows 服务的启动类型                                   |
| **使用体验**       | 移除菜单显示延迟、关闭任务栏动画和透明度等视觉效果，让系统响应更迅速                                    |

> [!IMPORTANT]
> 如果你觉得优化项目不多，别急着认为它们没效果或过时了。optimizerDuck 只专注于那些经过测试、基准验证或社区广泛信赖的优化。部分变更可能不会立即产生明显差异，但长期下来仍能帮助你的系统更稳定、更顺畅地运行。

### 功能开关

直接打开或关闭 Windows 设置，不用翻注册表或上网搜教程。分为四个区块：

- **桌面**：显示或隐藏图标（此电脑、回收站、网络、用户文件夹、控制面板），去除快捷方式小箭头
- **任务栏**：居中或靠左、切换小组件、任务视图按钮、结束任务按钮、时钟显示秒数、关闭开始菜单中的必应搜索
- **资源管理器**：文件扩展名、隐藏文件、剪贴板历史、紧凑模式、窗口贴靠、项目复选框、经典右键菜单等
- **游戏**：游戏模式、游戏栏、后台录制、鼠标加速度、全屏优化、硬件加速 GPU 调度
- **系统**：开机自动开启数字小键盘

### 内置工具

| 工具             | 功能                                                                                                                     |
| :--------------- | :----------------------------------------------------------------------------------------------------------------------- |
| **系统面板**     | 在一个面板中查看 CPU、内存、显卡、存储磁盘和操作系统详情                                                                 |
| **启动项管理**   | 查看所有开机启动的应用和任务，切换启用/禁用，打开文件位置                                                                |
| **计划任务**     | 浏览、运行、停止、启用、禁用或删除 Windows 计划任务                                                                      |
| **磁盘清理**     | 扫描并清理临时文件、系统缓存、Windows Update 残留文件、Prefetch、缩略图缓存、回收站、崩溃转储文件及旧版 Windows 安装文件 |
| **预装软件卸载** | 列出所有可移除的 AppX 包并附带风险标签（安全、谨慎、未知），让你自行选择要删除的内容                                     |

---

## 安全保障

我们深知修改系统设置存在风险，因此工具的设计核心围绕在可还原性和用户控制权。

有关数据处理方式的详细信息，请参阅[隐私政策](./PRIVACY.md)。

- **自动备份**：每次更改都会将还原文件写入本地文件夹。你可以还原单项调整或全部回滚
- **一键还原**：从界面直接点击即可撤销已套用的任何优化
- **风险分级**：每项优化标注安全、中等或高风险等级，根据其潜在影响程度分类
- **不默认套用**：在你手动选择之前不会执行任何操作。工具不会自行启用任何更改
- **还原点提醒**：在套用第一个优化之前，应用会建议你创建一个 Windows 还原点

---

## 技术细节

- **框架**：WPF 搭配 .NET 10，使用 WPF UI 库实现 Fluent 设计
- **还原系统**：四种还原步骤类型（注册表、服务、计划任务、Shell），以 JSON 存储状态并采用线程安全的文件 I/O
- **主题**：深色（默认）、浅色和高对比度模式，支持 Mica 背景效果
- **免安装**：以单一 .exe 运行，无需安装
- **备份系统**：每次更改均以本地文件夹备份，支持一键还原
- **自动探索**：优化与功能类别通过 reflection + 自定义属性自动探索，无需手动注册
- **无遥测**：应用不收集任何用户数据

---

## 官方文档

### [使用文档中心](https://optimizerduck.vercel.app/docs/guides/getting-started)

包含分步教程、每项优化的详细说明，以及使用 optimizerDuck 的最佳实践。

---

## 贡献

我们欢迎来自社区的每一份贡献！无论你是修复 bug、添加新的优化或功能、完善文档，还是协助将应用翻译成其他语言，你的支持都让我们非常感激。

如需更多信息，请查阅 [CONTRIBUTING.md](./CONTRIBUTING.md)。

---

## 社区交流

> [!TIP]
> 加入 Discord 服务器，获取使用帮助、分享优化经验，并与其他用户和项目开发者交流。
>
> <a href="https://discord.gg/tDUBDCYw9Q"><img src="https://discord.com/api/guilds/1091675679994675240/widget.png?style=banner2" alt="Discord 社区横幅"/></a>

如果你觉得本工具好用：

- ⭐ 给项目点亮星标
- 💬 加入 Discord 交流求助
- 🐞 在 GitHub 反馈问题与建议
- 🎁 支持项目开发 [前往支持](https://optimizerduck.vercel.app/docs/contribute/support-me)

### 相关链接

- 🌐 [官方网站](https://optimizerduck.vercel.app/)
- 📖 [使用文档](https://optimizerduck.vercel.app/docs/guides/getting-started)
- 💬 [Discord 社区](https://discord.gg/tDUBDCYw9Q)
- 🐞 [问题反馈](https://github.com/itsfatduck/optimizerDuck/issues)

每一份贡献都值得感谢：漏洞反馈、功能建议、翻译贡献或单纯分享使用体验，都能推动项目前进。

---

## 免责声明

optimizerDuck 按**现状原样**提供，不附带任何形式的保证。

使用本工具即表示你同意：项目作者不对系统不稳定、数据丢失、第三方软件冲突或用户自行修改操作引发的任何问题承担责任。

修改系统前请务必**创建系统还原点**并备份重要资料。

> [!NOTE]
> 本工具会修改系统设置与 Windows 注册表。使用风险自负。
> 建议提前备份数据并创建系统还原点，避免意外情况。
>
> 详细条款请查阅：[服务条款](./TERMS.md)、[隐私政策](./PRIVACY.md)、[免责声明](./DISCLAIMER.md)

---

## 开源协议

<div align="center">

<a href="./LICENSE">
<img src=".github/assets/gplv3.png" alt="GPL v3 开源协议" title="GPL v3 开源协议"/>
</a>

**[GPL v3 开源协议](https://www.gnu.org/licenses/gpl-3.0.en.html)**<br>详见 [LICENSE](./LICENSE) 文件。

</div>

<div align="center">

## 致敬所有项目贡献者

[![贡献者列表](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
