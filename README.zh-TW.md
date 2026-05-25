<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

# [optimizerDuck](https://optimizerduck.vercel.app/)

**optimizerDuck 是一款免費、開源的 Windows 優化工具，旨在提供性能、隱私和簡單性。**

[![Release](https://img.shields.io/github/release/itsfatduck/optimizerDuck?color=fed114&label=%E7%99%BC%E4%BD%88&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/itsfatduck/optimizerDuck/total?label=%E4%B8%8B%E8%BC%89%E9%87%8F&style=flat-square&color=lightgreen)](https://github.com/itsfatduck/optimizerDuck/releases)
[![Stars](https://img.shields.io/endpoint?url=https://api.pinstudios.net/api/badges/stars/itsfatduck/optimizerDuck&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/stargazers)
[![License](https://img.shields.io/badge/%E6%8E%88%E6%AC%8A-GPL_v3-red?style=flat-square)](./LICENSE)
[![Discord](https://img.shields.io/discord/1091675679994675240?color=5865f2&label=Discord&style=flat-square)](https://discord.gg/tDUBDCYw9Q)
<br>
[![CI](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml/badge.svg)](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml)
[![.NET Latest](https://img.shields.io/badge/.NET_Runtime-%E6%9C%80%E6%96%B0-ef99dd?style=flat-square)](https://dotnet.microsoft.com/en-us/download)
[![Supported OS](https://img.shields.io/badge/%E6%94%AF%E6%8F%B4%E7%B3%BB%E7%B5%B1-Windows_10%2B_x64-0078d4?style=flat-square)](https://www.microsoft.com/en-us/software-download/)

**[開始使用](https://optimizerduck.vercel.app/docs/guides/getting-started) | [運作原理](https://optimizerduck.vercel.app/docs/guides/how-it-works) | [常見問題](https://optimizerduck.vercel.app/docs/faq/general)**

[English](README.md) | [Tiếng Việt](README.vi.md) | **繁體中文** | [简体中文](README.zh-CN.md) | [Русский](README.ru-RU.md)

<details>
<summary>⭐ 星星歷史</summary>

如果 optimizerDuck 幫助改善了您的電腦，請考慮給這個專案一個 ⭐ 並分享給他人。
每一顆星都有助於激勵未來的持續改進。

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

## 快速開始

1. 從 **[GitHub Releases](https://github.com/itsfatduck/optimizerDuck/releases/latest)** 下載
2. 直接執行 `.exe`，無需安裝
3. 挑選您要套用的最佳化項目，套用後重新啟動電腦

> [!TIP]
> 進行系統更改前，請先建立**系統還原點**。

> [!NOTE]
> 目前支援英文、越南文、繁體中文（由 [@abc0922001](https://github.com/abc0922001) 貢獻）、簡體中文（由 [@wcxu21](https://github.com/wcxu21) 貢獻）、及俄文（由 [@Foodhead](https://github.com/Foodhead) 貢獻）。
> 想加入您的語言嗎？請查看[貢獻指南](./CONTRIBUTING.md)。

---

## optimizerDuck 做什麼

Windows 內建了不少您可能用不到的東西：背景服務、遙測資料、預裝應用程式、開機啟動程式，以及排程工作，這些都會消耗系統資源。optimizerDuck 提供一個統一的介面來清理這些內容。

它套用針對性的系統調整來減少資源消耗和阻擋不需要的行為，同時內建多款管理工具，讓您掌握執行中的程式、移除不需要的內容，並在出問題時還原任何變更。

> [!NOTE]
> 所有最佳化均可手動執行。optimizerDuck 只是讓您更輕鬆地套用這些最佳化。

### 系統最佳化

超過 30 項調整，分為 6 大類別。每項都有清楚說明與風險評級，讓您在套用前就了解這項變更會做什麼。

| 類別               | 涵蓋內容                                                                                                    |
| :----------------- | :---------------------------------------------------------------------------------------------------------- |
| **效能**           | 依據您的記憶體容量調整 Service Host、程序優先順序調整、降低鍵盤延遲、多媒體排程器優化以獲得更順暢的遊戲體驗 |
| **隱私**           | 停用 Windows 遙測、錯誤回報、廣告 ID、位置追蹤、Cortana、Copilot 及內容傳遞建議                             |
| **GPU**            | 針對 AMD、NVIDIA、Intel 顯示卡的專屬登錄檔調整，涵蓋電源狀態、時脈閘控和顯示延遲                            |
| **電源**           | 停用休眠與快速啟動、關閉 USB 選擇性暫停、安裝高效能自訂電源計畫、停用電源節流                               |
| **冗餘軟體與服務** | 阻止 OEM 應用程式重新安裝行為，並精細調整 200 多項 Windows 服務的啟動類型                                   |
| **使用體驗**       | 移除選單顯示延遲、停用工作列動畫和透明度等視覺效果，讓系統反應更迅速                                        |

> [!IMPORTANT]
> 如果您覺得最佳化項目不多，別急著認為它們沒效果或過時了。optimizerDuck 只專注於那些經過測試、基準驗證或社群廣泛信賴的最佳化。部分變更可能不會立即產生明顯差異，但長期下來仍能幫助您的系統更穩定、更順暢地運行。

### 功能開關

直接開啟或關閉 Windows 設定，不用翻註冊表或上網搜尋教學。分為四個區塊：

- **桌面**：顯示或隱藏圖示（本機、資源回收筒、網路、使用者資料夾、控制台），移除捷徑箭頭
- **工作列**：置中或靠左、切換小工具、工作檢視按鈕、結束工作按鈕、時鐘顯示秒數、停用開始選單中的 Bing 搜尋
- **檔案總管**：副檔名、隱藏檔案、剪貼簿歷史、緊湊模式、視窗貼齊、項目核取方塊、傳統右鍵選單等
- **遊戲**：遊戲模式、遊戲列、背景錄製、滑鼠加速度、全螢幕最佳化、硬體加速 GPU 排程
- **系統**：開機自動啟用 Num Lock

### 內建工具

| 工具             | 功能                                                                                                                 |
| :--------------- | :------------------------------------------------------------------------------------------------------------------- |
| **系統儀表板**   | 在同一個面板中查看 CPU、RAM、GPU、儲存裝置和作業系統詳細資訊                                                         |
| **啟動管理**     | 查看所有開機啟動的應用程式和工作，切換啟用或停用，開啟檔案位置                                                       |
| **排程工作**     | 瀏覽、執行、停止、啟用、停用或刪除 Windows 排程工作                                                                  |
| **磁碟清理**     | 掃描並清理暫存檔、系統快取、Windows Update 殘留檔案、Prefetch、縮圖快取、資源回收筒、當機傾印檔及舊版 Windows 安裝檔 |
| **冗餘軟體卸載** | 列出所有可移除的 AppX 套件並附上風險標籤（安全、謹慎、未知），讓您自行選擇要刪除的內容                               |

---

## 安全防護

我們了解修改系統設定存在風險，因此工具的設計核心圍繞在可還原性和使用者控制權。

有關資料處理方式的詳細資訊，請參閱[隱私權政策](./PRIVACY.md)。

- **自動備份**：每次變更都會將還原檔案寫入本機資料夾。您可以還原單一調整或全部復原
- **一鍵還原**：從介面直接點擊即可撤銷已套用的任何最佳化
- **風險評級**：每項調整都標示為安全、中等或高風險，依據其潛在影響程度分類
- **不預設套用**：在您手動選擇之前不會執行任何操作。工具不會自行啟用任何變更
- **還原點提醒**：在套用第一個最佳化之前，應用程式會建議您建立 Windows 還原點

---

## 技術細節

- **框架**：WPF 搭配 .NET 10，使用 WPF UI 程式庫實現 Fluent 設計
- **還原系統**：四種還原步驟類型（登錄檔、服務、排程工作、Shell），以 JSON 儲存狀態並採用執行緒安全的檔案 I/O
- **主題**：深色（預設）、淺色和高對比模式，支援 Mica 背景效果
- **免安裝**：以單一 .exe 執行，無需安裝
- **備份系統**：每次變更均以本機資料夾備份，支援一鍵還原
- **自動探索**：最佳化與功能類別透過 reflection + 自訂屬性自動探索，無需手動註冊
- **無遙測**：應用程式不收集任何使用者資料

---

## 文件

### [官方文件](https://optimizerduck.vercel.app/docs/guides/getting-started)

分步操作指南、每項最佳化的詳細說明，以及使用 optimizerDuck 的最佳實踐。

---

## 貢獻

我們歡迎來自社群的任何貢獻！無論您是修復錯誤、新增最佳化項目或功能、改善文件，還是協助將應用程式翻譯成其他語言，您的支持都讓我們非常感激。

如需更多資訊，請參閱 [CONTRIBUTING.md](./CONTRIBUTING.md)。

---

## 社群

> [!TIP]
> 加入我們的 Discord 伺服器，取得支援、分享使用技巧，並與其他使用者和開發者交流。
>
> <a href="https://discord.gg/tDUBDCYw9Q"><img src="https://discord.com/api/guilds/1091675679994675240/widget.png?style=banner2" alt="Discord Banner 2"/></a>

如果 optimizerDuck 對您的電腦有幫助：

- ⭐ 給專案點星
- 💬 加入 Discord 交流
- 🐞 在 GitHub 回報問題
- 🎁 支持專案開發 [前往支持](https://optimizerduck.vercel.app/docs/contribute/support-me)

### 連結

- 🌐 [官方網站](https://optimizerduck.vercel.app/)
- 📖 [使用文件](https://optimizerduck.vercel.app/docs/guides/getting-started)
- 💬 [Discord](https://discord.gg/tDUBDCYw9Q)
- 🐞 [問題回報](https://github.com/itsfatduck/optimizerDuck/issues)

每一份貢獻都有幫助。錯誤回報、功能建議、翻譯貢獻或單純分享使用心得，都能推動專案前進。

---

## 免責聲明

optimizerDuck 按**「現況」**提供，不附帶任何形式的保證。

使用本工具即表示您同意作者不對系統不穩定、資料遺失，或因第三方軟體或使用者修改所導致的問題負責。

套用變更前請務必建立**還原點**。

> [!NOTE]
> optimizerDuck 會修改系統設定與 Windows 登錄檔。使用風險自負。我們建議在進行變更前備份重要資料並建立還原點。
>
> 詳細資訊請參閱[服務條款](./TERMS.md)、[隱私權政策](./PRIVACY.md)和[免責聲明](./DISCLAIMER.md)。

---

## 授權

<div align="center">

<a href="./LICENSE">
<img src=".github/assets/gplv3.png" alt="GPL v3 License" title="GPL v3 License"/>
</a>

**[GPL v3 授權](https://www.gnu.org/licenses/gpl-3.0.en.html)**<br>參見 [LICENSE](./LICENSE)。

</div>

<div align="center">

## 感謝所有貢獻者

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
