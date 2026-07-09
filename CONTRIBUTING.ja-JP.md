<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

[English](CONTRIBUTING.md) | **日本語** | [Türkçe](CONTRIBUTING.tr-TR.md)

[はじめに](#introduction) • [セットアップ](#getting-started) • [アーキテクチャ概要](#architecture-overview) • [貢献の方法](#ways-to-contribute) • [最適化の作成](#creating-an-optimization) • [カスタマイズ設定の作成](#creating-a-customize-setting) • [リフレッシュスコープシステム](#the-refresh-scope-system) • [新機能の構築](#building-new-features) • [リバートシステム](#revert-system) • [テスト](#testing) • [コーディング規約](#coding-standards) • [ローカライズ](#localization) • [プルリクエストの手順](#pull-request-process) • [Issue ガイドライン](#issue-guidelines) • [FAQ とトラブルシューティング](#faq--troubleshooting) • [ライセンス](#license)

</div>

---

<h1 id="introduction">はじめに</h1>

**optimizerDuck** への貢献ありがとうございます。本プロジェクトは、.NET 10 上の WPF で構築された、無料のオープンソース Windows 最適化ツールです。

以下のような形でお手伝いいただけます：
- 再現手順を明確にしたバグ報告
- 新しい最適化や機能の提案（まず Issue を作成してください）
- ドキュメントやガイドの改善
- 翻訳の追加や修正
- コードの貢献：最適化、カスタマイズ設定、サービス、UI の改善
- テストの追加や既存テストのレビュー

---

<h1 id="getting-started">セットアップ</h1>

<h3 id="environment-setup">1. 環境のセットアップ</h3>

| 要件 | 備考 |
|---|---|
| **Windows 10/11 x64** | アプリは管理者として実行し、システムを変更します — Windows 専用 |
| **.NET 10 SDK** | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) からダウンロード |
| **IDE** | [Visual Studio 2026](https://visualstudio.microsoft.com/)（`.NET desktop development` ワークロード）、[JetBrains Rider](https://www.jetbrains.com/rider/)、または VS Code + C# Dev Kit |
| **Git** | バージョン管理 |

セットアップを確認します：

```bash
dotnet --version
# Should output 10.x
```

<h3 id="fork-and-clone">2. フォークとクローン</h3>

```bash
# Fork on GitHub first, then clone your fork
git clone https://github.com/<your-username>/optimizerDuck.git
cd optimizerDuck

# Add upstream remote to sync with the main repo
git remote add upstream https://github.com/itsfatduck/optimizerDuck.git

# Create a branch for your work (never work on master)
git checkout -b feature/your-feature-name
```

<h3 id="restore-build-test">3. 復元、ビルド、テスト</h3>

ソリューションは `.slnx` 形式（XML ベースのソリューションファイル、`.sln` ではありません）を使用します。

```bash
# Restore dependencies
dotnet restore optimizerDuck.slnx

# Build (CI uses Release, Debug works too)
dotnet build optimizerDuck.slnx --configuration Release --no-restore

# Run tests
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build

# Run the app
dotnet run --project optimizerDuck/optimizerDuck.csproj

# Format code with CSharpier
dotnet csharpier .
```

> 新しい NuGet 依存関係を追加した場合は、再度 `dotnet restore` を実行してください（以降のビルドでは `--no-restore` を使用します）。

<h3 id="publishing">4. 公開（Publishing）</h3>

```bash
publish.bat portable              # Portable folder (recommended for testing)
publish.bat single                # Single-file executable
publish.bat single --skip-tests   # Skip tests for quick iteration
publish.bat portable --no-pause   # Don't pause at the end (CI-friendly)
```

<h3 id="quick-start-checklist">5. クイックスタートチェックリスト</h3>

初めて貢献する前に：

- [ ] リポジトリをフォークしてクローンする
- [ ] `dotnet build` が成功する（エラー 0 件）
- [ ] `dotnet test` が通る（すべてのテストが成功）
- [ ] `dotnet csharpier .` がエラーなくフォーマットできる
- [ ] 下記の [アーキテクチャ概要](#architecture-overview) を読む

---

<h1 id="architecture-overview">アーキテクチャ概要</h1>

<h3 id="solution-structure">ソリューション構成</h3>

```
optimizerDuck.slnx                          # Solution file (.slnx format)
├── optimizerDuck/                          # Main WPF app (net10.0-windows)
│   ├── App.xaml.cs                         # DI registration, startup, theme, logging
│   ├── optimizerDuck.csproj                # TFM: net10.0-windows10.0.17763.0, UseWPF=true
│   ├── app.manifest                        # requireAdministrator UAC level
│   │
│   ├── Domain/                             # Pure models, interfaces, attributes (no WPF deps)
│   │   ├── Abstractions/                   # IOptimization, ICustomizeSetting, IRevertStep, IWindow, ICustomizeCategory, IOptimizationCategory
│   │   ├── Attributes/                     # [Optimization], [CustomizeSetting], [OptimizationCategory], [CustomizeCategory]
│   │   ├── Configuration/                  # AppSettings model
│   │   ├── Execution/                      # ExecutionScope — ambient step tracking via AsyncLocal
│   │   ├── Customize/                      # Customize settings (Desktop, Gaming, Preferences, System)
│   │   │   ├── Categories/                 # Category classes with nested setting classes
│   │   │   └── Models/                     # BaseCustomizeSetting, RegistryToggle, RefreshScope, SettingOption, CustomizeControlType, RecommendationState
│   │   ├── Optimizations/                  # Optimizations (Performance, Privacy, GPU, etc.)
│   │   │   ├── Categories/                 # Category classes with nested optimization classes
│   │   │   └── Models/                     # BaseOptimization, ApplyResult, OptimizationContext, ServiceItem, RegistryItem
│   │   │       ├── Bloatware/              # AppXPackage model
│   │   │       ├── Cleanup/                # CleanupItem
│   │   │       ├── ScheduledTask/          # ScheduledTaskModel
│   │   │       ├── Services/               # RegistryItem, ServiceItem, ShellResult, ServiceStartupType
│   │   │       └── StartupManager/         # StartupApp, StartupTask
│   │   ├── Revert/                         # RevertData, RevertResult, revert step types
│   │   │   └── Steps/                      # RegistryRevertStep, ServiceRevertStep, ScheduledTaskRevertStep, ShellRevertStep, UsbPowerRevertStep
│   │   └── UI/                             # Enums: OptimizationRisk, OptimizationTags, OptimizationCategoryOrder, CustomizeOrder, LanguageOption, OptimizationState, RiskVisual, ProcessingProgress
│   │
│   ├── Common/                             # Shared helpers, extensions, converters
│   │   ├── Converters/                     # 20+ WPF value converters
│   │   ├── Extensions/                     # StringExtensions, CustomizePageRegistryExtensions, OptimizationPageRegistryExtensions, LanguageExtensions
│   │   ├── Helpers/                        # Shared.cs, ReflectionHelper.cs, SystemRefreshService.cs, EmbeddedResourceHelper.cs, GitHubSourceHelper.cs, WmiHelper.cs, ThemeResource.cs
│   │   └── Native/                         # Native interop helpers
│   │
│   ├── Services/                           # Business logic layer
│   │   ├── Configuration/                  # ConfigManager, LanguageManager
│   │   ├── Customize/                      # CustomizeRegistry (reflection-based discovery)
│   │   ├── Optimization/                   # OptimizationRegistry, OptimizationService
│   │   │   └── Providers/                  # Static: RegistryService, ShellService, ScheduledTaskService, ServiceProcessService
│   │   ├── Revert/                         # RevertManager (atomic write/read of revert JSON files)
│   │   ├── System/                         # RegistryWatcher, StreamService, SystemInfoService, UpdaterService
│   │   └── UI/                             # BloatwareService, DiskCleanupService, StartupManagerService
│   │
│   ├── UI/                                 # WPF pages, ViewModels, controls, styles
│   │   ├── Behaviors/                      # SmoothScrollBehavior
│   │   ├── Controls/                       # FilledNavigationViewItem
│   │   ├── Dialogs/                        # ProcessingDialog, OptimizationDetailsDialog, OptimizationResultDialog, RestorePointDialog, LegalDialog, BloatwareConfirmationDialog, ScheduledTaskCreateDialog, ScheduledTaskDetailsDialog, StartupTaskDetailsPanel
│   │   ├── Pages/                          # Dashboard, Optimize, Customize, Settings, Bloatware, DiskCleanup, StartupManager, ScheduledTasks + sub-folders
│   │   │   ├── Customize/                  # CustomizePage + Categories/
│   │   │   └── Optimize/                   # OptimizePage + Categories/
│   │   ├── Styles/                         # FluentDesign.xaml, NavigationViewOverride.xaml, ToolTipOverride.xaml
│   │   ├── ViewModels/                     # Page and dialog ViewModels
│   │   │   ├── Customize/                  # CustomizeItemViewModel, CustomizeCategoryViewModel
│   │   │   ├── Dialogs/                    # ProcessingViewModel, OptimizationDetailsViewModel, etc.
│   │   │   ├── Optimizer/                  # OptimizationCategoryViewModel
│   │   │   ├── Pages/                      # Dashboard, Optimize, Customize, Settings, etc.
│   │   │   └── Windows/                    # MainWindowViewModel
│   │   └── Windows/                        # MainWindow
│   │
│   └── Resources/                          # Images, embedded assets, localization
│       ├── Embedded/                       # Power plans, icons
│       ├── Images/                         # Duck.png, logos
│       └── Languages/                      # Translations.resx + 11 locale variants
│
└── optimizerDuck.Test/                     # xUnit v3 test project
```

<h3 id="key-design-decisions">主要な設計判断</h3>

| 判断 | 理由 |
|---|---|
| **リフレクションによる自動検出** | DI 登録配列を更新する必要がありません。`ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` が起動時に `optimizerDuck.*` アセンブリをスキャンします。新しい最適化や設定は自動的に検出されます。 |
| **静的プロバイダーサービス** | `RegistryService`、`ShellService`、`ScheduledTaskService`、`ServiceProcessService` は静的クラスです。アンビエントな `ExecutionScope` にリバートステップを記録するため、コンテキストの注入や受け渡しは不要です。 |
| **ファイルベースのリバート追跡** | 適用状態 = ディスク上にファイルが存在する（`%localappdata%\optimizerDuck\Revert\{id}.json`）。データベースは使用しません。`File.Replace()` によるアトミック書き込み。 |
| **統合スタイルのテスト** | 実際のファイルシステム、実際のレジストリ（`HKCU\Software\TestOptimizerDuck*` 配下）、実際のプロセス実行。モックライブラリは使用せず、手書きのテストダブルのみ。 |
| **非同期サービスメソッド** | 外部プロセスを実行するプロバイダーメソッドは非同期（`*Async` サフィックス）です。最適化の `ApplyAsync` メソッドでは `async`/`await` を使用して UI の応答性を保ってください。 |
| **静的な WMI ヘルパー** | `WmiHelper.Initialize()` が起動時に実行され、異常終了時の WMI イベントクリーンアップハンドラを登録します。 |
| **保留中の変更トラッキング** | `App.HasPendingChanges` プロパティが未リバートの最適化を追跡します。アプリ終了時に PC/Explorer 再起動または終了のオプションを表示します。 |

---

<h1 id="ways-to-contribute">貢献の方法</h1>

| 貢献の種類 | 説明 | 着手場所 |
|---|---|---|
| **新しい最適化** | レジストリの調整、サービスの変更、システムの調整 | `Domain/Optimizations/Categories/*.cs` |
| **新しいカスタマイズ設定** | Windows 設定の UI トグル（ゲームモード、マウス加速度など） | `Domain/Customize/Categories/*.cs` |
| **新しいアプリ機能** | 新しいページ、ツール、機能 | まず Issue を作成 |
| **バグ修正** | クラッシュ修正、ロジックエラー、UI の問題 | 任意の場所 |
| **翻訳** | 新しい言語の追加や既存翻訳の修正 | `Resources/Languages/Translations.*.resx` |
| **ドキュメント** | README、CONTRIBUTING など | `*.md` ファイル |
| **テスト** | 新規・既存のテストの追加・レビュー | `optimizerDuck.Test/` |

---

<h1 id="creating-an-optimization">最適化の作成</h1>

<h3 id="how-discovery-works">検出の仕組み</h3>

起動時：

1. `ReflectionHelper.FindImplementationsInLoadedAssemblies<IOptimizationCategory>()` がすべての `optimizerDuck.*` アセンブリをスキャンする
2. `IOptimizationCategory` を実装するすべてのクラスを見つける
3. 各カテゴリについて、`IOptimization` を実装する**ネストされた public クラス**をスキャンする
4. 検出されたすべての最適化がインスタンス化され、`OwnerType` が自動的に割り当てられる
5. `OptimizationService.UpdateOptimizationStateAsync` がディスク上のリバートファイルをスキャンし、各最適化を適用済み/未適用としてマークする
6. `OptimizationRegistry.StartPreload()` が起動時にバックグラウンドスレッドでこれを実行する

**あなたの作業**：カテゴリ内にネストされたクラスを作成し、`BaseOptimization` を継承し、`[Optimization]` を付与する。以上です。

<h3 id="optimization-categories">最適化カテゴリ</h3>

現在のカテゴリ（`Domain/Optimizations/Categories/` 内）：

| ファイル | 属性 | 対象 |
|---|---|---|
| `Performance.cs` | `[OptimizationCategory(typeof(PerformanceOptimizerPage))]` | RAM 調整、プロセス優先度、キーボードレイテンシ、マルチメディアスケジューラ、アクセシビリティホットキー |
| `SecurityAndPrivacy.cs` | `[OptimizationCategory(typeof(SecurityAndPrivacyOptimizerPage))]` | テレメトリ、エラー報告、広告 ID、位置情報、Cortana、Copilot、コンテンツ配信マネージャー、アクティビティ履歴、AutoLogger |
| `Gpu.cs` | `[OptimizationCategory(typeof(GpuOptimizerPage))]` | AMD/NVIDIA/Intel レジストリ調整、電源状態、クロックゲーティング、ASPM、非同期フリップ |
| `PowerManagement.cs` | `[OptimizationCategory(typeof(PowerManagementOptimizerPage))]` | 休止状態、高速スタートアップ、USB 選択的サスペンド、カスタム電源プランのインストール、省電力設定の無効化 |
| `BloatwareAndServices.cs` | `[OptimizationCategory(typeof(BloatwareAndServicesOptimizerPage))]` | OEM プリインストールアプリのブロック、170 以上の Windows サービス起動タイプ最適化 |
| `UserExperience.cs` | `[OptimizationCategory(typeof(UserExperienceOptimizerPage))]` | メニュー遅延、視覚効果、タスクバーアニメーション、透明度、スタートメニューの Web 検索 |

<h3 id="step-by-step-add-to-existing-category">ステップバイステップ：既存カテゴリへの追加</h3>

最も適したカテゴリファイルを選び、ネストされたクラスを追加します：

```csharp
[OptimizationCategory(typeof(PerformanceOptimizerPage))]
public class Performance : IOptimizationCategory
{
    public string Name => Loc.Instance[$"Optimizer.{nameof(Performance)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.Performance;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(
        Id = "a1b2c3d4-...",                          // Generate a NEW GUID
        Risk = OptimizationRisk.Safe,                   // Safe / Moderate / Risky
        Tags = OptimizationTags.Performance             // Flags — combine with |
    )]
    public class MyNewTweak : BaseOptimization
    {
        public override async Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context)
        {
            // 1. Use static providers to make system changes
            RegistryService.Write(new RegistryItem(
                @"HKLM\SOFTWARE\Something", "ValueName", 1));

            // 2. Await async operations — this yields the UI thread
            await ServiceProcessService.ChangeServiceStartupTypeAsync(
                new ServiceItem("SomeService", ServiceStartupType.Disabled));

            // 3. Return result from the ambient ExecutionScope
            return CompleteFromScope();
        }
    }
}
```

<h3 id="key-rules">重要なルール</h3>

| ルール | 詳細 |
|---|---|
| **`Id` は新しい GUID であること** | リバートファイルの命名と適用状態の追跡に使用されます。PowerShell で `[guid]::NewGuid()` を使用して生成します。 |
| **`BaseOptimization` を継承する** | 属性とローカライズキーから `Name`、`ShortDescription`、`Prefix`、`RiskVisual`、`TagDisplays` を提供します |
| **`async Task<ApplyResult>` を使用する** | `Task.FromResult()` は使用しない。サービスプロバイダーは非同期 — await して UI の応答性を保つ |
| **`CompleteFromScope()` を返す** | アンビエントな `ExecutionScope` に記録されたステップから `ApplyResult` を導出する |
| **進捗を報告する** | `progress.Report(new ProcessingProgress { ... })` を使用して UI ダイアログを更新する |
| **すべての例外をキャッチしない** | 例外は上位に伝播させる。`ExecutionScope` が成功/失敗を追跡する。`OptimizationService` レイヤーが例外を処理する |
| **リバートステップを手動で作成しない** | 静的プロバイダーサービスが `ExecutionScope.RecordStep()` 経由で自動的に行う |
| **`context.Logger` を使用する** | 最適化コンテキストはロガーを提供します。重要な診断情報の記録に使用します |
| **`context.Snapshot` をチェックする** | `OptimizationContext.Snapshot` がシステム情報（RAM、GPU、CPU）を提供します。条件付きロジックに使用できます |

<h3 id="available-service-providers">利用可能なサービスプロバイダー</h3>

これらの**静的**クラスは、ログ記録、エラー処理、リバートステップの自動記録を担当します。

| サービス | 主要メソッド | 使用理由 |
|---|---|---|
| **`RegistryService`** | `Write()`、`Read<T>()`、`DeleteValue()`、`CreateSubKey()`、`DeleteSubKeyTree()`、`KeyExists()` | レジストリキーの読み書き/削除。リバート用に元の値をバックアップする。複数の RegistryItem を一度に書き込み可能。 |
| **`ShellService`** | `CMDAsync()`、`PowerShellAsync()`、`CMD()`（同期）、`PowerShell()`（同期） | CMD または PowerShell コマンドの実行。常に非同期バリアントを使用する。`revertCommand` パラメータで元に戻すコマンドを指定可能。 |
| **`ScheduledTaskService`** | `DisableTask()`、`EnableTask()`、`IsTaskEnabled()`、`DeleteTask()` | Windows スケジュールタスクの管理。 |
| **`ServiceProcessService`** | `ChangeServiceStartupTypeAsync()`、`GetStartupTypeAsync()` | Windows サービスの管理。常に非同期バリアントを使用する。複数のサービスを一度に変更可能。 |

使用例：

```csharp
// Sync registry writes — multiple items at once
RegistryService.Write(
    new RegistryItem(@"HKLM\...", "Value1", 1),
    new RegistryItem(@"HKLM\...", "Value2", 0)
);
RegistryService.DeleteValue(new RegistryItem(@"HKCU\...", "OldValue"));

// Async service changes — multiple services at once
await ServiceProcessService.ChangeServiceStartupTypeAsync(
    new ServiceItem("DiagTrack", ServiceStartupType.Disabled),
    new ServiceItem("dmwappushservice", ServiceStartupType.Disabled)
);

// Async shell commands with revert command
var result = await ShellService.CMDAsync(
    "powercfg /h off",
    "powercfg /h on"     // revert command stored for undo
);

// Async PowerShell
var usbStates = await ShellService.PowerShellAsync(
    "Get-CimInstance -Namespace root\\wmi -ClassName MSPower_DeviceEnable"
);
```

<h3 id="new-category-and-helper-class">新しいカテゴリとヘルパーベースクラスの作成</h3>

最適化が既存のカテゴリに当てはまらない場合のみ。過度に細かいカテゴリは避けてください。

1. `Domain/Optimizations/Categories/YourCategory.cs` を作成する
2. `IOptimizationCategory` を実装する
3. `[OptimizationCategory(PageType = typeof(YourPage))]` を適用する — XAML ページも必要です
4. `Domain/UI/OptimizationCategoryOrder.cs` の `OptimizationCategoryOrder` 列挙型にメンバーを追加する
5. XAML ページは `App.xaml.cs` の `services.AddAllOptimizationPages()` 経由で自動登録される

複数の最適化が同じ構造を共有する場合（例：GPU ツイーク）、抽象中間クラスを作成します：

```csharp
public abstract class GpuRegistryOptimization : BaseOptimization
{
    protected abstract GpuVendor Vendor { get; }
    protected abstract IReadOnlyList<RegistryItem> CreateItems(string registryPath);

    public override Task<ApplyResult> ApplyAsync(...)
    {
        foreach (var gpu in context.Snapshot.Gpus.Where(g => g.Vendor == Vendor))
        {
            var path = $@"HKLM\...\{index:D4}";
            RegistryService.Write(CreateItems(path).ToArray());
        }
        return Task.FromResult(CompleteFromScope());
    }
}
```

<h3 id="localization-keys-optimization">ローカライズキー</h3>

すべての最適化には `Translations.resx` へのエントリが必要です。キーは厳格な規則に従います：

```
Optimizer.{CategoryName}.{OptimizationKey}.Name
Optimizer.{CategoryName}.{OptimizationKey}.ShortDescription
Optimizer.{CategoryName}.{OptimizationKey}.Progress.{CustomKey}
Optimizer.{CategoryName}.{OptimizationKey}.Error.{CustomKey}
```

`CategoryName` = カテゴリクラス名（例：`Performance`）、`OptimizationKey` = ネストされたクラス名。

> [!IMPORTANT]
> **翻訳は必須です**。これらのキーを追加し忘れると、アプリは `"Optimizer.Performance.MyNewTweak.Name"` のような生のキー文字列を表示します。最低限 `Translations.resx`（英語）にエントリを追加してください。

---

<h1 id="creating-a-customize-setting">カスタマイズ設定の作成</h1>

カスタマイズ設定は、Windows 設定を ON/OFF に切り替える UI コントロール（トグルスイッチ、ドロップダウン、数値入力）です。`Domain/Customize/Categories/` に配置されます。

<h3 id="customize-categories">カスタマイズカテゴリ</h3>

| ファイル | 属性 | 対象 |
|---|---|---|
| `Desktop.cs` | `[CustomizeCategory(PageType = typeof(DesktopFeatureCategory))]` | デスクトップアイコン（PC、ごみ箱、ネットワーク、ユーザーファイル）、ショートカットオーバーレイ |
| `Preferences.cs` | `[CustomizeCategory(PageType = typeof(PreferencesFeatureCategory))]` | タスクバーの配置、ウィジェット、ダークモード、ファイル拡張子、隠しファイル、クリップボード履歴、検索モード、秒表示、Bing 検索、クラシックコンテキストメニュー |
| `Gaming.cs` | `[CustomizeCategory(PageType = typeof(GamingFeatureCategory))]` | ゲームモード、ゲームバー、バックグラウンド録画、マウス加速度、フルスクリーン最適化、GPU スケジューリング |
| `SystemFeatures.cs` | `[CustomizeCategory(PageType = typeof(SystemFeatureCategory))]` | 起動時の Num Lock、開発者モード、LongPaths、バッテリー残量表示 |

<h3 id="step-by-step-simple-registry-toggle">ステップバイステップ：シンプルなレジストリトグル</h3>

シンプルな ON/OFF レジストリトグルの場合、基底クラスがすべての処理を行います：

```csharp
private enum Sections { Taskbar, Widgets, Advanced }

[CustomizeSetting(
    Section = nameof(Sections.Taskbar),        // Groups settings in the UI
    Icon = SymbolRegular.AlignCenter24,         // From Wpf.Ui.Controls.SymbolRegular
    Recommendation = RecommendationState.On     // On / Off / Depends / Experimental / None
)]
public class TaskbarAlignment : BaseCustomizeSetting
{
    protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "TaskbarAl",
                OnValue = 0,            // value when toggle is ON
                OffValue = 1,           // value when toggle is OFF
                DefaultValue = 1,       // value = default state (used when key missing)
            },
        ];

    // Declare what needs refreshing after this setting changes
    protected override CustomizeRefreshScope RefreshScope =>
        CustomizeRefreshScope.TaskbarSettings;
}
```

<h3 id="registrytoggle-properties">RegistryToggle プロパティ</h3>

| プロパティ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `Path` | `string` | 必須 | レジストリキーの完全パス（例：`@"HKCU\Software\..."`） |
| `Name` | `string` | 必須 | レジストリ値の名前 |
| `OnValue` | `object?` | `1` | 「オン」状態を表す値 |
| `OffValue` | `object?` | `0` | 「オフ」状態を表す値 |
| `DefaultValue` | `object?` | `0` | レジストリ値が存在しない場合のフォールバック |
| `IsOptional` | `bool` | `false` | `true` の場合、状態検出に不要 |
| `TreatMissingAsDefault` | `bool` | `false` | `true` の場合、キーが存在しないときは「オフ」ではなく `DefaultValue` を使用 |
| `ValueKind` | `RegistryValueKind` | `DWord` | レジストリ値の型（DWord、String など） |

<h3 id="control-types">コントロールタイプ</h3>

| 型 | 表示形式 | 用途 |
|---|---|---|
| `Toggle` | ON/OFF スイッチ | ほとんどの設定（デフォルト） |
| `Dropdown` | コンボボックス | 複数選択（例：電源プラン、検索ボックスモード） |
| `Option` | ラジオボタングループ | 排他的な視覚オプション（例：左/中央揃え） |
| `NumberInt` | 整数テキスト入力 | 数値（例：秒数） |
| `NumberFloat` | 小数テキスト入力 | 精度の高い値 |
| `String` | テキスト入力 | 自由形式のテキスト |

<h3 id="dropdown-with-options">オプション付きドロップダウン</h3>

```csharp
public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;

public override IReadOnlyList<SettingOption>? Options =>
    [
        Option("Never", 0),      // Option() helper reads from Translations.resx
        Option("Battery", 1),
        Option("Always", 2),
    ];

public override async Task ApplyAsync(object? value)
{
    var intValue = value is int i ? i : 0;
    RegistryService.Write(new RegistryItem(Path, "ValueName", intValue));
    await ExecutePostActionAsync();  // MUST call when overriding ApplyAsync
}
```

<h3 id="dynamic-options">動的オプション（プラットフォーム対応）</h3>

Windows バージョンに応じてオプションを条件付きで表示できます：

```csharp
public override IReadOnlyList<SettingOption>? Options
{
    get
    {
        if (Shared.IsWindows11OrGreater)
            return [Option("Hidden", 0), Option("Icon", 1), Option("IconAndLabel", 2), Option("SearchBox", 3)];
        return [Option("Hidden", 0), Option("Icon", 1), Option("SearchBox", 2)];
    }
}
```

<h3 id="custom-logic-override">カスタムロジック（GetStateAsync / ApplyAsync のオーバーライド）</h3>

```csharp
[CustomizeSetting(
    Section = nameof(Sections.Input),
    Icon = SymbolRegular.Cursor24,
    Recommendation = RecommendationState.Off
)]
public class MouseAcceleration : BaseCustomizeSetting
{
    private const string Path = @"HKCU\Control Panel\Mouse";

    protected override IReadOnlyList<string> GetWatchedRegistryPaths() => [Path];

    public override Task<bool> GetStateAsync()
    {
        return Task.Run(() =>
        {
            var speed = RegistryService.Read<string>(new RegistryItem(Path, "MouseSpeed"));
            var t1 = RegistryService.Read<string>(new RegistryItem(Path, "MouseThreshold1"));
            var t2 = RegistryService.Read<string>(new RegistryItem(Path, "MouseThreshold2"));
            return (int.TryParse(speed, out var s) && s != 0)
                || (int.TryParse(t1, out var a) && a != 0)
                || (int.TryParse(t2, out var b) && b != 0);
        });
    }

    public override async Task ApplyAsync(object? value)
    {
        var isOn = value is bool b && b;
        RegistryService.Write(new RegistryItem(Path, "MouseSpeed", isOn ? "1" : "0"));
        RegistryService.Write(new RegistryItem(Path, "MouseThreshold1", isOn ? "6" : "0"));
        RegistryService.Write(new RegistryItem(Path, "MouseThreshold2", isOn ? "10" : "0"));
        await ExecutePostActionAsync();  // MUST call when overriding ApplyAsync
    }

    protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Default;
}
```

<h3 id="recommendation-system">レコメンデーションシステム</h3>

各カスタマイズ設定はレコメンデーションを宣言できます：

```csharp
[CustomizeSetting(..., Recommendation = RecommendationState.On)]
// Available: On, Off, Depends, Experimental, None
```

レコメンデーションの理由はローカライズキーで追加：`Customize.{Category}.{Feature}.Recommendation.Reason`

<h3 id="embedded-resource-extraction">埋め込みリソースの抽出</h3>

設定によってはアセンブリから埋め込みリソースを抽出する必要があります：

```csharp
var outputPath = Path.Combine(Shared.AssetsDirectory, nameof(Desktop), "blank.ico");
EmbeddedResourceHelper.TryExtract("Icons.blank.ico", outputPath);
RegistryService.Write(new RegistryItem(Path, "29", outputPath));
```

<h3 id="what-to-override-per-pattern">パターンごとのオーバーライド</h3>

| シナリオ | オーバーライド |
|---|---|
| シンプルなレジストリトグル | `RegistryToggles` + `RefreshScope` |
| 複数のレジストリトグル | `RegistryToggles`（すべてリストする） |
| ドロップダウン/オプション | `ControlType` → `Dropdown`、`Options`、カスタム `ApplyAsync`、`CurrentValue` |
| 複数値ロジック | `GetStateAsync()` + `ApplyAsync()` + `GetWatchedRegistryPaths()` |
| レジストリ操作のない設定 | `GetStateAsync()` + `ApplyAsync()`（完全カスタム） |
| カスタムリフレッシュ動作 | `RefreshScope` または `ExecutePostActionAsync()` |
| 収束チェック付き状態検出 | `GetStateWithRetryAsync()`（組み込み — オーバーライド不要） |
| Windows バージョンごとの動的オプション | `Options` ゲッターを条件付きでオーバーライド |
| 埋め込みリソース抽出 | `EmbeddedResourceHelper.TryExtract()` |

<h3 id="create-a-new-customize-category">新しいカテゴリの作成</h3>

1. `Domain/Customize/Categories/YourCategory.cs` を作成する
2. `[CustomizeCategory(PageType = typeof(YourPage))]` 付きで `ICustomizeCategory` を実装する
3. `Domain/UI/CustomizeOrder.cs` の `CustomizeOrder` 列挙型にメンバーを追加する
4. XAML ページを作成する（`UI/Pages/Customize/Categories/` に新しいクラス）
5. ページは `App.xaml.cs` の `services.AddAllCustomizeCategoryPages()` 経由で自動登録される

<h3 id="localization-keys-customize">カスタマイズ設定のローカライズキー</h3>

```
Customize.{CategoryName}.{SettingKey}.Name
Customize.{CategoryName}.{SettingKey}.Description
Customize.{CategoryName}.{SettingKey}.Options.{OptionKey}    (if using SettingOption)
Customize.{CategoryName}.{SettingKey}.Recommendation.Reason   (if Recommendation != None)
Customize.{CategoryName}.Section.{SectionName}                (for section headers)
```

---

<h1 id="the-refresh-scope-system">リフレッシュスコープシステム</h1>

<h3 id="available-flags">利用可能なフラグ</h3>

| メンバー | 値 | 効果 | P/Invoke |
|---|---|---|---|
| `None` | `0` | リフレッシュなし | — |
| `Settings` | `1 << 0` | `WM_SETTINGCHANGE` をブロードキャスト | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE)` |
| `Associations` | `1 << 1` | ファイル関連付け変更をシェルに通知 | `SHChangeNotify(SHCNE_ASSOCCHANGED)` |
| `Desktop` | `1 << 2` | デスクトップアイコンリストの再描画を強制 | `LVM_REFRESH` + `LVM_UPDATE` |
| `Taskbar` | `1 << 3` | タスクバー向け `WM_SETTINGCHANGE` | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, "TraySettings")` |
| `PolicyUpdate` | `1 << 4` | `SystemParametersInfo` をプッシュ | `SystemParametersInfo(SPI_SETDESKWALLPAPER)` |
| `Theme` | `1 << 5` | `WM_THEMECHANGED` をブロードキャスト | `SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED)` |
| `DesktopIconCache` | `1 << 6` | HideIcons レジストリ + `WM_COMMAND 0x7402` | Registry read + `SendMessage(Progman, WM_COMMAND)` |

<h3 id="named-composites">名前付きコンポジット</h3>

| 名前 | 構成 | 使用例 |
|---|---|---|
| `Default` | `Settings \| Associations` | 一般的なエクスプローラーレベルの設定 |
| `DesktopIcons` | `Settings \| Desktop` | 個別のデスクトップアイコンの表示/非表示 |
| `HideDesktopIcons` | `Settings \| DesktopIconCache` | 「すべてのデスクトップアイコンを非表示」トグル |
| `TaskbarSettings` | `Settings \| Taskbar` | タスクバー設定 |
| `ExplorerView` | `Settings \| Associations \| PolicyUpdate` | ファイル拡張子、隠しファイル |

---

<h1 id="building-new-features">新機能の構築</h1>

新しいページやツール（例：「ネットワークモニター」）を追加する場合：

1. **まず GitHub Issue を作成する** — 機能、ユースケース、設計を説明する。
2. **実装順序**：Service → ViewModel → XAML Page → App.xaml.cs に登録

```csharp
// DI Registration Pattern (from App.xaml.cs)
services.AddSingleton<YourViewModel>();
services.AddSingleton<YourPage>();
services.AddSingleton<ConfigManager>();
services.AddSingleton<RevertManager>();
services.AddSingleton<OptimizationRegistry>();
services.AddSingleton<CustomizeRegistry>();
services.AddSingleton<OptimizationService>();
services.AddSingleton<BloatwareService>();
services.AddSingleton<DiskCleanupService>();
services.AddSingleton<StartupManagerService>();
services.AddSingleton<SystemInfoService>();
services.AddSingleton<StreamService>();
services.AddSingleton<UpdaterService>();
services.AddSingleton<IRegistryWatcher, RegistryWatcher>();
```

<h3 id="system-services">システムサービスリファレンス</h3>

| サービス | 目的 |
|---|---|
| `SystemInfoService` | CPU、RAM、GPU 情報を含む `SystemSnapshot` を提供 |
| `StreamService` | リモートリソースのダウンロード |
| `UpdaterService` | GitHub リリースの更新確認 |
| `RegistryWatcher` | レジストリキーの監視と UI 更新 |
| `BloatwareService` | プリインストール AppX パッケージの一覧表示 |
| `DiskCleanupService` | ディスククリーンアップのスキャン |
| `StartupManagerService` | スタートアップアプリとタスクの管理 |

---

<h1 id="revert-system">リバートシステム</h1>

<h3 id="how-it-works-jp">仕組み</h3>

```
ApplyAsync()
  │
  ├─ ExecutionScope.Begin(optimization, logger)    ← creates ambient AsyncLocal scope
  │
  ├─ RegistryService.Write(...)                     ← auto-records RegistryRevertStep
  ├─ ServiceProcessService.ChangeServiceStartupTypeAsync(...)  ← auto-records ServiceRevertStep
  ├─ ShellService.CMDAsync(...)                     ← auto-records ShellRevertStep
  │
  ├─ CompleteFromScope() → ApplyResult              ← derived from recorded steps
  │
  └─ ExecutionScope disposes → RevertManager.SaveRevertDataAsync()
```

<h3 id="step-types-jp">ステップタイプ</h3>

| ステップタイプ | 記録内容 | 自動作成元 |
|---|---|---|
| **`RegistryRevertStep`** | 変更前の元のレジストリ値 | `RegistryService.Write()`、`DeleteValue()`、`CreateSubKey()`、`DeleteSubKeyTree()` |
| **`ServiceRevertStep`** | 元のサービス起動タイプ | `ServiceProcessService.ChangeServiceStartupTypeAsync()` |
| **`ScheduledTaskRevertStep`** | 元のタスク状態（有効/無効） | `ScheduledTaskService.DisableTask()`、`EnableTask()` |
| **`ShellRevertStep`** | 元に戻すシェルコマンド | `ShellService.CMDAsync()`、`PowerShellAsync()` — `revertCommand` パラメータを渡す |
| **`UsbPowerRevertStep`** | USB 電源設定（デバイス別） | USB 関連の最適化（手動で `ExecutionScope.RecordStep()`） |

リバートコマンドをシェル呼び出しに追加する：

```csharp
await ShellService.CMDAsync("powercfg /h off", "powercfg /h on");  // revertCommand
```

<h3 id="key-details-jp">重要な詳細</h3>

- **適用状態**はディスク上のファイルの存在から推論される
- **アトミック書き込み**：`.tmp` に書き込んでから `File.Replace()`
- **同時アクセス**：ファイルごとの `SemaphoreSlim` ロック、30秒タイムアウト
- **リバートは逆順で実行**（最後に適用 = 最初にリバート）
- **部分的成功**：一部のステップが失敗しても続行
- **リトライ**：`OptimizationService.RetryFailedStepsAsync()`
- **Upsert**：`RevertManager.UpsertRevertStepAtIndexAsync()` で特定インデックスのリバートステップを追加/置換
- **ステップレジストリ**：`IRevertStep` を実装 + 静的な `FromData(JObject)` メソッドで自動登録

---

<h1 id="testing">テスト</h1>

<h3 id="test-patterns-jp">テストパターン</h3>

| パターン | 詳細 |
|---|---|
| **モックライブラリなし** | すべてのテストダブルは手書きクラス |
| **実際の I/O** | 実際のファイルシステム、レジストリ、プロセス実行 |
| **クリーンアップ** | `try/finally` または `IDisposable` |
| **命名** | `{Method}_{Scenario}_{ExpectedResult}` |
| **ログ記録** | `NullLogger<T>.Instance` |
| **STA スレッド** | WPF コンポーネントを含むテストは `RunInStaThreadAsync` ヘルパーを使用 |

```bash
# Running tests
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build
```

<h3 id="ci-integration-jp">CI 統合</h3>

CI パイプライン（`ci.yml`）は以下のコマンドを実行します：

```bash
dotnet restore optimizerDuck.slnx
dotnet build optimizerDuck.slnx --configuration Release --no-restore
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build --blame-hang --blame-hang-timeout 30s
```

---

<h1 id="coding-standards">コーディング規約</h1>

<h3 id="language-features-jp">言語機能</h3>

| 機能 | 使用 | 備考 |
|---|---|---|
| ファイルスコープ名前空間 | はい | `namespace X.Y;` |
| コレクション式 | はい | `[]`、`[item1, item2]` |
| プライマリコンストラクタ | 一部 | シンプルな型で使用 |
| 暗黙的 using | はい | `.csproj` で有効 |
| Null 許容参照型 | はい | `<Nullable>enable</Nullable>` |
| 拡張メソッド（`extension(T type)`） | はい | C# 13 機能 |

<h3 id="naming-conventions-jp">命名規則</h3>

| 要素 | 規則 | 例 |
|---|---|---|
| クラス、列挙型、メソッド、プロパティ | `PascalCase` | `RegistryService` |
| プライベートフィールド | `_camelCase` | `_lastError` |
| ローカル変数、パラメータ | `camelCase` | `progress` |
| 非同期メソッド | `*Async` サフィックス | `ChangeServiceStartupTypeAsync` |
| 定数 | `PascalCase` / `_PascalCase` | `MaxRetries` / `_defaultTimeout` |

<h3 id="formatting-jp">フォーマット</h3>

| 設定 | 値 |
|---|---|
| インデント | 4 スペース |
| 最大行長 | 100 文字 |
| フォーマッター | **CSharpier** — `dotnet csharpier .` |

<h3 id="code-style-jp">コードスタイル</h3>

- **ハードコードされた文字列は禁止** — 常に `Translations.KeyName` または `Loc.Instance["Key"]`
- **コメントは最小限に**
- **既存のライブラリを優先**
- **`@formatter:off` / `@formatter:on`** を使用して大きなレジストリ書き込みブロックの自動フォーマットを抑制可能

<h3 id="error-handling-jp">エラー処理</h3>

| レイヤー | プラクティス |
|---|---|
| **最適化** | `ApplyResult.False("reason")` を返す。例外はスローしない |
| **プロバイダーサービス** | try/catch + `ExecutionScope.LogError` |
| **ViewModel** | コマンドハンドラーで例外をキャッチ、スナックバー表示 |
| **グローバル** | App.xaml.cs で 3 つのグローバル例外ハンドラを登録。クラッシュは `%localappdata%\optimizerDuck\Crashes\crash_*.log` に記録 |

---

<h1 id="localization">ローカライズ</h1>

<h3 id="resx-files-jp">RESX ファイル</h3>

すべてのユーザー向け文字列は `Resources/Languages/Translations.resx` に格納されています。

- `Translations.Designer.cs` は直接編集しない
- `{0}`、`{1}` などのフォーマットパラメータは正確に保持する

<h3 id="available-locales-jp">利用可能なロケール（11 言語 + 英語）</h3>

| 言語 | ファイル |
|---|---|
| English | `Translations.resx` (default) |
| Vietnamese | `Translations.vi-VN.resx` |
| Spanish | `Translations.es-ES.resx` |
| French | `Translations.fr-FR.resx` |
| Traditional Chinese | `Translations.zh-TW.resx` |
| Simplified Chinese | `Translations.zh-CN.resx` |
| Russian | `Translations.ru-RU.resx` |
| Korean | `Translations.ko-KR.resx` |
| Japanese | `Translations.ja-JP.resx` |
| Polish | `Translations.pl-PL.resx` |
| Turkish | `Translations.tr-TR.resx` |
| Portuguese (Brazil) | `Translations.pt-BR.resx` |

<h3 id="hardcoded-string-rule-jp">文字列ハードコードの禁止</h3>

```csharp
string title = Translations.Features_Desktop_Name;
string title = Loc.Instance[$"Optimizer.{category}.{key}.Name"];
```

XAML：

```xml
<ui:TextBlock Text="{ext:Loc Dashboard.Header.Title}" />
```

---

<h1 id="pull-request-process">プルリクエストの手順</h1>

1. `master` からブランチを作成：`feature/name` または `fix/issue-id`
2. Conventional Commits：`feat:`、`fix:`、`refactor:`、`docs:`、`test:`、`i18n:`、`chore:`
3. プッシュ前に確認：

```bash
dotnet build optimizerDuck.slnx --configuration Release
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build
dotnet csharpier .
```

4. PR を作成：変更内容と理由を説明、UI 変更のスクリーンショットを含める、関連 Issue を `Closes #42` でリンク

<h3 id="pr-checklist-jp">PR チェックリスト</h3>

- [ ] 既存のパターンに従っている
- [ ] ローカライズキーが `Translations.resx` に追加されている
- [ ] `dotnet build` 成功
- [ ] `dotnet test` 成功
- [ ] `dotnet csharpier .` 実行済み
- [ ] ハードコード文字列なし
- [ ] リバートステップが記録されている（該当する場合）
- [ ] UI 変更のスクリーンショットあり

---

<h1 id="issue-guidelines">Issue ガイドライン</h1>

- **バグ報告**：再現手順、期待/実際の動作、`%localappdata%\optimizerDuck\optimizerDuck.log` のログ + システム仕様
- **機能リクエスト**：ユースケース、解決する問題、動作
- **最適化の提案**：レジストリパス、サービス名、CLI コマンド、情報源へのリンク
- **質問**：GitHub Discussions または [Discord](https://discord.gg/tDUBDCYw9Q)

---

<h1 id="faq--troubleshooting">FAQ とトラブルシューティング</h1>

<h3>ビルドが CA1416 エラーで失敗する</h3>
`.editorconfig` が CA1416 を抑制。最新の `.editorconfig` を確認。

<h3>最適化が UI に表示されない</h3>
- カテゴリクラス内のネストされた public クラス？
- カテゴリが `IOptimizationCategory` を実装？
- `BaseOptimization` を継承？
- `[Optimization(Id = "...")]` 属性？
- ローカライズキーが `Translations.resx` に追加されている？
- `OptimizationRegistry.IsPreloaded` を確認？

<h3>カスタマイズ設定が表示されない</h3>
- `[CustomizeSetting(Section = ..., Icon = ...)]` 属性？
- `Section` のスペルは正しい？
- `[CustomizeCategory(PageType = ...)]` 属性？

<h3>UI がフリーズする</h3>
`async`/`await` を使用していることを確認。`.Result` / `.Wait()` はフリーズの原因。

<h3>GUID の生成</h3>

```powershell
[guid]::NewGuid()
```

<h3>翻訳がキー名として表示される</h3>
`Translations.resx` へのローカライズキーの追加を忘れている。

<h3>「No revert data」エラー</h3>
最適化の `Id` GUID が変更されていないことを確認。

<h3>新しいリバートステップタイプの追加</h3>
1. `Domain/Revert/Steps/` に `IRevertStep` を実装する新しいクラスを作成
2. 静的な `FromData(JObject data)` メソッドを追加（デシリアライズ用）
3. `RevertManager` のリフレクションベースの `_stepRegistry` が自動検出
4. `ExecutionScope.RecordStep()` で記録

<h3>クラッシュセーフティ</h3>
- リバートファイル：アトミック書き込み（`.tmp` + `File.Replace`）
- クラッシュログ：`%localappdata%\optimizerDuck\Crashes\crash_*.log`
- `WmiHelper.Initialize()` が起動時に異常終了クリーンアップを登録
- App.xaml.cs に 3 つのグローバル例外ハンドラ

---

<div align="center">

<h2 id="credits">クレジット</h2>

マージされた PR の貢献者はリリースノートに記載されます。

---

<h2 id="license">ライセンス</h2>

optimizerDuck に貢献することにより、あなたの貢献はプロジェクトの [GPL v3 ライセンス](../LICENSE) の下でライセンスされることに同意したものとみなされます。

---

<p><i>optimizerDuck をより良くしてくれてありがとう。</i></p>

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
