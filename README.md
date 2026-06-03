<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

# [optimizerDuck](https://optimizerduck.vercel.app/)

**optimizerDuck is a free, open-source Windows optimization tool focused on performance, privacy, and simplicity.**

[![Release](https://img.shields.io/github/release/itsfatduck/optimizerDuck?color=fed114&label=Release&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/itsfatduck/optimizerDuck/total?label=Downloads&style=flat-square&color=lightgreen)](https://github.com/itsfatduck/optimizerDuck/releases)
[![Stars](https://img.shields.io/endpoint?url=https://api.pinstudios.net/api/badges/stars/itsfatduck/optimizerDuck&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/stargazers)
[![License](https://img.shields.io/badge/License-GPL_v3-red?style=flat-square)](./LICENSE)
[![Discord](https://img.shields.io/discord/1091675679994675240?color=5865f2&label=Discord&style=flat-square)](https://discord.gg/tDUBDCYw9Q)
<br>
[![CI](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml/badge.svg)](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml)
[![.NET Latest](https://img.shields.io/badge/.NET_Runtime-Latest-ef99dd?style=flat-square)](https://dotnet.microsoft.com/en-us/download)
[![Supported OS](https://img.shields.io/badge/Supported-Windows_10%2B_x64-0078d4?style=flat-square)](https://www.microsoft.com/en-us/software-download/)

**[Getting Started](https://optimizerduck.vercel.app/docs/guides/getting-started) | [How It Works](https://optimizerduck.vercel.app/docs/guides/how-it-works) | [FAQ](https://optimizerduck.vercel.app/docs/faq/general)**

**English** | [Tiếng Việt](README.vi.md) | [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md) | [Русский](README.ru-RU.md) | [Français](README.fr-FR.md) | [한국어](README.ko-KR.md)

<details>
<summary>⭐ Star History</summary>

If optimizerDuck helped improve your PC, consider giving the repo a ⭐ and sharing it with others.
Every star helps motivate future improvements.

<a href="https://www.star-history.com/#itsfatduck/optimizerDuck&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&legend=top-left" />
 </picture>
</a>

</details>

<img src="./.github/assets/app.png" alt="optimizerDuck Dark Mode" title="optimizerDuck Dark Mode" width="800"/>

</div>

---

## Quick Start

1. Download from **[GitHub Releases](https://github.com/itsfatduck/optimizerDuck/releases/latest)**
2. Run the `.exe` directly, no installation required
3. Choose the optimizations you want, apply them, and restart your PC when you're ready

> [!TIP]
> Always create a **system restore point** before making changes.

> [!NOTE]
> | | Language | Native Name | Translator |
> |------|----------|-------------|------------|
> | 🇺🇸 | English (United States) | English | Primary & recommended |
> | 🇻🇳 | Vietnamese | Tiếng Việt | [itsfatduck](https://github.com/itsfatduck) |
> | 🇹🇼 | Traditional Chinese | 正體中文 | [abc0922001](https://github.com/abc0922001) |
> | 🇨🇳 | Simplified Chinese | 简体中文 | [wcxu21](https://github.com/wcxu21) |
> | 🇷🇺 | Russian | Русский | [Foodhead](https://github.com/Foodhead) |
> | 🇫🇷 | French | Français | [Robocnop](https://github.com/Robocnop) |
> | 🇰🇷 | Korean | 한국어 | [klfnn](https://github.com/klfnn) |
> | 🇪🇸 | Spanish | Español | [thexxtt](https://github.com/thexxtt) |

> Want to add your language? See [CONTRIBUTING.md](./CONTRIBUTING.md).

---

## What optimizerDuck Does

Windows itself is fine. But a clean install also comes with services, telemetry, pre-installed apps, and scheduled tasks you've probably never heard of, all quietly running in the background, consuming your CPU, RAM, and disk. At the same time, some features that could actually help you get the most out of your hardware aren't enabled by default.

optimizerDuck gives you a single interface to clean up the bloat and unlock the good stuff.

It applies targeted system tweaks to reduce overhead and block unwanted behavior, and bundles several management tools so you can see what is running, remove what you do not want, and revert any change if something goes wrong.

> [!NOTE]
> Every optimization can be applied manually. optimizerDuck just makes it easier for you to apply these optimizations.

### System Optimizations

Over 30 tweaks across 6 categories, each with a clear description and risk rating so you know exactly what each change does before applying it.

| Category                 | What it covers                                                                                                                                       |
| :----------------------- | :--------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Performance**          | Service host tuning based on your RAM, process priority adjustments, keyboard latency reduction, and multimedia scheduler tweaks for smoother gaming |
| **Privacy**              | Disable Windows telemetry, error reporting, advertising ID, location tracking, Cortana, Copilot, and content delivery suggestions                    |
| **GPU**                  | Vendor-specific registry tweaks for AMD, NVIDIA, and Intel GPUs, covering power states, clock gating, and display latency                            |
| **Power**                | Disable hibernation and fast startup, turn off USB selective suspend, install a custom high-performance power plan, and disable power throttling     |
| **Bloatware & Services** | Block OEM app reinstall behavior and fine-tune startup types for 200+ Windows services                                                               |
| **User Experience**      | Remove menu show delays, disable visual effects like taskbar animations and transparency for a snappier feel                                         |

> [!NOTE]
> The optimizations here are researched from well-known tools with large user bases, nothing is AI-generated or blindly added. Every tweak is chosen for real-world impact.

### Customize

No need to dig through the registry, just toggles, dropdowns, and number inputs presented in one place. Organized into four categories:

- **Desktop**: Show or hide icons (This PC, Recycle Bin, Network, User Files, Control Panel), remove shortcut arrow overlays
- **Preferences**: Taskbar alignment, widgets, Task View and End Task buttons, clock seconds, dark mode, file extensions, hidden files, clipboard history, compact view, snap assist, item checkboxes, classic context menu, and Bing search
- **Gaming**: Game Mode, Game Bar, background recording, mouse acceleration, fullscreen optimizations, hardware-accelerated GPU scheduling
- **System**: Enable Num Lock on boot

### Built-in Tools

| Tool                  | What it does                                                                                                                                     |
| :-------------------- | :----------------------------------------------------------------------------------------------------------------------------------------------- |
| **System Dashboard**  | View your CPU, RAM, GPU, storage drives, and OS details in one panel                                                                             |
| **Startup Manager**   | See every app and task that launches at boot, toggle them on or off, and open their file location                                                |
| **Scheduled Tasks**   | Browse, run, stop, enable, disable, or delete Windows scheduled tasks                                                                            |
| **Disk Cleanup**      | Scan and clear temp files, system cache, Windows Update leftovers, prefetch, thumbnails, recycle bin, crash dumps, and old Windows installations |
| **Bloatware Remover** | Lists all removable AppX packages with risk badges (Safe, Caution, Unknown), so you can pick what to remove                                      |

---

## Safety

Changing system settings carries risk. optimizerDuck is built around reversibility and user control.

See the [Privacy Policy](./PRIVACY.md) for details on our data practices.

- **Automatic backups**: Every change writes a revert file to a local folder. You can restore individual tweaks or roll back everything
- **One-click revert**: Undo any applied optimization from the UI with a single click
- **Risk ratings**: Each tweak is labeled Safe, Moderate, or Risky based on its potential impact
- **No defaults applied**: Nothing runs until you select it. The tool does not enable anything on its own
- **Restore point prompt**: Before your first optimization, the app suggests creating a Windows restore point

---

## Technical Details

- **Framework**: WPF on .NET 10, using the WPF UI library for Fluent design
- **Revert system**: Four revert step types (Registry, Service, Scheduled Task, Shell) with JSON-persisted state and thread-safe file I/O
- **Theming**: Dark (default), Light, and High Contrast modes with Mica backdrop support
- **No installer**: Runs as a single .exe, no installation required
- **Backup system**: Local folder-based backup for every change, with one-click restore
- **Discovery**: Optimization and Feature categories are discovered automatically via reflection + custom attributes, no manual registration needed
- **No telemetry**: The app does not collect any user data

---

## Documentation

### [Official Documentation](https://optimizerduck.vercel.app/docs/guides/getting-started)

Guides, optimization details, and usage tips.

---

## Contribute

Bug reports, new optimizations, docs improvements, and translations are all welcome. See [CONTRIBUTING.md](./CONTRIBUTING.md).

---

## Community

> [!TIP]
> Join our Discord server for support, tips, and discussions with other users and contributors.
>
> <a href="https://discord.gg/tDUBDCYw9Q"><img src="https://discord.com/api/guilds/1091675679994675240/widget.png?style=banner2" alt="Discord Banner 2"/></a>

If optimizerDuck helped your PC:

- ⭐ Star the repo
- 💬 Join Discord for support
- 🐞 Report bugs on GitHub
- 🎁 Support the project [here](https://optimizerduck.vercel.app/docs/contribute/support-me)

### Links

- 🌐 [Website](https://optimizerduck.vercel.app/)
- 📖 [Documentation](https://optimizerduck.vercel.app/docs/guides/getting-started)
- 💬 [Discord](https://discord.gg/tDUBDCYw9Q)
- 🐞 [Issues](https://github.com/itsfatduck/optimizerDuck/issues)

Bug reports, feature suggestions, translations, and sharing your experience all help the project.

---

## Disclaimer

optimizerDuck is provided **"as is"**, without warranty of any kind.

By using this tool, you agree that the authors are not liable for system instability, data loss, or issues caused by third-party software or user modifications.

Always create a **restore point** before applying changes.

> [!NOTE]
> optimizerDuck modifies system settings and the Windows registry. Use at your own risk. We recommend backing up important data and creating a restore point before making changes.
>
> See [Terms of Service](./TERMS.md), [Privacy Policy](./PRIVACY.md), and [Disclaimer](./DISCLAIMER.md) for more information.

---

## License

<div align="center">

<a href="./LICENSE">
<img src=".github/assets/gplv3.png" alt="GPL v3 License" title="GPL v3 License"/>
</a>

**[GPL v3 License](https://www.gnu.org/licenses/gpl-3.0.en.html)**<br>See [LICENSE](./LICENSE).

</div>

<div align="center">

## Thanks to all Contributors

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
