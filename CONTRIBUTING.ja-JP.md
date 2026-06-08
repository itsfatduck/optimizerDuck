<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

[English](CONTRIBUTING.md) | **日本語**

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

公開プロファイルは `Properties/PublishProfiles/` で定義されています。

<h3 id="quick-start-checklist">5. クイックスタートチェックリスト</h3>

初めて貢献する前に：

- [ ] リポジトリをフォークしてクローンする
- [ ] `dotnet build` が成功する（エラー 0 件）
- [ ] `dotnet test` が通る（166 件以上のテストがすべて成功）
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
│   │
│   ├── Domain/                             # Pure models, interfaces, attributes (no WPF deps)
│   │   ├── Abstractions/                   # IOptimization, ICustomizeSetting, IRevertStep, etc.
│   │   ├── Attributes/                     # [Optimization], [CustomizeSetting], [OptimizationCategory]
│   │   ├── Configuration/                  # AppSettings model
│   │   ├── Execution/                      # ExecutionScope — ambient step tracking via AsyncLocal
│   │   ├── Customize/                      # Customize settings (Desktop, Gaming, Preferences, System)
│   │   │   ├── Categories/                 # Category classes with nested setting classes
│   │   │   └── Models/                     # BaseCustomizeSetting, RegistryToggle, RefreshScope
│   │   ├── Optimizations/                  # Optimizations (Performance, Privacy, GPU, etc.)
│   │   │   ├── Categories/                 # Category classes with nested optimization classes
│   │   │   └── Models/                     # BaseOptimization, ApplyResult, OptimizationContext
│   │   ├── Revert/                         # RevertData, RevertResult, revert step types
│   │   │   └── Steps/                      # RegistryRevertStep, ServiceRevertStep, etc.
│   │   └── UI/                             # Enums: OptimizationRisk, OptimizationTags, CategoryOrder
│   │
│   ├── Common/                             # Shared helpers, extensions, converters
│   │   ├── Extensions/                     # StringExtensions, CustomizePageRegistryExtensions
│   │   ├── Converters/                     # WPF value converters
│   │   └── Helpers/                        # Shared.cs, ReflectionHelper.cs, SystemRefreshService.cs
│   │
│   ├── Services/                           # Business logic
│   │   ├── Configuration/                  # ConfigManager, LanguageManager
│   │   ├── Customize/                      # CustomizeRegistry (discovery via reflection)
│   │   ├── Managers/                       # BloatwareService, DiskCleanupService,
│   │   │                                   # StartupManagerService, SystemInfoService,
│   │   │                                   # StreamService, UpdaterService
│   │   ├── Optimization/                   # OptimizationRegistry, OptimizationService
│   │   │   └── Providers/                  # Static: RegistryService, ShellService,
│   │   │                                   # ScheduledTaskService, ServiceProcessService
│   │   ├── Revert/                         # RevertManager (writes/reads revert JSON files)
│   │   ├── System/                         # RegistryWatcher
│   │   └── UI/                             # ContentDialogService, etc.
│   │
│   ├── UI/                                 # WPF pages, ViewModels, controls, styles
│   │   ├── Controls/                       # Custom WPF controls
│   │   ├── Dialogs/                        # Dialog windows (ProcessingDialog, OptimizationResultDialog)
│   │   ├── Pages/                          # App pages + sub-folders (Optimize/, Customize/)
│   │   ├── Styles/                         # Fluent design styles
│   │   ├── ViewModels/                     # Page and dialog ViewModels
│   │   │   ├── Customize/                  # CustomizeItemViewModel, CustomizeGroupViewModel
│   │   │   ├── Dialogs/                    # ProcessingViewModel, OptimizationResultDialogViewModel
│   │   │   ├── Optimizer/                  # OptimizationCategoryViewModel
│   │   │   ├── Pages/                      # Dashboard, Optimize, Customize, Settings, etc.
│   │   │   └── Windows/                    # MainWindowViewModel
│   │   └── Windows/                        # MainWindow
│   │
│   └── Resources/                          # Images, embedded assets, localization
│       ├── Embedded/                       # Power plans, icons
│       ├── Images/                         # Duck.png, logos
│       └── Languages/                      # Translations.resx + 7 locale variants
│
└── optimizerDuck.Test/                     # xUnit v3 test project (166+ tests)
    ├── Common/Helpers/
    ├── Domain/
    │   ├── Customize/
    │   ├── Exceptions/
    │   ├── Optimizations/
    │   └── Revert/Steps/
    └── Services/
        ├── Managers/
        └── OptimizationServices/
```

<h3 id="key-design-decisions">主要な設計判断</h3>

| 判断 | 理由 |
|---|---|
| **リフレクションによる自動検出** | DI 登録配列を更新する必要がありません。`ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` が起動時に `optimizerDuck.*` アセンブリをスキャンします。新しい最適化や設定は自動的に検出されます。 |
| **静的プロバイダーサービス** | `RegistryService`、`ShellService`、`ScheduledTaskService`、`ServiceProcessService` は静的クラスです。アンビエントな `ExecutionScope` にリバートステップを記録するため、コンテキストの注入や受け渡しは不要です。 |
| **ファイルベースのリバート追跡** | 適用状態 = ディスク上にファイルが存在する（`%localappdata%\optimizerDuck\Revert\{id}.json`）。データベースは使用しません。`File.Replace()` によるアトミック書き込み。 |
| **統合スタイルのテスト** | 実際のファイルシステム、実際のレジストリ（`HKCU\Software\TestOptimizerDuck*` 配下）、実際のプロセス実行。モックライブラリは使用せず、手書きのテストダブルのみ。 |
| **非同期サービスメソッド** | 外部プロセスを実行するプロバイダーメソッドは非同期（`*Async` サフィックス）です。最適化の `ApplyAsync` メソッドでは `async`/`await` を使用して UI の応答性を保ってください。 |

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

---

<h1 id="creating-an-optimization">最適化の作成</h1>

<h3 id="how-discovery-works">検出の仕組み</h3>

起動時：

1. `ReflectionHelper.FindImplementationsInLoadedAssemblies<IOptimizationCategory>()` がすべての `optimizerDuck.*` アセンブリをスキャンする
2. `IOptimizationCategory` を実装するすべてのクラスを見つける
3. 各カテゴリについて、`IOptimization` を実装する**ネストされた public クラス**をスキャンする
4. 検出されたすべての最適化がインスタンス化され、`OwnerType` が自動的に割り当てられる

**あなたの作業**：カテゴリ内にネストされたクラスを作成し、`BaseOptimization` を継承し、`[Optimization]` を付与する。以上です。

<h3 id="optimization-categories">最適化カテゴリ</h3>

現在のカテゴリ（`Domain/Optimizations/Categories/` 内）：

| ファイル | 属性 | 対象 |
|---|---|---|
| `Performance.cs` | `[OptimizationCategory(typeof(PerformanceOptimizerPage))]` | RAM 調整、プロセス優先度、キーボードレイテンシ、マルチメディアスケジューラ |
| `SecurityAndPrivacy.cs` | `[OptimizationCategory(typeof(SecurityAndPrivacyOptimizerPage))]` | テレメトリ、エラー報告、広告 ID、位置情報、Cortana、Copilot |
| `Gpu.cs` | `[OptimizationCategory(typeof(GpuOptimizerPage))]` | AMD/NVIDIA/Intel レジストリ調整、電源状態、クロックゲーティング |
| `PowerManagement.cs` | `[OptimizationCategory(typeof(PowerManagementOptimizerPage))]` | 休止状態、高速スタートアップ、USB 選択的サスペンド、カスタム電源プラン |
| `BloatwareAndServices.cs` | `[OptimizationCategory(typeof(BloatwareAndServicesOptimizerPage))]` | OEM 再インストールのブロック、200 以上の Windows サービス起動タイプ |
| `UserExperience.cs` | `[OptimizationCategory(typeof(UserExperienceOptimizerPage))]` | メニュー遅延、視覚効果、タスクバーアニメーション、透明度 |

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

<h3 id="available-service-providers">利用可能なサービスプロバイダー</h3>

これらの**静的**クラスは、ログ記録、エラー処理、リバートステップの自動記録を担当します。

| サービス | 主要メソッド | 使用理由 |
|---|---|---|
| **`RegistryService`** | `Write()`、`Read<T>()`、`DeleteValue()`、`CreateSubKey()`、`DeleteSubKeyTree()` | レジストリキーの読み書き/削除。リバート用に元の値をバックアップする。 |
| **`ShellService`** | `CMDAsync()`**、`PowerShellAsync()`** | CMD または PowerShell コマンドの実行。常に非同期バリアントを使用する。 |
| **`ScheduledTaskService`** | `DisableTask()`、`EnableTask()`、`IsTaskEnabled()`、`DeleteTask()` | Windows スケジュールタスクの管理。 |
| **`ServiceProcessService`** | `ChangeServiceStartupTypeAsync()`**、`GetStartupTypeAsync()`** | Windows サービスの管理。常に非同期バリアントを使用する。 |

> **`**` が付いたメソッドは非同期です。** 最適化の `ApplyAsync` 内で `await` を使用して呼び出してください。

使用例：

```csharp
// Sync registry writes
RegistryService.Write(new RegistryItem(@"HKLM\...", "Value", 1));
RegistryService.DeleteValue(new RegistryItem(@"HKCU\...", "OldValue"));

// Async service changes
await ServiceProcessService.ChangeServiceStartupTypeAsync(
    new ServiceItem("DiagTrack", ServiceStartupType.Disabled));

// Async shell commands
var result = await ShellService.PowerShellAsync("Some-Command");
```

<h3 id="create-a-new-category">新しいカテゴリの作成</h3>

最適化が既存のカテゴリに当てはまらない場合のみ。過度に細かいカテゴリは避けてください。

1. `Domain/Optimizations/Categories/YourCategory.cs` を作成する
2. `IOptimizationCategory` を実装する
3. `[OptimizationCategory(PageType = typeof(YourPage))]` を適用する — XAML ページも必要です
4. `Domain/UI/OptimizationCategoryOrder.cs` の `OptimizationCategoryOrder` 列挙型にメンバーを追加する
5. XAML ページは `App.xaml.cs` の `services.AddAllOptimizationPages()` 経由で自動登録される

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
| `Desktop.cs` | `[CustomizeCategory(PageType = typeof(DesktopFeatureCategory))]` | デスクトップアイコン（PC、ごみ箱、ネットワーク）、ショートカットオーバーレイ |
| `Preferences.cs` | `[CustomizeCategory(PageType = typeof(PreferencesFeatureCategory))]` | タスクバーの配置、ウィジェット、ダークモード、ファイル拡張子、隠しファイルなど |
| `Gaming.cs` | `[CustomizeCategory(PageType = typeof(GamingFeatureCategory))]` | ゲームモード、ゲームバー、マウス加速度、フルスクリーン最適化、GPU スケジューリング |
| `SystemFeatures.cs` | `[CustomizeCategory(PageType = typeof(SystemFeatureCategory))]` | 起動時の Num Lock |

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

**状態検出ロジック**：`GetState()`（`BaseCustomizeSetting` 内）がすべての非オプションの `RegistryToggles` を収集し、**すべて**の必須トグルが `OnValue` と一致する場合にのみ `true` を返します。

<h3 id="control-types">コントロールタイプ</h3>

| 型 | 表示形式 | 用途 |
|---|---|---|
| `Toggle` | ON/OFF スイッチ | ほとんどの設定（デフォルト） |
| `Dropdown` | コンボボックス | 複数選択（例：電源プラン） |
| `Option` | ラジオボタングループ | 排他的な視覚オプション（例：左/中央揃え） |
| `NumberInt` | 整数テキスト入力 | 数値（例：秒数） |
| `NumberFloat` | 小数テキスト入力 | 精度の高い値 |
| `String` | テキスト入力 | 自由形式のテキスト |

`ControlType` をオーバーライドして UI コントロールを変更します：

```csharp
public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;
```

<h3 id="dropdown-with-options">オプション付きドロップダウン</h3>

複数の選択肢がある設定の場合：

```csharp
public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;

public override IReadOnlyList<SettingOption>? Options =>
    [
        Option("Never", 0),      // Option() helper reads from Translations.resx:
        Option("Battery", 1),    //   Customize.{Category}.{Feature}.Options.Never
        Option("Always", 2),     //   Customize.{Category}.{Feature}.Options.Battery
    ];

public override async Task ApplyAsync(object? value)
{
    var intValue = value is int i ? i : 0;
    RegistryService.Write(new RegistryItem(Path, "ValueName", intValue));
    await ExecutePostActionAsync();  // MUST call when overriding ApplyAsync
}
```

<h3 id="custom-logic-override">カスタムロジック（GetStateAsync / ApplyAsync のオーバーライド）</h3>

シンプルなレジストリトグルではない設定の場合（例：マウス加速度は 3 つのレジストリ値を組み合わせる）：

```csharp
[CustomizeSetting(
    Section = nameof(Sections.Input),
    Icon = SymbolRegular.Cursor24,
    Recommendation = RecommendationState.Off
)]
public class MouseAcceleration : BaseCustomizeSetting
{
    private const string Path = @"HKCU\Control Panel\Mouse";

    // Watched paths let the UI auto-refresh when external changes occur
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

<h3 id="what-to-override-per-pattern">パターンごとのオーバーライド</h3>

| シナリオ | オーバーライド |
|---|---|
| シンプルなレジストリトグル | `RegistryToggles` + `RefreshScope` |
| 複数のレジストリトグル | `RegistryToggles`（すべてリストする） |
| ドロップダウン/オプション | `ControlType` → `Dropdown`、`Options`、カスタム `ApplyAsync` |
| 複数値ロジック（例：マウス加速度） | `GetStateAsync()` + `ApplyAsync()` + `GetWatchedRegistryPaths()` |
| レジストリ操作のない設定 | `GetStateAsync()` + `ApplyAsync()`（完全カスタム） |
| カスタムリフレッシュ動作 | `RefreshScope`（フラグのみ変更の場合）または `ExecutePostActionAsync()`（完全オーバーライド） |

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

カスタマイズ設定の状態が変更されると、異なる Windows サーフェスに異なるリフレッシュ戦略が必要になります。`CustomizeRefreshScope` [Flags] 列挙型がこれを細かく制御します。

<h3 id="available-flags">利用可能なフラグ</h3>

| メンバー | 値 | 効果 | P/Invoke |
|---|---|---|---|
| `None` | `0` | リフレッシュなし | — |
| `Settings` | `1 << 0` | `WM_SETTINGCHANGE` をブロードキャストしてアプリがレジストリを再読み込み | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE)` |
| `Associations` | `1 << 1` | ファイル関連付けやアイコンキャッシュの変更をシェルに通知 | `SHChangeNotify(SHCNE_ASSOCCHANGED)` |
| `Desktop` | `1 << 2` | デスクトップアイコンリスト（`SysListView32`）の再描画を強制 | `LVM_REFRESH` + `LVM_UPDATE` |
| `Taskbar` | `1 << 3` | タスクバー向けの `WM_SETTINGCHANGE`（"TraySettings"）をブロードキャスト | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, "TraySettings")` |
| `PolicyUpdate` | `1 << 4` | ユーザーごとのパラメータに `SPIF_SENDCHANGE` 付きで `SystemParametersInfo` をプッシュ | `SystemParametersInfo(SPI_SETDESKWALLPAPER)` |
| `Theme` | `1 << 5` | テーマ/視覚調整用に `WM_THEMECHANGED` をブロードキャスト | `SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED)` |
| `DesktopIconCache` | `1 << 6` | HideIcons レジストリを切り替え + デスクトップに `WM_COMMAND 0x7402` を送信 | Registry read + `SendMessage(Progman, WM_COMMAND)` |

<h3 id="named-composites">名前付きコンポジット</h3>

| 名前 | 構成 | 使用例 |
|---|---|---|
| `Default` | `Settings \| Associations` | 一般的なエクスプローラーレベルの設定 |
| `DesktopIcons` | `Settings \| Desktop` | 個別のデスクトップアイコンの表示/非表示（PC、ごみ箱） |
| `HideDesktopIcons` | `Settings \| DesktopIconCache` | グローバルな「すべてのデスクトップアイコンを非表示」トグル |
| `TaskbarSettings` | `Settings \| Taskbar` | タスクバーの配置、ウィジェット、タスクビュー、タスクの終了 |
| `ExplorerView` | `Settings \| Associations \| PolicyUpdate` | ファイル拡張子、隠しファイル、コンパクト表示 |

<h3 id="how-refresh-flows">リフレッシュの流れ</h3>

```
Setting toggle → BaseCustomizeSetting.ApplyAsync(value)
  ├─ Writes RegistryToggles (if any)
  ├─ Checks NeedsPostAction (true if RefreshScope != None)
  └─ Task.Run → ExecutePostActionAsync()
       ├─ Checks each CustomizeRefreshScope flag
       ├─ Calls SystemRefreshService methods (P/Invoke)
       └─ Win32 notifications sent to Windows
```

`ApplyAsync` をオーバーライドする場合、リフレッシュをトリガーするために**必ず** `await ExecutePostActionAsync()` を自分で呼び出してください。基底クラスは、デフォルトの `RegistryToggles` ベースの適用を使用する場合にのみ自動的にこれを行います。

---

<h1 id="building-new-features">新機能の構築</h1>

新しいページやツール（例：「ネットワークモニター」）を追加する場合：

1. **まず GitHub Issue を作成する** — 機能、ユースケース、設計を説明する。メンテナーのフィードバックを待つ。
2. **実装順序**：

```csharp
// 1. Service layer in Services/Managers/YourService.cs
public class YourService(ILogger<YourService> logger) { ... }

// 2. ViewModel in UI/ViewModels/Pages/YourViewModel.cs
//    Extends ViewModel (which extends ObservableValidator + INavigationAware)

// 3. XAML Page in UI/Pages/YourPage.xaml (+ code-behind)

// 4. Register as singletons in App.xaml.cs
services.AddSingleton<YourViewModel>();
services.AddSingleton<YourPage>();
```

- ViewModel と Page は `App.xaml.cs` で**シングルトンとして登録する必要があります**
- ナビゲーションは WPF UI（`INavigationService`）が処理します
- 既存のパターンに従う — `DashboardPage`、`OptimizePage` などを参照してください

<h3 id="di-registration-pattern">DI 登録パターン（App.xaml.cs より）</h3>

```csharp
// Pages + ViewModels — one pair per feature
services.AddSingleton<DashboardViewModel>();
services.AddSingleton<DashboardPage>();

services.AddSingleton<OptimizeViewModel>();
services.AddSingleton<OptimizePage>();

// Managers
services.AddSingleton<ConfigManager>();
services.AddSingleton<RevertManager>();

// Services
services.AddSingleton<OptimizationRegistry>();
services.AddSingleton<CustomizeRegistry>();
services.AddSingleton<OptimizationService>();
services.AddSingleton<UpdaterService>();
services.AddSingleton<IRegistryWatcher, RegistryWatcher>();

// Automatic page registration (category pages only)
services.AddAllCustomizeCategoryPages();   // scans [CustomizeCategory] attributes
services.AddAllOptimizationPages();        // scans [OptimizationCategory] attributes
```

---

<h1 id="revert-system">リバートシステム</h1>

適用されたすべての最適化は、`%localappdata%\optimizerDuck\Revert\{optimizationId}.json` に JSON ファイルを作成します。

<h3 id="how-it-works">仕組み</h3>

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

<h3 id="step-types">ステップタイプ</h3>

| ステップタイプ | 記録内容 | 自動作成元 |
|---|---|---|
| **`RegistryRevertStep`** | 変更前の元のレジストリ値 | `RegistryService.Write()`、`RegistryService.DeleteValue()`、`RegistryService.CreateSubKey()`、`RegistryService.DeleteSubKeyTree()` |
| **`ServiceRevertStep`** | 元のサービス起動タイプ | `ServiceProcessService.ChangeServiceStartupTypeAsync()` |
| **`ScheduledTaskRevertStep`** | 元のタスク状態（有効/無効） | `ScheduledTaskService.DisableTask()`、`ScheduledTaskService.EnableTask()` |
| **`ShellRevertStep`** | 変更を元に戻すシェルコマンド | `ShellService.CMDAsync()`、`ShellService.PowerShellAsync()` |
| **`UsbPowerRevertStep`** | USB 電源設定 | USB 関連の最適化 |

<h3 id="revert-data-format">リバートデータ形式</h3>

```json
{
  "SchemaVersion": 1,
  "OptimizationId": "guid",
  "OptimizationName": "DisableTelemetry",
  "AppliedAt": "2026-06-02T12:00:00Z",
  "Steps": [
    { "Index": 0, "Type": "Registry", "Data": { ... } },
    null,                    // null gap = failed step at this index
    { "Index": 2, "Type": "Service", "Data": { ... } }
  ]
}
```

<h3 id="key-details">重要な詳細</h3>

- **適用状態**はディスク上のファイルの存在から推論される（`RevertManager.IsAppliedAsync(id)`）
- **アトミック書き込み**：`.tmp` に書き込んでから `File.Replace()` — クラッシュに安全
- **`ExecutionScope`** はアンビエントなステップ追跡に `AsyncLocal<ExecutionScope?>` を使用。パラメータ経由でコンテキストを渡す必要はない
- **リバートは逆順でステップを実行する**（最後に適用されたものが最初にリバートされる）
- **部分的成功**：一部のステップが失敗してもリバートは続行される。失敗したステップにはリトライアクションが記録される
- **リトライ**：`OptimizationService.RetryFailedStepsAsync()` で個別の失敗ステップをリトライできる

> **重要**：プロバイダーサービス（`RegistryService.Write`、`ShellService.CMDAsync` など）を呼び出すと、リバートステップは自動的に記録されます。リバートステップを手動で作成しないでください。

---

<h1 id="testing">テスト</h1>

テストは **xUnit v3** を使用し、実際の I/O を伴う統合スタイルのアプローチに従います。

<h3 id="test-patterns">テストパターン</h3>

| パターン | 詳細 |
|---|---|
| **モックライブラリなし** | すべてのテストダブルはインターフェースを実装する手書きクラス |
| **実際の I/O** | 実際のファイルシステム（リバート JSON ファイル）、実際のレジストリ（`HKCU\Software\TestOptimizerDuck*`）、実際のプロセス実行（CMD、PowerShell） |
| **クリーンアップ** | テスト成果物のクリーンアップに `try/finally` または `IDisposable` を使用 |
| **命名** | `{Method}_{Scenario}_{ExpectedResult}` — 例：`ApplyAsync_Success_PersistsRevertDataFile` |
| **ログ記録** | DI ログパラメータに `NullLogger<T>.Instance` / `NullLoggerFactory.Instance` を使用 |
| **STA スレッド** | `ContentDialogService` や WPF コンポーネントを含むテストは `RunInStaThreadAsync` ヘルパーを使用する必要がある |

<h3 id="test-structure">テスト構成</h3>

```
optimizerDuck.Test/
├── Common/Helpers/
│   └── SystemRefreshServiceTests.cs
├── Domain/
│   ├── Customize/
│   │   └── BaseCustomizeSettingTests.cs
│   ├── Exceptions/
│   │   └── StepExecutionExceptionTests.cs
│   ├── Optimizations/
│   │   ├── PowerManagementTests.cs
│   │   └── Models/Services/RegistryItemKindDetectionTests.cs
│   └── Revert/Steps/
│       ├── ScheduledTaskRevertStepTests.cs
│       └── RevertStepSerializationTests.cs
└── Services/
    ├── ApplyRevertComprehensiveTests.cs
    ├── OptimizationServiceTests.cs
    ├── OptimizationServiceIntegrationTests.cs
    ├── OptimizationExecutionContextTests.cs
    ├── OptimizationServices/
    │   ├── RegistryServiceTests.cs
    │   ├── ShellServiceTests.cs
    │   └── ShellPolicyTests.cs
    ├── Managers/
    │   └── RevertManagerTests.cs
    ├── RegistryWatcherTests.cs
    └── SystemInfoServiceTests.cs
```

<h3 id="running-tests">テストの実行</h3>

```bash
# After building
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build

# Build + test in one step
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release
```

<h3 id="writing-tests-for-provider-services">プロバイダーサービスのテスト作成</h3>

```csharp
public class MyOptimizationTests
{
    [Fact]
    public async Task ApplyAsync_Success_PersistsRevertDataFile()
    {
        var optimization = new TestOptimization
        {
            ApplyImpl = _ =>
            {
                ExecutionScope.RecordStep("Test", "Step 1", true, ...);
                return Task.FromResult(ApplyResult.True());
            },
        };

        var service = CreateService();
        var result = await service.ApplyAsync(optimization, new Progress<ProcessingProgress>());

        Assert.Equal(OptimizationSuccessResult.Success, result.Status);
    }

    private static OptimizationService CreateService()
    {
        return new OptimizationService(
            new RevertManager(NullLogger<RevertManager>.Instance, NullLoggerFactory.Instance),
            NullLoggerFactory.Instance,
            new SystemInfoService(NullLogger<SystemInfoService>.Instance),
            new StreamService(NullLogger<StreamService>.Instance),
            null!,
            NullLogger<OptimizationService>.Instance
        );
    }
}
```

---

<h1 id="coding-standards">コーディング規約</h1>

<h3 id="language-features">言語機能</h3>

| 機能 | 使用 | 備考 |
|---|---|---|
| ファイルスコープ名前空間 | はい | `namespace X.Y;` |
| コレクション式 | はい | 空は `[]`、リストは `[item1, item2]` |
| プライマリコンストラクタ | 一部 | シンプルな型で使用 |
| 暗黙的 using | はい | `.csproj` で有効 |
| Null 許容参照型 | はい | `<Nullable>enable</Nullable>` — null を適切に処理する |
| 拡張メソッド（`extension(T type)`） | はい | C# 13 機能、`OptimizationTagsToDisplay` で使用 |

<h3 id="naming-conventions">命名規則</h3>

| 要素 | 規則 | 例 |
|---|---|---|
| クラス、列挙型、インターフェース、メソッド、プロパティ | `PascalCase` | `RegistryService`、`ApplyAsync` |
| プライベートフィールド | `_camelCase` | `_lastError`、`registryService` |
| ローカル変数、パラメータ | `camelCase` | `progress`、`serviceName` |
| 非同期メソッド | `*Async` サフィックス | `ChangeServiceStartupTypeAsync`、`CMDAsync` |
| public 定数 | `PascalCase` | `MaxRetries` |
| private 定数 | `_PascalCase` | `_defaultTimeout` |

<h3 id="formatting">フォーマット</h3>

| 設定 | 値 |
|---|---|
| インデント | 4 スペース（タブなし） |
| 改行コード | LF |
| エンコーディング | UTF-8 |
| 最大行長 | 100 文字 |
| 末尾の空白 | トリミング |
| 最終改行 | 必須 |
| フォーマッター | **CSharpier** — コミット前に `dotnet csharpier .` を実行 |
| CA1416 | `.editorconfig` で抑制 — すべてのコードは Windows 専用 |

<h3 id="code-style">コードスタイル</h3>

- **ハードコードされた文字列は禁止** — 常に `Translations.KeyName` または `Loc.Instance["Key"]` を使用する
- **コメントは最小限に** — 既存のコードにはほとんどコメントがない。不要なコメントは追加しない
- **型エラーの抑制は禁止** — C# には `as any` / `@ts-ignore` に相当するものはない。型を適切に処理する
- **新しい依存関係より既存のライブラリを優先する**
- **大規模なリファクタリングより小さく焦点を絞った変更を優先する**

<h3 id="dependency-injection">依存性注入</h3>

- サービス、ViewModel、Page は `App.xaml.cs` でシングルトンとして登録される
- コンストラクタインジェクションを使用する：`public class Foo(Bar bar, Baz baz)`
- 静的プロバイダーサービス（`RegistryService`、`ShellService` など）は注入されない — 直接アクセスする
- テストダブルは手書き（Moq などのモックライブラリは使用しない）

<h3 id="error-handling">エラー処理</h3>

| レイヤー | プラクティス |
|---|---|
| **最適化** | 例外をスローする代わりに `ApplyResult.False("reason")` を返す。ステップレベルの失敗追跡は `ExecutionScope` に任せる |
| **プロバイダーサービス** | システム呼び出しの周りに try/catch を使用し、`ExecutionScope.LogError` 経由でエラーをログに記録する。リトライアクション付きで失敗ステップを記録する |
| **ViewModel** | コマンドハンドラーで例外をキャッチし、ユーザーフレンドリーなスナックバーを表示する |
| **禁止事項** | 処理できない例外をキャッチしない。すべての例外を黙って飲み込まない |

---

<h1 id="localization">ローカライズ</h1>

<h3 id="resx-files">RESX ファイル</h3>

すべてのユーザー向け文字列は `Resources/Languages/Translations.resx` に格納されています。C# では型安全な `Translations` クラス、または動的ルックアップには `Loc.Instance["Key"]` を使用します。

- `Translations.Designer.cs` は自動生成されるため、**直接編集しない**
- [ResXManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager)（VS）または Rider の組み込みリソースエディターを使用する
- `{0}`、`{1}` などのフォーマットパラメータは正確に保持する
- 文字列は簡潔に — 一部の UI カードには幅の制限がある

<h3 id="available-locales">利用可能なロケール</h3>

| 言語 | ファイル |
|---|---|
| English | `Translations.resx` (default) |
| Vietnamese | `Translations.vi-VN.resx` |
| French | `Translations.fr-FR.resx` |
| Traditional Chinese | `Translations.zh-TW.resx` |
| Simplified Chinese | `Translations.zh-CN.resx` |
| Russian | `Translations.ru-RU.resx` |
| Korean | `Translations.ko-KR.resx` |
| Polish | `Translations.pl-PL.resx` |
| Japanese | `Translations.ja-JP.resx` |

<h3 id="adding-a-new-language">新しい言語の追加</h3>

1. `Translations.{locale}.resx`（例：`Translations.ja-JP.resx`）を作成し、`Translations.resx` と同じキーをすべて含める
2. `UI/ViewModels/Pages/SettingsViewModel.cs` で言語を登録する：

```csharp
new() { DisplayName = "日本語", Culture = new CultureInfo("ja-JP") },
```

<h3 id="hardcoded-string-rule">ハードコード文字列のルール</h3>

**文字列をハードコードしない**。常に以下を使用する：

```csharp
// Strongly typed (recommended)
string title = Translations.Features_Desktop_Name;

// With format args
string msg = string.Format(Translations.Dashboard_SystemInfo_Storage_DiskInfo, used, total, percent);

// Dynamic key lookup (for convention-based keys)
string title = Loc.Instance[$"Optimizer.{category}.{key}.Name"];
```

XAML では：

```xml
<!-- Without args -->
<ui:TextBlock Text="{ext:Loc Dashboard.Header.Title}" />

<!-- With bound args -->
<ui:TextBlock Text="{ext:Loc Dashboard.UpdateInfoBar.Message, {Binding ViewModel.LatestVersion}}" />
```

---

<h1 id="pull-request-process">プルリクエストの手順</h1>

1. **`master` からブランチを作成する** — master で直接作業しない：

   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/issue-number
   ```

2. **Conventional Commits でコミットする**：

   | プレフィックス | 使用タイミング |
   |---|---|
   | `feat:` | 新しい最適化や機能 |
   | `fix:` | バグ修正 |
   | `refactor:` | 動作変更のないコード再構成 |
   | `docs:` | ドキュメントの更新 |
   | `test:` | テストの追加や修正 |
   | `i18n:` | 翻訳の更新 |
   | `chore:` | メンテナンス、ビルド設定、依存関係 |

3. **プッシュ前に確認する**：

   ```bash
   # 1. Build
   dotnet build optimizerDuck.slnx --configuration Release

   # 2. Test
   dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build

   # 3. Format
   dotnet csharpier .

   # 4. Check git status — make sure only intended files are staged
   git status
   git diff --cached
   ```

4. **PR を作成する**：
   - **何が**変更され、**なぜ**変更されたかを説明する
   - UI の変更がある場合は、**スクリーンショットを含める**
   - 関連 Issue をリンクする：`Closes #42`
   - 作業中の場合はドラフトとしてマークする

5. **レビュー**：メンテナーがレビューします。フィードバックを受け入れ、迅速に対応してください。

<h3 id="pr-checklist">PR チェックリスト</h3>

- [ ] コードが既存のパターンに従っている（検出、属性、非同期命名）
- [ ] 最低限 `Translations.resx` にローカライズキーが追加されている
- [ ] `dotnet build` が成功する（エラー 0 件）
- [ ] `dotnet test` が通る（すべてのテストが成功）
- [ ] `dotnet csharpier .` が実行されている
- [ ] ハードコードされた文字列がない
- [ ] リバートステップが適切に記録されている（該当する場合）
- [ ] UI の変更にスクリーンショットが含まれている

---

<h1 id="issue-guidelines">Issue ガイドライン</h1>

- **バグ報告**：バグ報告テンプレートを使用する。再現手順、期待される動作と実際の動作、`%localappdata%\optimizerDuck\optimizerDuck.log` のログ + システム仕様を含める。
- **機能リクエスト**：ユースケース、解決する問題、動作の仕方を説明する。
- **最適化の提案**：レジストリパス、サービス名、CLI コマンドを含める。ドキュメントや信頼できる情報源へのリンクを添える。
- **質問**：GitHub Discussions を使用するか、[Discord サーバー](https://discord.gg/tDUBDCYw9Q) に参加する。

---

<h1 id="faq--troubleshooting">FAQ とトラブルシューティング</h1>

<h3 id="build-fails-ca1416">ビルドが「CA1416」エラーで失敗する</h3>

`.editorconfig` が CA1416 を抑制します。まだ表示される場合は、master から最新の `.editorconfig` を取得していることを確認してください。本プロジェクトは Windows 専用です — `SupportedOSPlatform` ガードは追加しないでください。

<h3 id="optimization-not-showing">最適化が UI に表示されない</h3>

チェックリスト：
- カテゴリクラス内の**ネストされた public クラス**になっているか？
- カテゴリクラスは `IOptimizationCategory` を実装しているか？
- 最適化クラスは `BaseOptimization` を継承しているか？
- `[Optimization(Id = "...", ...)]` 属性があるか？
- ローカライズキーが `Translations.resx` に追加されているか？

<h3 id="customize-setting-not-showing">カスタマイズ設定が表示されない</h3>

上記と同様のチェックを `ICustomizeCategory` / `BaseCustomizeSetting` について行う。
- `[CustomizeSetting(Section = ..., Icon = ...)]` があるか？
- `Section` 列挙型の値のスペルは正しいか？

<h3 id="no-revert-data-after-testing">テスト後にリバートデータファイルがない</h3>

リバートデータを確認するテストは `%localappdata%\optimizerDuck\Revert\` 内のファイルを期待します。テストのクリーンアップは `finally` ブロックで実行されます — アサーションがクリーンアップの前に実行されることを確認してください。

<h3 id="ui-freezes">最適化の適用時に UI がフリーズする</h3>

非同期のプロバイダー呼び出し（`ChangeServiceStartupTypeAsync`、`CMDAsync`、`PowerShellAsync`）には `ApplyAsync` で `async`/`await` を使用していることを確認してください。`Task.FromResult` を使用したり、`.Result` / `.Wait()` でブロックしたりすると、UI スレッドがフリーズします。

<h3 id="generate-guid">GUID の生成方法</h3>

```powershell
# PowerShell
[guid]::NewGuid()
```

```bash
# Command line (if uuidgen is available)
uuidgen
```

<h3 id="translations-showing-key-names">UI に翻訳ではなくキー名が表示される</h3>

`Translations.resx` へのローカライズキーの追加を忘れています。期待されるキーパターンについては [ローカライズ](#localization) セクションを確認してください。

<h3 id="no-revert-data-error">リバート時に「No revert data」エラーが出る</h3>

最適化の `Id` GUID が変更されていないことを確認してください。リバートファイルは `Id` でキー付けされます。GUID を再生成すると、以前適用した最適化に一致するリバートファイルがなくなります。

---

<div align="center">

<h2 id="credits">クレジット</h2>

マージされた PR の貢献者はリリースノートに記載されます。モジュールに大きく貢献した場合は、ファイルヘッダーの上部に著者タグを追加できます。

---

<h2 id="license">ライセンス</h2>

optimizerDuck に貢献することにより、あなたの貢献はプロジェクトの [GPL v3 ライセンス](../LICENSE) の下でライセンスされることに同意したものとみなされます。

---

<p><i>optimizerDuck をより良くしてくれてありがとう。</i></p>

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
