<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="../assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

[English](../../CONTRIBUTING.md) | **日本語** | [Türkçe](CONTRIBUTING.tr-TR.md)

[はじめに](#introduction) • [セットアップ](#getting-started) • [アーキテクチャ概要](#architecture-overview) • [貢献の方法](#ways-to-contribute) • [最適化の作成](#creating-an-optimization) • [カスタマイズ設定の作成](#creating-a-customize-setting) • [条件システム](#the-condition-system) • [リフレッシュスコープシステム](#the-refresh-scope-system) • [新機能の構築](#building-new-features) • [リバートシステム](#revert-system) • [テスト](#testing) • [コーディング規約](#coding-standards) • [ローカライズ](#localization) • [プルリクエストの手順](#pull-request-process) • [Issue ガイドライン](#issue-guidelines) • [FAQ とトラブルシューティング](#faq--troubleshooting) • [ライセンス](#license)

</div>

---

<h1 id="introduction">はじめに</h1>

**.NET 10 上の WPF で構築された、無料のオープンソース Windows 最適化ツール**である **optimizerDuck** への貢献ありがとうございます。

以下のような形でお手伝いいただけます：

- 再現手順を明確にしたバグ報告
- 新しい最適化や機能の提案（まず Issue を作成してください）
- ドキュメントやガイドの改善
- 翻訳の追加や修正
- コードの貢献：最適化、カスタマイズ設定、サービス、UI の改善
- テストの追加や既存テストのレビュー

> **初めての方へ**：まず [セットアップ](#getting-started)、次に [アーキテクチャ概要](#architecture-overview) を読んでください。最も一般的なコード貢献は [最適化の作成](#creating-an-optimization) と [カスタマイズ設定の作成](#creating-a-customize-setting) です。

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
# 10.x と表示されるはずです
```

<h3 id="fork-and-clone">2. フォークとクローン</h3>

```bash
# まず GitHub でリポジトリをフォークし、自分のフォークをクローンします
git clone https://github.com/<your-username>/optimizerDuck.git
cd optimizerDuck

# メインリポジトリと同期するために upstream を追加します
git remote add upstream https://github.com/itsfatduck/optimizerDuck.git

# 作業用のブランチを作成します（master で直接作業しないこと）
git checkout -b feature/your-feature-name
```

<h3 id="restore-build-test">3. 復元、ビルド、テスト</h3>

ソリューションは `.slnx` 形式（XML ベースのソリューションファイル、`.sln` ではありません）を使用します。

```bash
# 依存関係を復元
dotnet restore optimizerDuck.slnx

# ビルド（CI は Release、Debug でも動作します）
dotnet build optimizerDuck.slnx --configuration Release --no-restore

# テストを実行
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build

# アプリを実行（システム設定を変更するため管理者権限のプロンプトが必要）
dotnet run --project optimizerDuck/optimizerDuck.csproj

# CSharpier でコードを整形
dotnet csharpier .
```

> 新しい NuGet 依存関係を追加した場合は、再度 `dotnet restore` を実行してください（以降のビルドでは `--no-restore` を使用します）。

<h3 id="publishing">4. 公開（Publishing）</h3>

```bash
publish.bat portable              # ポータブルフォルダ（テスト用に推奨）
publish.bat single                # 単一ファイルの実行可能ファイル
publish.bat single --skip-tests   # 素早い反復のためにテストをスキップ
publish.bat portable --no-pause   # 最後に一時停止しない（CI 向け）
```

`publish.bat` はまずテストを実行し（`--skip-tests` を渡した場合はスキップ）、選択したプロファイル（`Portable` または `Single`）で `dotnet publish` を呼び出します。

<h3 id="quick-start-checklist">5. クイックスタートチェックリスト</h3>

初めて貢献する前に：

- [ ] リポジトリをフォークしてクローンする
- [ ] `dotnet build` が成功する（エラー 0 件）
- [ ] `dotnet test` が通る（すべてのテストが成功）
- [ ] `dotnet csharpier .` がエラーなく整形できる
- [ ] 下記の [アーキテクチャ概要](#architecture-overview) を読む

---

<h1 id="architecture-overview">アーキテクチャ概要</h1>

<h3 id="solution-structure">ソリューション構成</h3>

```
optimizerDuck.slnx                          # ソリューションファイル（.slnx 形式）
├── optimizerDuck/                          # メイン WPF アプリ（net10.0-windows）
│   ├── App.xaml.cs                         # DI 登録、スタートアップ、テーマ、ログ
│   ├── optimizerDuck.csproj                # TFM: net10.0-windows10.0.17763.0, UseWPF=true
│   ├── app.manifest                        # requireAdministrator UAC レベル
│   │
│   ├── Domain/                             # 純粋なモデル、インターフェイス、属性（WPF 依存なし）
│   │   ├── Abstractions/                   # IOptimization, ICustomizeSetting, IRevertStep, IWindow,
│   │   │                                   #   ICustomizeCategory, IOptimizationCategory
│   │   ├── Attributes/                     # [Optimization], [CustomizeSetting],
│   │   │                                   #   [OptimizationCategory], [CustomizeCategory]
│   │   ├── Conditions/                     # 互換性条件システム（「条件システム」を参照）
│   │   │   ├── BuiltIn/                    # 既製の条件（Windows バージョン、GPU/CPU ブランド、
│   │   │   │                               #   最小 RAM、レジストリキー/サービスの存在など）
│   │   │   ├── ICondition.cs               # 条件のコントラクト
│   │   │   ├── ConditionBase.cs            # 共通ヘルパー（OS ビルド番号の解析など）
│   │   │   ├── ConditionResult.cs          # Available / Unsupported / Error の結果
│   │   │   ├── ConditionState.cs           # 結果の列挙型
│   │   │   ├── ConditionValidation.cs      # 検出時のメタデータ検証
│   │   │   └── WindowsBuilds.cs            # OS ビルド番号の定数
│   │   ├── Configuration/                  # AppSettings モデル
│   │   ├── Exceptions/                     # StepExecutionException
│   │   ├── Execution/                      # ExecutionScope — AsyncLocal による環境的ステップ追跡
│   │   ├── Customize/                      # カスタマイズ設定
│   │   │   ├── Categories/                 # ネストされた設定クラスを持つカテゴリクラス
│   │   │   └── Models/                     # BaseCustomizeSetting, RegistryToggle, RegistryBinding,
│   │   │                                   #   CustomizeRefreshScope, SettingOption,
│   │   │                                   #   CustomizeControlType, RecommendationState など
│   │   ├── Optimizations/                  # 最適化
│   │   │   ├── Categories/                 # ネストされた最適化クラスを持つカテゴリクラス
│   │   │   └── Models/                     # BaseOptimization, ApplyResult, OptimizationContext,
│   │   │       ├── Bloatware/              # プリインストールアプリ用の AppXPackage モデル
│   │   │       ├── Cleanup/                # ディスククリーンアップ用の CleanupItem
│   │   │       ├── ScheduledTask/          # ScheduledTaskModel
│   │   │       ├── Services/               # RegistryItem, ServiceItem, ShellResult, ServiceStartupType
│   │   │       └── StartupManager/         # StartupApp, StartupTask モデル
│   │   ├── Revert/                         # RevertData, RevertResult, リバートステップ型
│   │   │   └── Steps/                      # RegistryRevertStep, ServiceRevertStep,
│   │   │                                   #   ScheduledTaskRevertStep, ShellRevertStep, UsbPowerRevertStep
│   │   └── UI/                             # Enums & helpers: OptimizationRisk, OptimizationTags,
│   │                                       #   OptimizationCategoryOrder, CustomizeOrder,
│   │                                       #   LanguageOption, SupportedLanguages (single source of truth, 17 locales),
│   │                                       #   OptimizationState, RiskVisual, ProcessingProgress, ...
│   │
│   ├── Common/                             # Shared helpers, extensions, converters
│   │   ├── Converters/                     # WPF value converters (BooleanToVisibility, MBToGB, ThemeToIndexConverter, ThemeToGitHubIconConverter, ...)
│   │   ├── Extensions/                     # StringExtensions, page-registry extensions,
│   │   │                                   #   LanguageExtensions
│   │   └── Helpers/                        # Shared.cs, ReflectionHelper.cs, SystemRefreshService.cs,
│   │                                       #   EmbeddedResourceHelper.cs, WmiHelper.cs,
│   │                                       #   GitHubSourceHelper.cs, ThemeResource.cs など
│   │
│   ├── Services/                           # ビジネスロジック層
│   │   ├── Conditions/                     # ConditionEvaluator（静的評価のエントリポイント）
│   │   ├── Configuration/                  # ConfigManager, LanguageManager
│   │   ├── Customize/                      # CustomizeRegistry（リフレクションベースの検出）
│   │   ├── Optimization/                   # OptimizationRegistry, OptimizationService
│   │   │   └── Providers/                  # 静的: RegistryService, ShellService (+ ShellPolicy),
│   │   │                                   #   ScheduledTaskService, ServiceProcessService
│   │   ├── Revert/                         # RevertManager（リバート JSON のアトミック読み書き）
│   │   ├── System/                         # RegistryWatcher (+ IRegistryWatcher), SystemInfoService,
│   │   │                                   #   StreamService, UpdaterService, CrossPageEventBus
│   │   └── UI/                             # BloatwareService, DiskCleanupService, StartupManagerService
│   │
│   ├── UI/                                 # WPF ページ、ViewModel、コントロール、スタイル
│   │   ├── Behaviors/                      # SmoothScrollBehavior
│   │   ├── Controls/                       # FilledNavigationViewItem, EmptyBadge
│   │   ├── Dialogs/                        # ProcessingDialog, OptimizationDetailsDialog,
│   │   │                                   #   OptimizationResultDialog, RestorePointDialog, LegalDialog (+ ViewModel),
│   │   │                                   #   BloatwareConfirmationDialog, ScheduledTask dialogs, ...
│   │   ├── Pages/                          # Dashboard, Optimize, Customize, Settings, Bloatware,
│   │   │   ├── Customize/                  # CustomizePage + Categories/ (auto-registered pages)
│   │   │   ├── Optimize/                   # OptimizePage + Categories/ (auto-registered pages)
│   │   │   ├── DiskCleanupPage
│   │   │   ├── StartupManagerPage
│   │   │   └── ScheduledTasksPage
│   │   ├── Styles/                         # FluentDesign.xaml, NavigationViewOverride.xaml, ToolTipOverride.xaml
│   │   ├── ViewModels/                     # Page, dialog and window ViewModels
│   │   │   ├── Dialogs/                    # LegalDialogViewModel (transient, runtime language/theme)
│   │   │   ├── Pages/                      # DashboardViewModel, SettingsViewModel, ...
│   │   └── Windows/                        # MainWindow
│   │
│   └── Resources/                          # 画像、埋め込みアセット、ローカライズ
│       ├── Embedded/                       # Icons/ と PowerPlans/（optimizerDuck.pow）
│       ├── Images/                         # Duck.png, GitHub ロゴ, Discord ロゴ
│       └── Languages/                      # Translations.resx（デフォルト）+ ロケール版
│
└── optimizerDuck.Test/                     # xUnit v3 テストプロジェクト（InternalsVisibleTo）
```

> **上のツリーを厳密な仕様として参照しないでください。** これは地図であり仕様ではありません — フォルダやファイルは進化します。迷ったら実際のフォルダを確認してください。本セクション末尾の「プロジェクト構造」も参照してください。

<h3 id="key-design-decisions">主要な設計判断</h3>

| 判断 | 理由 |
|---|---|
| **リフレクションによる自動検出** | DI 登録配列を更新する必要がありません。`ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` が `optimizerDuck.*` アセンブリをスキャンします。新しい最適化や設定は自動的に検出されます。 |
| **静的プロバイダーサービス** | `RegistryService`、`ShellService`、`ScheduledTaskService`、`ServiceProcessService` は静的クラスです。環境的な `ExecutionScope` にリバートステップを記録するため、コンテキストの注入や受け渡しは不要です。 |
| **ファイルベースのリバート追跡** | 適用状態 = ディスク上にファイルが存在する（`%localappdata%\optimizerDuck\Revert\{id}.json`）。データベースは使用しません。`File.Replace()` によるアトミック書き込み。 |
| **条件システム（フェイルオープン）** | 最適化や設定は互換性条件を宣言できます。評価の失敗がアイテムを隠すことはありません — [条件システム](#the-condition-system) を参照。 |
| **統合スタイルのテスト** | 実際のファイルシステム、実際のレジストリ（`HKCU\Software\TestOptimizerDuck*` 配下）、実際のプロセス実行。モックライブラリは使用せず、手書きのテストダブルのみ。 |
| **非同期サービスメソッド** | 外部プロセスを実行するプロバイダーメソッドは非同期（`*Async` サフィックス）。最適化の `ApplyAsync` では `async`/`await` で UI の応答性を保ちます。 |
| **静的な WMI ヘルパー** | `WmiHelper.Initialize()` が起動時に実行され、異常終了時の WMI クリーンアップハンドラを登録します。 |
| **保留中の変更トラッキング** | `App.HasPendingChanges` が未リバートの最適化を追跡。終了時に PC/Explorer 再起動または終了のオプションを表示します。 |

<h3 id="project-structure">プロジェクト構造</h3>

信頼できる構造は次の 3 か所にあり、移動やリネームの際は常に同期を保つ必要があります：

1. **ディスク上のフォルダ** — `optimizerDuck/`（アプリ）と `optimizerDuck.Test/`（テスト）。これ以外のトップレベルプロジェクトディレクトリはありません。
2. **`optimizerDuck.csproj`** — 埋め込みリソース、画像、パッケージ参照。
3. **`App.xaml.cs`** — DI 登録と起動シーケンス。

これら 2 つのプロジェクトフォルダ以外のトップレベルディレクトリを作成**しないでください**。

---

<h1 id="ways-to-contribute">貢献の方法</h1>

| 貢献の種類 | 説明 | 着手場所 |
|---|---|---|
| **新しい最適化** | レジストリの調整、サービスの変更、システムの調整 | `Domain/Optimizations/Categories/*.cs` |
| **新しいカスタマイズ設定** | Windows 設定の UI トグル（ゲームモード、マウス加速度、タスクバーなど） | `Domain/Customize/Categories/*.cs` |
| **新しい条件** | 最適化/設定の互換性ゲート（Windows バージョン、ハードウェアなど） | `Domain/Conditions/` |
| **新しいアプリ機能** | 新しいページ、ツール、機能 | まず Issue を作成 |
| **バグ修正** | クラッシュ修正、ロジックエラー、UI の問題 | 任意の場所 |
| **翻訳** | 新しい言語の追加や既存翻訳の修正 | `Resources/Languages/Translations.*.resx` |
| **ドキュメント** | README、CONTRIBUTING など | `*.md` ファイル |
| **テスト** | 新規・既存のテストの追加・レビュー | `optimizerDuck.Test/` |

---

<h1 id="creating-an-optimization">最適化の作成</h1>

<h3 id="how-discovery-works">検出の仕組み</h3>

起動時にアプリは `OptimizationRegistry.PreloadOptimizationsAsync()` を呼び出し、リフレクション処理をバックグラウンドスレッドで実行します：

1. `ReflectionHelper.FindImplementationsInLoadedAssemblies<IOptimizationCategory>()` がすべてのカテゴリクラスを検出します。
2. 各カテゴリについて、`IOptimization` を実装する**ネストされた public クラス**をスキャンします。
3. 各最適化をインスタンス化し、`OwnerType` を割り当て、`[Optimization]` メタデータ（`Condition` を含む）を検証します。
4. `OptimizationService.UpdateOptimizationStateAsync` がディスク上のリバートファイルをスキャンし、各最適化を適用済み/未適用としてマークします。
5. Optimize ページはバインド前に `EnsurePreloadedAsync()` を呼び出します（プリロード済みなら何もしません）。

**あなたの作業**：カテゴリ内にネストされたクラスを作成し、`BaseOptimization` を継承し、`[Optimization]` を付与する。以上です — 登録の更新は不要です。

<h3 id="optimization-categories">最適化カテゴリ</h3>

カテゴリは `Domain/Optimizations/Categories/` にファイル単位で配置されています。正確な一覧はそのフォルダを確認してください（セットは時間とともに変わります）。執筆時点のカテゴリは次のとおりです：

| ファイル | 対象 |
|---|---|
| `Performance.cs` | RAM 調整、プロセス優先度、キーボードレイテンシ、マルチメディアスケジューラ、アクセシビリティホットキー |
| `SecurityAndPrivacy.cs` | テレメトリ、エラー報告、広告 ID、位置情報、Copilot、アクティビティ履歴、配信最適化など |
| `Gpu.cs` | AMD/NVIDIA/Intel レジストリ調整、電源状態、クロックゲーティング、ASPM、非同期フリップ |
| `PowerManagement.cs` | 休止状態、高速スタートアップ、USB 選択的サスペンド、カスタム電源プランのインストール |
| `BloatwareAndServices.cs` | OEM プリインストールアプリのブロック、Windows サービス起動タイプの最適化 |
| `UserExperience.cs` | メニュー遅延、視覚効果、タスクバーアニメーション、透明度、スタートメニューの Web 検索 |
| `AI.cs` | Windows AI 機能（Recall、Click To Do） |

各カテゴリクラスは `[OptimizationCategory(typeof(SomePage))]` 属性で UI ページと紐づけられています。

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
        Id = "a1b2c3d4-...",                          // 新しい GUID を生成
        Risk = OptimizationRisk.Safe,                   // Safe / Moderate / Risky
        Tags = OptimizationTags.Performance,            // フラグ — | で組み合わせ
        Condition = typeof(Windows11Condition)          // 任意（「条件システム」を参照）
    )]
    public class MyNewTweak : BaseOptimization
    {
        public override async Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context)
        {
            // 1. 静的プロバイダーでシステムを変更
            RegistryService.Write(new RegistryItem(
                @"HKLM\SOFTWARE\Something", "ValueName", 1));

            // 2. 非同期操作を await — UI スレッドを解放
            await ServiceProcessService.ChangeServiceStartupTypeAsync(
                new ServiceItem("SomeService", ServiceStartupType.Disabled));

            // 3. 環境的な ExecutionScope から結果を返す
            return CompleteFromScope();
        }
    }
}
```

<h3 id="key-rules">重要なルール</h3>

| ルール | 詳細 |
|---|---|
| **`Id` は新しい GUID であること** | リバートファイルの命名と適用状態の追跡に使用。PowerShell で `[guid]::NewGuid()` で生成。 |
| **`BaseOptimization` を継承する** | 属性とローカライズキーから `Name`、`ShortDescription`、`RiskVisual`、`TagDisplays` を提供。 |
| **`OwnerType` は自動で割り当てられる** | 検出処理が設定します — 自分で設定しないこと。 |
| **`async Task<ApplyResult>` を使用する** | サービスプロバイダーは非同期 — `await` して UI の応答性を保つ。 |
| **`CompleteFromScope()` を返す** | 環境的な `ExecutionScope` に記録されたステップから `ApplyResult` を導出。手動で `ApplyResult` を構築しないこと。 |
| **進捗を報告する** | `progress.Report(new ProcessingProgress { ... })` で UI ダイアログを更新。 |
| **すべての例外をキャッチしない** | 例外は上位に伝播。`ExecutionScope` が成功/失敗を追跡し、`OptimizationService` が処理。 |
| **リバートステップを手動で作成しない** | 静的プロバイダーサービスが `ExecutionScope.RecordStep()` 経由で自動的に行う。 |
| **`context.Logger` を使用する** | 重要な診断情報の記録に使用。 |
| **`context.Snapshot` を使用する** | `OptimizationContext.Snapshot`（`SystemSnapshot`）が RAM、GPU、CPU、OS 情報を提供。条件分岐に使用。 |
| **`context.StreamService` を使用する** | リモートリソース（電源プランなど）をダウンロードする最適化向け。 |
| **必要なら `Condition` を宣言する** | Windows バージョンやハードウェアでゲート — [条件システム](#the-condition-system) を参照。 |

<h3 id="available-service-providers">利用可能なサービスプロバイダー</h3>

これらの**静的**クラスは、ログ記録、エラー処理、リバートステップの自動記録を担当します。

| サービス | 主要メソッド | 使用理由 |
|---|---|---|
| **`RegistryService`** | `Write()`、`Read<T>()`、`DeleteValue()`、`CreateSubKey()`、`DeleteSubKeyTree()`、`KeyExists()`、`CleanupEmptyKeys()` | レジストリキーの読み書き/削除。リバート用に元の値をバックアップ。params 配列でバッチ書き込み可能。 |
| **`ShellService`** | `CMDAsync()`、`PowerShellAsync()`、`CMD()`（同期）、`PowerShell()`（同期） | CMD / PowerShell コマンドの実行。非同期版を推奨。元に戻すコマンドを `revertCommand` で指定可能。非標準終了コードは `ShellPolicy` を参照。 |
| **`ScheduledTaskService`** | `DisableTask()`、`EnableTask()`、`IsTaskEnabled()`、`DeleteTask()`、`GetAllTasks()`、`RegisterTask()`、`RunTask()`、`StopTask()` | Windows スケジュールタスクの管理。 |
| **`ServiceProcessService`** | `ChangeServiceStartupTypeAsync()`、`GetStartupTypeAsync()` | Windows サービスの管理。常に非同期版を使用。params 配列でバッチ変更可能。 |

> **params 配列で複数アイテムを受け付けるメソッド**：ほとんどの書き込み/変更メソッドは params 配列を受け付けます（例：`RegistryService.Write(item1, item2, item3)`）。個別呼び出しより効率的です。

使用例：

```csharp
// 同期レジストリ書き込み — 複数アイテムを一度に
RegistryService.Write(
    new RegistryItem(@"HKLM\...", "Value1", 1),
    new RegistryItem(@"HKLM\...", "Value2", 0)
);
RegistryService.DeleteValue(new RegistryItem(@"HKCU\...", "OldValue"));

// 非同期サービス変更 — 複数サービスを一度に
await ServiceProcessService.ChangeServiceStartupTypeAsync(
    new ServiceItem("DiagTrack", ServiceStartupType.Disabled),
    new ServiceItem("dmwappushservice", ServiceStartupType.Disabled)
);

// リバートコマンド付きの非同期シェルコマンド
var result = await ShellService.CMDAsync(
    "powercfg /h off",
    "powercfg /h on"     // 元に戻すコマンドを保存
);

// 非同期 PowerShell
var usbStates = await ShellService.PowerShellAsync(
    "Get-CimInstance -Namespace root\\wmi -ClassName MSPower_DeviceEnable"
);
```

<h3 id="handling-async">非同期操作の扱い</h3>

すべての最適化が `async`/`await` を必要とするわけではありません。同期レジストリ書き込みのみ（非同期呼び出しなし）の場合は `Task.FromResult()` を返せます：

```csharp
public override Task<ApplyResult> ApplyAsync(
    IProgress<ProcessingProgress> progress,
    OptimizationContext context)
{
    RegistryService.Write(new RegistryItem(@"HKLM\...", "Value", 1));
    context.Logger.LogInformation("Applied tweak");
    return Task.FromResult(CompleteFromScope());
}
```

ただし、非同期プロバイダー（サービス、シェル、タスク）を使う場合は常に `await` します：

```csharp
public override async Task<ApplyResult> ApplyAsync(...)
{
    await ServiceProcessService.ChangeServiceStartupTypeAsync(...);
    return CompleteFromScope();
}
```

<h3 id="new-category">新しいカテゴリの作成</h3>

既存カテゴリに当てはまらない場合のみ。過度に細かいカテゴリは避けてください。

1. `Domain/Optimizations/Categories/YourCategory.cs` を作成します。
2. `IOptimizationCategory` を実装します。
3. `[OptimizationCategory(typeof(YourPage))]` を適用します — XAML ページも必要です（[新機能の構築](#building-new-features) を参照）。
4. `Domain/UI/OptimizationCategoryOrder.cs` の `OptimizationCategoryOrder` 列挙型にメンバーを追加し、並び順を整えます。
5. XAML ページは `App.xaml.cs` の `services.AddAllOptimizationPages()` で自動登録されます。

<h3 id="helper-base-class">最適化ヘルパーベースクラスの作成</h3>

複数の最適化が同じ構造を共有する場合（検出された GPU を反復する GPU ツイークなど）、抽象中間クラスを作成します：

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

AMD、NVIDIA、Intel のサブクラスを使った実例は `Domain/Optimizations/Categories/Gpu.cs` を参照してください。

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
> **翻訳は必須です**。キーを追加し忘れると、アプリは `"Optimizer.Performance.MyNewTweak.Name"` のような生のキー文字列を表示します。最低限 `Translations.resx`（英語）にエントリを追加してください。

---

<h1 id="creating-a-customize-setting">カスタマイズ設定の作成</h1>

カスタマイズ設定は、Windows 設定を ON/OFF に切り替える UI コントロール（トグルスイッチ、ドロップダウン、数値入力）です。`Domain/Customize/Categories/` に配置されます。

<h3 id="customize-categories">カスタマイズカテゴリ</h3>

カテゴリは `Domain/Customize/Categories/` にファイル単位で配置されています — 正確な一覧はそのフォルダを確認してください。執筆時点：

| ファイル | 対象 |
|---|---|
| `Desktop.cs` | デスクトップアイコン（PC、ごみ箱、ネットワーク、ユーザーファイル、コントロールパネル）、全体の表示/非表示、ショートカット矢印の表示 |
| `Preferences.cs` | タスクバー配置、ウィジェット、タスクビュー、タスク終了、ダークモード、ファイル拡張子、隠しファイル、クリップボード履歴、検索モード、秒表示、Bing 検索、クラシックコンテキストメニュー |
| `Gaming.cs` | ゲームモード、ゲームバー、バックグラウンド録画、マウス加速度、フルスクリーン最適化、GPU スケジューリング |
| `SystemFeatures.cs` | 起動時の Num Lock、開発者モード、長いパス、バッテリー残量表示 |

各カテゴリクラスは `[CustomizeCategory(PageType = typeof(SomePage))]` 属性で UI ページと紐づけられています。

<h3 id="step-by-step-simple-registry-toggle">ステップバイステップ：シンプルなレジストリトグル</h3>

シンプルな ON/OFF レジストリトグルの場合、基底クラスがすべての処理を行います：

```csharp
private enum Sections { Taskbar, Widgets, Advanced }

[CustomizeSetting(
    Section = nameof(Sections.Taskbar),        // UI で設定をグループ化
    Icon = SymbolRegular.AlignCenter24,         // Wpf.Ui.Controls.SymbolRegular から
    Recommendation = RecommendationState.On,    // On / Off / Depends / Experimental / None
    Condition = typeof(Windows11Condition)      // 任意の互換性条件
)]
public class TaskbarAlignment : BaseCustomizeSetting
{
    protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "TaskbarAl",
                OnValues = [0],       // ON のときの値（複数可）
                OffValues = [1],      // OFF のときの値（複数可）
                DefaultValue = 1,     // デフォルト状態の値（キー欠落時に使用）
            },
        ];

    // 変更後にリフレッシュが必要な対象を宣言
    protected override CustomizeRefreshScope RefreshScope =>
        CustomizeRefreshScope.TaskbarSettings;
}
```

<h3 id="registrytoggle-properties">RegistryToggle プロパティ</h3>

| プロパティ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `Path` | `string` | 必須 | レジストリキーの完全パス（例：`@"HKCU\Software\..."`） |
| `Name` | `string` | 必須 | レジストリ値の名前 |
| `OnValues` | `IReadOnlyList<object?>` | `[1]` | 「オン」状態を表す値のリスト。`null` は「キー不在 = オン」。 |
| `OffValues` | `IReadOnlyList<object?>` | `[0]` | 「オフ」状態を表す値のリスト。`null` は「キー不在 = オフ」。 |
| `DefaultValue` | `object?` | `0` | キー欠落時のデフォルト状態値（デフォルトへのリセットに使用）。 |
| `IsOptional` | `bool` | `false` | `true` の場合、状態検出に不要。 |
| `ValueKind` | `RegistryValueKind` | `DWord` | レジストリ値の型（DWord、String など）。 |

**状態検出ロジック**：`GetState()`（`BaseCustomizeSetting` 内）は非オプションの `RegistryToggles` をすべて集め、**すべて**の必須トグルが `OnValues` のいずれかに一致する場合のみ `true` を返します。

<h3 id="control-types">コントロールタイプ</h3>

| 型 | 表示形式 | 用途 |
|---|---|---|
| `Toggle` | ON/OFF スイッチ | ほとんどの設定（デフォルト） |
| `Dropdown` | コンボボックス | 複数選択（例：電源プラン、検索ボックスモード、タスクバー配置） |
| `Option` | ラジオボタングループ | 排他的な視覚オプション（例：左/中央揃え） |
| `NumberInt` | 整数テキスト入力 | 数値（例：秒数） |
| `NumberFloat` | 小数テキスト入力 | 精度の高い値 |
| `String` | テキスト入力 | 自由形式のテキスト |

UI コントロールを変更するには `ControlType` をオーバーライドします：

```csharp
public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;
```

<h3 id="dropdown-with-options">オプション付きドロップダウン</h3>

ドロップダウンのオプションは `RegistryBinding` を宣言するため、基底クラスが現在値の自動読み取りと選択時の自動書き込みを行えます。`Option()` ヘルパーを使用します：

```csharp
[CustomizeSetting(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.AlignCenter24)]
public class TaskbarAlignment : BaseCustomizeSetting
{
    private const string RegPath =
        @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string RegName = "TaskbarAl";

    public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;

    // Option(key, regPath, regName, value) はラベルを
    //   Customize.{Category}.{Feature}.Options.{key} から読み取る
    protected override IReadOnlyList<SettingOption>? GetOptions() =>
        [Option("Center", RegPath, RegName, 1), Option("Left", RegPath, RegName, 0)];

    protected override CustomizeRefreshScope RefreshScope =>
        CustomizeRefreshScope.TaskbarSettings;
}
```

**複数**のレジストリ値に触れるオプションでは、明示的なバインディングを渡します：

```csharp
protected override IReadOnlyList<SettingOption>? GetOptions() =>
[
    Option("On", 1,
        new RegistryBinding(@"HKCU\...\Key1", "ValueA", 1),
        new RegistryBinding(@"HKCU\...\Key2", "ValueB", "enabled", RegistryValueKind.String)),
    Option("Off", 0,
        new RegistryBinding(@"HKCU\...\Key1", "ValueA", 0),
        new RegistryBinding(@"HKCU\...\Key2", "ValueB", "disabled", RegistryValueKind.String)),
];
```

基底クラスはバインディングから現在値を読み取り、ライブ値がどの宣言オプションにも一致しない場合はメモリ上だけの **「カスタム」**（または **「未設定」**）フォールバックを表示し、選択されたオプションのすべてのバインディングを書き込みます。一般的なケースではカスタムの `ApplyAsync` は不要です。

<h3 id="dynamic-options">動的オプション（プラットフォーム対応）</h3>

Windows バージョンに応じてオプションを条件付きで表示できます：

```csharp
protected override IReadOnlyList<SettingOption>? GetOptions()
{
    if (Shared.IsWindows11OrGreater)
        return
        [
            Option("Hidden", RegPath, RegName, 0),
            Option("Icon", RegPath, RegName, 1),
            Option("IconAndLabel", RegPath, RegName, 2),
            Option("SearchBox", RegPath, RegName, 3),
        ];
    return
    [
        Option("Hidden", RegPath, RegName, 0),
        Option("Icon", RegPath, RegName, 1),
        Option("SearchBox", RegPath, RegName, 2),
    ];
}
```

<h3 id="custom-logic-override">カスタムロジック（GetStateAsync / ApplyAsync のオーバーライド）</h3>

シンプルなレジストリトグルでない設定（マウス加速度が 3 つのレジストリ値を組み合わせるなど）の場合：

```csharp
[CustomizeSetting(
    Section = nameof(Sections.Input),
    Icon = SymbolRegular.Cursor24,
    Recommendation = RecommendationState.Off
)]
public class MouseAcceleration : BaseCustomizeSetting
{
    private const string Path = @"HKCU\Control Panel\Mouse";

    // 監視パスにより外部変更時に UI が自動リフレッシュされる
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

        if (NeedsPostAction)
            await ExecutePostActionAsync();
    }

    protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Default;
}
```

> `ApplyAsync` をオーバーライドする場合、**必ず** `await ExecutePostActionAsync()` を自分で呼び出してください（`NeedsPostAction` でガード）。基底クラスが自動的に行うのは、デフォルトの `RegistryToggles` ベースとドロップダウンバインディングベースの適用経路のみです。

<h3 id="state-detection-retry">リトライ付きの状態検出</h3>

値の適用後、UI は `GetStateWithRetryAsync()`（`GetStateAsync()` ではありません）を呼び出します。このメソッドは：

1. 状態を `maxRetries`（デフォルト 3）回、`delayMs`（デフォルト 80ms）間隔で読み取ります。
2. 連続する 2 回の読み取りが同じ値で一致したら（収束チェック）返します。
3. リトライを使い切ったら最後の読み取り値にフォールバックします。

これにより、書き込み後にレジストリが落ち着くまでの間、UI が古い値を表示するのを防ぎます。

<h3 id="non-registry-deps">非レジストリ依存のカスタムロジック</h3>

埋め込みリソースの抽出を伴う設定（ショートカット矢印を空白アイコンに置き換えるなど）の場合：

```csharp
public override async Task ApplyAsync(object? value)
{
    var isOn = value is bool b && b;
    if (isOn)
    {
        RegistryService.DeleteValue(new RegistryItem(Path, "29"));
    }
    else
    {
        var outputPath = Path.Combine(Shared.AssetsDirectory, nameof(Desktop), "blank.ico");
        EmbeddedResourceHelper.TryExtract("Icons.blank.ico", outputPath);
        RegistryService.Write(new RegistryItem(Path, "29", outputPath));
    }
    await ExecutePostActionAsync();
}
```

埋め込みリソースをアセンブリからディスクに抽出するには `EmbeddedResourceHelper.TryExtract(resourceName, outputPath)` を使用します。

<h3 id="recommendation-system">レコメンデーションシステム</h3>

各カスタマイズ設定はレコメンデーションを宣言できます：

```csharp
[CustomizeSetting(..., Recommendation = RecommendationState.On)]
// 利用可能: On, Off, Depends, Experimental, None
```

- **`On`**: オン推奨 — システム改善
- **`Off`**: オフ推奨 — システム改善
- **`Depends`**: ユーザーのニーズ/構成に依存
- **`Experimental`**: 不安定な可能性、注意して使用
- **`None`**（デフォルト）: レコメンデーション非表示

理由はローカライズキーで追加できます：`Customize.{Category}.{Feature}.Recommendation.Reason`。

<h3 id="what-to-override-per-pattern">パターンごとのオーバーライド</h3>

| シナリオ | オーバーライド |
|---|---|
| シンプルなレジストリトグル | `RegistryToggles` + `RefreshScope` |
| 複数のレジストリトグル（例：ゲームモード 2 値） | `RegistryToggles`（すべてリスト） |
| ドロップダウン / オプション | `ControlType` → `Dropdown` + `GetOptions()` で `Option(...)` バインディング |
| 複数値ロジック（例：マウス加速度 3 値） | `GetStateAsync()` + `ApplyAsync()` + `GetWatchedRegistryPaths()` |
| レジストリ操作のない設定 | `GetStateAsync()` + `ApplyAsync()`（完全カスタム） |
| カスタムリフレッシュ動作 | `RefreshScope`（フラグのみ）または `ExecutePostActionAsync()`（完全オーバーライド） |
| 収束チェック付き状態検出 | `GetStateWithRetryAsync()`（組み込み — オーバーライド不要） |
| Windows バージョンごとの動的オプション | `GetOptions()` を条件付きでオーバーライド |
| 埋め込みリソース抽出 | カスタム `ApplyAsync` で `EmbeddedResourceHelper.TryExtract()` |
| 互換性ゲート | `[CustomizeSetting]` 属性の `Condition = typeof(...)` |

<h3 id="create-a-new-customize-category">新しいカテゴリの作成</h3>

1. `Domain/Customize/Categories/YourCategory.cs` を作成します。
2. `[CustomizeCategory(PageType = typeof(YourPage))]` 付きで `ICustomizeCategory` を実装します。
3. `Domain/UI/CustomizeOrder.cs` の `CustomizeOrder` 列挙型にメンバーを追加します。
4. XAML ページを作成します（`UI/Pages/Customize/Categories/` に新しいクラス）。
5. ページは `App.xaml.cs` の `services.AddAllCustomizeCategoryPages()` で自動登録されます。

<h3 id="localization-keys-customize">カスタマイズ設定のローカライズキー</h3>

```
Customize.{CategoryName}.{SettingKey}.Name
Customize.{CategoryName}.{SettingKey}.Description
Customize.{CategoryName}.{SettingKey}.Options.{OptionKey}    (SettingOption 使用時)
Customize.{CategoryName}.{SettingKey}.Recommendation.Reason   (Recommendation != None のとき)
Customize.{CategoryName}.Section.{SectionName}                (セクションヘッダー用)
```

---

<h1 id="the-condition-system">条件システム</h1>

条件を使うと、最適化やカスタマイズ設定を互換性チェックでゲートし、「お使いのシステムではサポートされていません」と UI に表示できます。動作しないものを適用する代わりになります。

<h3 id="core-concepts">中核概念</h3>

条件は `Domain/Conditions/` にあり、`Services/Conditions/` の静的 `ConditionEvaluator` が評価します。

| 要素 | 目的 |
|---|---|
| `ICondition` | コントラクト: `ConditionResult Evaluate(SystemSnapshot snapshot)`。実装には public のパラメータなしコンストラクタが必要（リフレクションでインスタンス化されるため）。 |
| `ConditionBase` | 共通ヘルパーを持つ任意の基底クラス（例：OS ビルド番号解析の `TryGetOsBuild`）。 |
| `ConditionResult` | 結果: `Available`、`Unsupported(title, description)`、または `Error()`。ローカライズテキストはプロバイダー経由で遅延解決。 |
| `ConditionState` | `Available`、`Unsupported`、`Error`。 |
| `ConditionValidation` | 検出時に `Condition = typeof(...)` メタデータを検証し、設定ミスを起動時に早期検出。 |
| `WindowsBuilds` | OS ビルド番号の定数（例：`Windows11`、`Windows11_24H2`）。 |

**フェイルオープンの原則**：アイテムをブロックするのは `Unsupported` のみで、それも未適用（またはユーザーが非表示にしていない）の場合だけです。`Error` や未取得のスナップショットがアイテムを隠すことはありません — 不完全なハードウェア検出が利用可能な選択肢を奪ってはなりません。

<h3 id="declaring-a-condition">条件の宣言</h3>

両方の属性が任意の `Condition` を受け付けます：

```csharp
// 最適化
[Optimization(Id = "...", Risk = OptimizationRisk.Safe,
    Tags = OptimizationTags.Privacy | OptimizationTags.System,
    Condition = typeof(Windows11_24H2OrGreaterCondition))]
public class DisableRecall : BaseOptimization { ... }

// カスタマイズ設定
[CustomizeSetting(Section = ..., Icon = SymbolRegular.Grid24,
    Condition = typeof(Windows11Condition))]
public class TaskbarWidgets : BaseCustomizeSetting { ... }
```

<h3 id="built-in-conditions">組み込み条件</h3>

既製の条件は `Domain/Conditions/BuiltIn/` にあります — 完全で最新の一覧はそのフォルダを確認してください。例：

| 条件 | 一致対象 |
|---|---|
| `Windows10Condition` / `Windows11Condition` | ビルド番号による OS バージョン |
| `Windows11_24H2OrGreaterCondition` | Windows 11 24H2（ビルド 26100）以降 |
| `CpuBrandCondition` / `GpuBrandCondition` | CPU/GPU ベンダー（Intel、AMD、NVIDIA） |
| `MinimumRamCondition`（基底）/ `SixteenGbRamCondition` | 最小搭載 RAM |
| `RegistryKeyExistsCondition` | レジストリキーの存在 |
| `ServiceExistsCondition` | Windows サービスの存在 |
| `RecallInstalledCondition` | Windows Recall の存在 |

<h3 id="writing-a-custom-condition">カスタム条件の作成</h3>

```csharp
public sealed class MyCondition : ConditionBase
{
    public override ConditionResult Evaluate(SystemSnapshot snapshot)
    {
        // ConditionBase.TryGetOsBuild は "22631.xxxx" を 22631 に解析
        if (TryGetOsBuild(snapshot, out var build) && build >= 22000)
            return ConditionResult.Available;

        return ConditionResult.Unsupported(
            () => Loc.Instance["Condition.MyCondition.Title"],
            () => Loc.Instance["Condition.MyCondition.Description"]);
    }
}
```

ガイドライン：

- システムが通過したら **`Available`** を、そうでなければ **`Unsupported(title, description)`** を返します。
- 予期しない失敗には `ConditionResult.Error()` を使う（または throw — `ConditionEvaluator` がキャッチして `Error` に変換）。エラーはブロックしません。
- ユーザー向けテキストはローカライズプロバイダー（`() => Loc.Instance[...]`）の後ろに置き、読み取り時に現在のカルチャが反映されるようにします。
- クラスに **public のパラメータなしコンストラクタ**を与えます（追加しないか、明示的な空コンストラクタを追加）。

<h3 id="how-conditions-evaluated">評価の仕組み</h3>

1. 検出処理が `ConditionValidation.Validate(...)` を呼び、宣言された型が `ICondition` を実装し構築可能であることを確認します。
2. UI は `ConditionEvaluator.Evaluate(conditionType, snapshot, logger)` を呼び出します。条件インスタンスをキャッシュし、決して例外を投げません。
3. `Unsupported` のアイテムはブロック/非対応状態で表示されます（ユーザーはセッション中だけ非表示にできます）。適用済みのアイテムが再ブロックされることはありません。

---

<h1 id="the-refresh-scope-system">リフレッシュスコープシステム</h1>

カスタマイズ設定が状態を変更すると、Windows の各サーフェスごとに異なるリフレッシュ戦略が必要です。`CustomizeRefreshScope` の `[Flags]` 列挙型がこれを細かく制御します。

<h3 id="available-flags">利用可能なフラグ</h3>

| メンバー | 値 | 効果 | P/Invoke |
|---|---|---|---|
| `None` | `0` | リフレッシュなし | — |
| `Settings` | `1 << 0` | `WM_SETTINGCHANGE` をブロードキャスト | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE)` |
| `Associations` | `1 << 1` | ファイル関連付け/アイコンキャッシュの変更をシェルに通知 | `SHChangeNotify(SHCNE_ASSOCCHANGED)` |
| `Desktop` | `1 << 2` | デスクトップアイコンリスト（`SysListView32`）の再描画を強制 | `LVM_REFRESH` + `LVM_UPDATE` |
| `Taskbar` | `1 << 3` | タスクバー向け `WM_SETTINGCHANGE`（"TraySettings"）をブロードキャスト | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, "TraySettings")` |
| `PolicyUpdate` | `1 << 4` | ユーザー単位パラメータのため `SPIF_SENDCHANGE` 付き `SystemParametersInfo` をプッシュ | `SystemParametersInfo` |
| `Theme` | `1 << 5` | `WM_THEMECHANGED` をブロードキャスト | `SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED)` |
| `DesktopIconCache` | `1 << 6` | HideIcons レジストリの切り替え + `WM_COMMAND 0x7402` をデスクトップへ送信 | レジストリ読み取り + `SendMessage(Progman, WM_COMMAND)` |

<h3 id="named-composites">名前付きコンポジット</h3>

| 名前 | 構成 | 使用例 |
|---|---|---|
| `Default` | `Settings \| Associations` | 一般的なエクスプローラーレベルの設定 |
| `DesktopIcons` | `Settings \| Desktop` | 個別デスクトップアイコンの表示/非表示 |
| `HideDesktopIcons` | `Settings \| DesktopIconCache` | 「すべてのデスクトップアイコンを非表示」トグル |
| `TaskbarSettings` | `Settings \| Taskbar` | タスクバー配置、ウィジェット、タスクビュー、タスク終了 |
| `ExplorerView` | `Settings \| Associations \| PolicyUpdate` | ファイル拡張子、隠しファイル、コンパクトビュー |

<h3 id="how-refresh-flows">リフレッシュの流れ</h3>

```
設定トグル → BaseCustomizeSetting.ApplyAsync(value)
  ├─ RegistryToggles を書き込み（あれば）、または選択オプションのバインディングを適用
  ├─ NeedsPostAction を確認（RefreshScope != None なら true）
  └─ Task.Run → ExecutePostActionAsync()
       ├─ 各 CustomizeRefreshScope フラグを確認
       ├─ SystemRefreshService のメソッド（P/Invoke）を呼び出し
       └─ Win32 通知を Windows へ送信
```

`ApplyAsync` をオーバーライドする場合、**必ず** `await ExecutePostActionAsync()` を自分で呼び出してください（上記の例を参照）。基底クラスが自動的に行うのは、デフォルトの `RegistryToggles` ベースとドロップダウンバインディングベースの適用のみです。

---

<h1 id="building-new-features">新機能の構築</h1>

新しいページやツール（例：「ネットワークモニター」）を追加する場合：

1. **まず GitHub Issue を作成する** — 機能、ユースケース、設計を説明し、メンテナのフィードバックを待ちます。
2. **実装順序**：

```csharp
// 1. Service layer in Services/UI or Services/System/YourService.cs
public class YourService(ILogger<YourService> logger) { ... }

// 2. ViewModel in UI/ViewModels/Pages/YourViewModel.cs
//    Extends ViewModel (which extends ObservableValidator + INavigationAware)

// 3. XAML Page in UI/Pages/YourPage.xaml (+ code-behind)

// 4. Register as singletons in App.xaml.cs
services.AddSingleton<YourViewModel>();
services.AddSingleton<YourPage>();

// 4b. Dialogs are transient (fresh instance per show, supports runtime language/theme)
services.AddTransient<YourDialogViewModel>();
services.AddTransient<YourDialog>();
```

- ViewModel とページは `App.xaml.cs` にシングルトンとして登録する必要があり、ダイアログはトランジェント（`AddTransient`）で登録する必要があります。シングルトンとトランジェントの使い分けに注意し、`App.AppHost` 経由で解決されることに留意してください。
- ナビゲーションは WPF UI（`INavigationService`）が処理します。ダイアログは `App.AppHost.Services.GetRequiredService<T>()` で解決されます（`MainWindow.xaml.cs` → `LegalDialog` を参照）。
- 既存のパターンに従ってください — `DashboardPage`、`OptimizePage`、`BloatwarePage`、`DiskCleanupPage`、`ScheduledTasksPage`、`StartupManagerPage`、および `LegalDialog`（ViewModel + トランジェント登録）などを参照。

<h3 id="di-registration-pattern">DI 登録パターン（App.xaml.cs より）</h3>
```csharp
// ページ + ViewModel — 機能ごとに 1 ペア（シングルトン）
services.AddSingleton<DashboardViewModel>();
services.AddSingleton<DashboardPage>();

services.AddSingleton<OptimizeViewModel>();
services.AddSingleton<OptimizePage>();

services.AddSingleton<SettingsViewModel>();
services.AddSingleton<SettingsPage>();

services.AddSingleton<BloatwareViewModel>();
services.AddSingleton<BloatwarePage>();

services.AddSingleton<DiskCleanupViewModel>();
services.AddSingleton<DiskCleanupPage>();

services.AddSingleton<StartupManagerViewModel>();
services.AddSingleton<StartupManagerPage>();

services.AddSingleton<ScheduledTasksViewModel>();
services.AddSingleton<ScheduledTasksPage>();

// Dialogs — transient (fresh instance per show)
services.AddTransient<LegalDialogViewModel>();
services.AddTransient<LegalDialog>();

// Customize
services.AddSingleton<CustomizeViewModel>();
services.AddSingleton<CustomizePage>();

// 自動ページ登録（リフレクションによるカテゴリページ）
services.AddAllCustomizeCategoryPages();   // [CustomizeCategory] 属性をスキャン
services.AddAllOptimizationPages();        // [OptimizationCategory] 属性をスキャン

// マネージャー
services.AddSingleton<ConfigManager>();
services.AddSingleton<RevertManager>();

// サービス
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

> これは把握のためのスナップショットです — 現在の登録は `App.xaml.cs` が正です。また、起動時の呼び出し `ShellService.Init(appOptionsMonitor)` と `WmiHelper.Initialize()`、およびトランジェントなダイアログの解決に使われる公開プロパティ `App.AppHost` にも注意してください。

<h3 id="system-services">システムサービスリファレンス</h3>

| サービス | 目的 |
|---|---|
| `SystemInfoService` | `OptimizationContext` と条件システムが使う `SystemSnapshot`（CPU、RAM、GPU、OS、ディスク）を提供。 |
| `StreamService` | リモートリソース（更新された電源プランなど）をダウンロード。`OptimizationContext.StreamService` 経由。 |
| `UpdaterService` | GitHub リリースの更新確認。Dashboard に更新プロンプトを表示。 |
| `RegistryWatcher` | 外部変更を監視して UI にリフレッシュ通知。`IRegistryWatcher` を実装。 |
| `BloatwareService` | プリインストール AppX パッケージを一覧表示し、Safe/Caution/Dangerous に分類。 |
| `DiskCleanupService` | クリーンアップ対象（一時ファイル、キャッシュ、ログ）をスキャン。 |
| `StartupManagerService` | スタートアップアプリとスケジュールタスクを一覧・管理。 |
| `ConditionEvaluator` | 互換性条件を評価する静的エントリポイント（[条件システム](#the-condition-system) を参照）。 |

---

<h1 id="revert-system">リバートシステム</h1>

適用された各最適化は `%localappdata%\optimizerDuck\Revert\{optimizationId}.json` に JSON ファイルを作成します。

<h3 id="how-it-works-jp">仕組み</h3>

```
ApplyAsync()
  │
  ├─ ExecutionScope.Begin(optimization, logger)    ← 環境的な AsyncLocal スコープを作成
  │
  ├─ RegistryService.Write(...)                     ← RegistryRevertStep を自動記録
  ├─ ServiceProcessService.ChangeServiceStartupTypeAsync(...)  ← ServiceRevertStep を自動記録
  ├─ ShellService.CMDAsync(...)                     ← ShellRevertStep を自動記録
  │
  ├─ CompleteFromScope() → ApplyResult              ← 記録されたステップから導出
  │
  └─ ExecutionScope 破棄 → RevertManager.SaveRevertDataAsync()
```

<h3 id="scope-variants-jp">スコープのバリエーション</h3>

| メソッド | 目的 |
|---|---|
| `ExecutionScope.Begin(optimization, logger)` | 実際の適用のための永続化可能なスコープを作成。 |
| `ExecutionScope.BeginForLogging(logger)` | ログのみ — ステップは記録するがリバートデータは永続化しない。 |
| `ExecutionScope.BeginForCapture(logger)` | リトライ用: `OptimizationId = Guid.Empty` でステップを捕捉し、後で実スコープに再割り当て。 |

<h3 id="step-types-jp">ステップタイプ</h3>

| ステップタイプ | 記録内容 | 自動作成元 |
|---|---|---|
| **`RegistryRevertStep`** | 変更前の元のレジストリ値 | `RegistryService.Write()`、`DeleteValue()`、`CreateSubKey()`、`DeleteSubKeyTree()` |
| **`ServiceRevertStep`** | 元のサービス起動タイプ | `ServiceProcessService.ChangeServiceStartupTypeAsync()` |
| **`ScheduledTaskRevertStep`** | 元のタスク状態（有効/無効） | `ScheduledTaskService.DisableTask()`、`EnableTask()` |
| **`ShellRevertStep`** | 元に戻すシェルコマンド | `ShellService.CMDAsync()`、`PowerShellAsync()` — `revertCommand` パラメータを渡す |
| **`UsbPowerRevertStep`** | USB 電源設定（デバイス別） | USB 関連の最適化（手動で `ExecutionScope.RecordStep()`） |

<h3 id="revert-command">シェル呼び出しへのリバートコマンド追加</h3>

`CMDAsync` または `PowerShellAsync` を呼ぶとき、元に戻すために保存される `revertCommand` パラメータを任意で渡せます：

```csharp
// "powercfg /h on" がこの変更を元に戻すために保存される
await ShellService.CMDAsync("powercfg /h off", "powercfg /h on");
```

<h3 id="revert-data-format-jp">リバートデータ形式</h3>

```json
{
  "SchemaVersion": 1,
  "OptimizationId": "guid",
  "OptimizationName": "DisableTelemetry",
  "AppliedAt": "2026-06-02T12:00:00Z",
  "Steps": [
    { "Index": 0, "Type": "Registry", "Data": { "..." } },
    null,                    // null ギャップ = このインデックスの失敗ステップ
    { "Index": 2, "Type": "Service", "Data": { "..." } }
  ]
}
```

<h3 id="key-details-jp">重要な詳細</h3>

- **適用状態**はディスク上のファイルの存在から推論されます（`RevertManager.IsAppliedAsync(id)`）。
- **アトミック書き込み**：`.tmp` に書き込んでから `File.Replace()` — クラッシュ安全。
- **同時アクセス**：ファイルごとの `SemaphoreSlim` ロックで競合を防止、30 秒タイムアウト。
- **`ExecutionScope`** は `AsyncLocal<ExecutionScope?>` で環境的ステップ追跡。パラメータでコンテキストを渡す必要なし。
- **リバートは逆順で実行**（最後に適用 = 最初にリバート）。
- **部分成功**：一部のステップが失敗しても続行。失敗ステップにはリトライアクションが記録。
- **リトライ**：`OptimizationService.RetryFailedStepsAsync()` が個別の失敗ステップをリトライ。`RecordStepAtIndex()` が元のインデックス配置を保持。
- **Upsert**：`RevertManager.UpsertRevertStepAtIndexAsync()` が特定インデックスのリバートステップを追加/置換（リトライ時に使用）。
- **ステップレジストリ**：リバートステップのデシリアライズはリフレクションベースの `_stepRegistry` — 新しいステップ型は `IRevertStep` を実装し静的な `FromData(JObject)` メソッドを持つだけで自動登録。

> **重要**: プロバイダーサービス（`RegistryService.Write`、`ShellService.CMDAsync` など）を呼ぶと、リバートステップは自動記録されます。カスタムプロバイダー（`UsbPowerRevertStep` など）を実装する場合を除き、手動でリバートステップを作成**しないでください**。

---

<h1 id="testing">テスト</h1>

テストは **xUnit v3** を使い、実 I/O による統合スタイルのアプローチを取ります。

<h3 id="test-patterns-jp">テストパターン</h3>

| パターン | 詳細 |
|---|---|
| **モックライブラリなし** | すべてのテストダブルはインターフェイスを実装する手書きクラス |
| **実際の I/O** | 実際のファイルシステム（リバート JSON ファイル）、実際のレジストリ（`HKCU\Software\TestOptimizerDuck*`）、実際のプロセス実行（CMD、PowerShell） |
| **クリーンアップ** | `try/finally` または `IDisposable` でテスト成果物を削除 |
| **命名** | `{Method}_{Scenario}_{ExpectedResult}` — 例: `ApplyAsync_Success_PersistsRevertDataFile` |
| **ログ記録** | DI ログパラメータには `NullLogger<T>.Instance` / `NullLoggerFactory.Instance` を使用 |
| **STA スレッド** | WPF コンポーネントを含むテストは `RunInStaThreadAsync` ヘルパー（STA スレッド + `TaskCompletionSource`）を使用 |

<h3 id="test-structure-jp">テスト構成</h3>

```
optimizerDuck.Test/
├── Common/Helpers/
├── Domain/
│   ├── Customize/
│   ├── Exceptions/
│   ├── Optimizations/
│   └── Revert/Steps/
└── Services/
    ├── Managers/
    ├── OptimizationServices/
    └── ApplyRevertComprehensiveTests.cs
```

アプリの構造を反映してください：`Services/OptimizationServices/` のテストは `Services/OptimizationServices/` に、ドメインモデルのテストは対応する `Domain/` サブディレクトリに配置します。

<h3 id="running-tests-jp">テストの実行</h3>

```bash
# ビルド後
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build

# ビルド + テストを一度に
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release

# 名前で単一テストを実行
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build --filter "FullyQualifiedName~TestName"
```

<h3 id="ci-integration-jp">CI 統合</h3>

CI パイプライン（`.github/workflows/ci.yml`）は以下を実行します：

```bash
dotnet restore optimizerDuck.slnx
dotnet build optimizerDuck.slnx --configuration Release --no-restore
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build --blame-hang --blame-hang-timeout 30s
```

`--blame-hang --blame-hang-timeout 30s` フラグにより、テストが 30 秒以上ハングしないことを保証します。これは実 Windows サービスとやり取りする統合スタイルのテストにとって重要です。

<h3 id="writing-tests-jp">プロバイダーサービスのテスト作成</h3>

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
                ExecutionScope.RecordStep("Test", "Step 1", true);
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

<h3 id="language-features-jp">言語機能</h3>

| 機能 | 使用 | 備考 |
|---|---|---|
| ファイルスコープ名前空間 | はい | `namespace X.Y;` |
| コレクション式 | はい | 空は `[]`、リストは `[item1, item2]` |
| プライマリコンストラクタ | はい | サービスやシンプルな型で使用 |
| 暗黙的 using | はい | `.csproj` で有効 |
| Null 許容参照型 | はい | `<Nullable>enable</Nullable>` — null を適切に処理 |
| 拡張メソッド（`extension(T type)`） | はい | C# 13 機能、`OptimizationTagsToDisplay` で使用 |

<h3 id="naming-conventions-jp">命名規則</h3>

| 要素 | 規則 | 例 |
|---|---|---|
| クラス、列挙型、インターフェイス、メソッド、プロパティ | `PascalCase` | `RegistryService`、`ApplyAsync` |
| プライベートフィールド | `_camelCase` | `_lastError` |
| ローカル変数、パラメータ | `camelCase` | `progress`、`serviceName` |
| 非同期メソッド | `*Async` サフィックス | `ChangeServiceStartupTypeAsync`、`CMDAsync` |
| 定数 | `PascalCase` / `_PascalCase` | `MaxRetries` / `_defaultTimeout` |

<h3 id="formatting-jp">フォーマット</h3>

| 設定 | 値 |
|---|---|
| インデント | 4 スペース（タブなし） |
| 行末 | LF |
| エンコーディング | UTF-8 |
| 最大行長 | 100 文字 |
| 末尾の空白 | 削除 |
| 末尾の改行 | 必須 |
| フォーマッター | **CSharpier** — コミット前に `dotnet csharpier .` |
| CA1416 | `.editorconfig` で抑制 — すべて Windows 専用 |

<h3 id="code-style-jp">コードスタイル</h3>

- **ハードコードされた文字列は禁止** — 常に `Translations.KeyName` または `Loc.Instance["Key"]`
- **コメントは最小限に** — 既存コードにはほとんどありません。不要なコメントを追加しないこと。
- **既存のライブラリを優先**（新しい依存関係より）
- **小さく焦点を絞った変更を優先**（大きなリファクタより）
- **マシン固有のパスやシークレットをコミットしない**

<h3 id="dependency-injection-jp">依存性注入</h3>

- サービス、ViewModel、ページは `App.xaml.cs` にシングルトンとして登録します。
- コンストラクタ注入を使用：`public class Foo(Bar bar, Baz baz)` または `public class Foo(ILogger<Foo> logger)`。
- 静的プロバイダーサービス（`RegistryService`、`ShellService`、`ScheduledTaskService`、`ServiceProcessService`）は注入**されません** — 直接アクセスします。
- テストダブルは手書き（モックライブラリなし）。

<h3 id="error-handling-jp">エラー処理</h3>

| レイヤー | プラクティス |
|---|---|
| **最適化** | スローせず `ApplyResult.False("reason")` を返す。ステップ単位の失敗追跡は `ExecutionScope` に任せる。 |
| **プロバイダーサービス** | システム呼び出しを try/catch で囲みエラーをログ。失敗ステップにリトライアクションを記録。 |
| **ViewModel** | コマンドハンドラーで例外をキャッチし、ユーザーフレンドリーなスナックバーを表示。 |
| **条件** | `ConditionResult.Error()` を返すか throw — `ConditionEvaluator` がキャッチして `Error`（ブロックしない）に変換。 |
| **してはいけない** | 処理できない例外のキャッチ。すべての例外を黙って握りつぶさない。 |

<h3 id="global-error-handling-jp">グローバルエラー処理</h3>

`App.xaml.cs` は 3 つのグローバル例外ハンドラを登録します：

- `AppDomain.CurrentDomain.UnhandledException` — 致命的例外を捕捉
- `TaskScheduler.UnobservedTaskException` — 未監視のタスク例外を捕捉
- `DispatcherUnhandledException` — 未処理の UI スレッド例外を捕捉

クラッシュの詳細はすべて `%localappdata%\optimizerDuck\Crashes\crash_*.log` に記録されます。

---

<h1 id="localization">ローカライズ</h1>

<h3 id="resx-files-jp">RESX ファイル</h3>

すべてのユーザー向け文字列は `Resources/Languages/Translations.resx`（英語のデフォルト）に格納されています。C# では型付きの `Translations` クラス、動的ルックアップでは `Loc.Instance["Key"]` を使用します。

- `Translations.Designer.cs` は**直接編集しないでください** — 自動生成です。
- [ResXManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager)（VS）または Rider 組み込みのリソースエディタを使用します。
- `{0}`、`{1}` などのフォーマットパラメータは正確に保持してください。
- 文字列は簡潔に — 一部の UI カードには幅制限があります。

<h3 id="available-locales-jp">利用可能なロケール</h3>

アプリは**17言語**を同梱しており、その数は増え続けています。ここに列挙すると古くなってしまうため、代わりに次を確認してください：

- **ロケールファイル自体**：`optimizerDuck/Resources/Languages/` — 言語ごとに `Translations.{locale}.resx` が 1 つ、英語のデフォルトとして `Translations.resx` があります。
- **単一の真実の情報源**：`Domain/UI/SupportedLanguages.cs` — `SupportedLanguages.All` が UI に表示される言語の正式なリストです（`SettingsViewModel` と `LegalDialogViewModel` が使用）。リストを重複させないでください。

<h3 id="adding-a-new-language-jp">新しい言語の追加</h3>

1. `Translations.{locale}.resx`（例：`Translations.de-DE.resx`）を `Translations.resx` と同じすべてのキーで作成します。
2. 言語を `Domain/UI/SupportedLanguages.cs` に登録します：

```csharp
new() { DisplayName = "Deutsch", Culture = new CultureInfo("de-DE") },
```

Settings とダイアログは `SupportedLanguages.All` 経由で自動的に反映されます — 他のファイルを更新する必要はありません。

<h3 id="runtime-language-switching-jp">ランタイム言語切り替え</h3>

アプリは再起動なしで言語を切り替えできます（`Loc.Instance.ChangeCulture`）。ベストプラクティス：

- ランタイムで更新すべき C# 文字列には `Loc.Instance["Key"]` または `Loc.Instance["Key", arg0, arg1]`（内部で `string.Format`）を使用します。
- 選択を `ConfigManager.SetAsync(x => x.App.Language, cultureName)` で永続化し、fire-and-forget + 失敗時リバートパターンを使います（`SettingsViewModel` と `LegalDialogViewModel` を参照）。
- 開いたまま言語が変わるダイアログでは、`ContentDialog` のプロパティを `Loc.Instance` にバインドしてタイトル/ボタンがライブ更新されるようにします：

```csharp
dialog.SetBinding(ContentDialog.TitleProperty,
    new Binding("[LegalDialog.Title]") { Source = Loc.Instance, Mode = BindingMode.OneWay });
dialog.SetBinding(ContentDialog.PrimaryButtonTextProperty,
    new Binding("[Button.Accept]") { Source = Loc.Instance, Mode = BindingMode.OneWay });
```

- 言語ピッカーには `SupportedLanguages.All` を再利用します（`ComboBox` で `DisplayMemberPath="DisplayName"`、`SelectedValuePath="Culture.Name"`、`SelectedValue="{Binding SelectedCultureName}"`）。
- 英語が最も正確である旨のヒントを表示します：`LegalDialog.Language.Tip` / `LegalDialog.Theme.Tip` — `Caption` + `Italic` + `TextFillColorTertiaryBrush`。

<h3 id="dialog-localization-jp">ダイアログのローカライズ</h3>

`LegalDialog` のようなダイアログは `Transient` で、表示ごとに `ViewModel` が再生成されます。`InitializeOnceAsync` で現在の `App.Language`/`App.Theme` を読み込み、`SelectedCultureName`/`CurrentApplicationTheme` を双方向バインディングで公開します。テーマは `ApplicationThemeManager.Apply` + `ThemeToIndexConverter`、GitHub アイコンは `ThemeToGitHubIconConverter` でライト/ダークを切り替えます。

<h3 id="hardcoded-string-rule-jp">文字列ハードコードの禁止</h3>

**文字列をハードコードしないでください**。常に次を使用します：

```csharp
// 型付き（推奨）
string title = Translations.Features_Desktop_Name;

// フォーマット引数付き — string.Format より Loc インデクサを推奨
string msg = Loc.Instance["Dashboard.SystemInfo.Storage.DiskInfo", used, total, percent];

// 動的キールックアップ（規則ベースのキー用）
string title = Loc.Instance[$"Optimizer.{category}.{key}.Name"];
```

XAML：

```xml
<!-- 引数なし -->
<ui:TextBlock Text="{ext:Loc Dashboard.Header.Title}" />

<!-- バインドされた引数付き -->
<ui:TextBlock Text="{ext:Loc Dashboard.UpdateInfoBar.Message, {Binding ViewModel.LatestVersion}}" />

<!-- ライブダイアログバインディング（Loc 変更時に更新） -->
<ui:TextBlock Text="{ext:Loc LegalDialog.Intro.Title}" />
```

---

<h1 id="pull-request-process">プルリクエストの手順</h1>

1. **`master` からブランチを作成** — master で直接作業しないこと：

   ```bash
   git checkout -b feature/your-feature-name
   # または
   git checkout -b fix/issue-number
   ```

2. **Conventional Commits でコミット**：

   | プレフィックス | 使用場面 |
   |---|---|
   | `feat:` | 新しい最適化や機能 |
   | `fix:` | バグ修正 |
   | `refactor:` | 動作を変えないコード再構成 |
   | `docs:` | ドキュメント更新 |
   | `test:` | テストの追加・修正 |
   | `i18n:` | 翻訳の更新 |
   | `chore:` | メンテナンス、ビルド設定、依存関係 |

3. **プッシュ前に確認**：

   ```bash
   # 1. ビルド
   dotnet build optimizerDuck.slnx --configuration Release

   # 2. テスト
   dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build

   # 3. 整形
   dotnet csharpier .

   # 4. git status を確認 — 意図したファイルだけがステージされていることを確認
   git status
   git diff --cached
   ```

4. **PR を開く**：
   - **何を**変え、**なぜ**変えたかを説明します。
   - UI 変更がある PR は**スクリーンショットを含めてください**。
   - 関連 Issue をリンク：`Closes #42`。
   - 作業中ならドラフトとしてマークします。

5. **レビュー**：メンテナがレビューします。フィードバックにオープンに、迅速に対応してください。

<h3 id="pr-checklist-jp">PR チェックリスト</h3>

- [ ] 既存のパターンに従っている（検出、属性、非同期命名）
- [ ] ローカライズキーが最低限 `Translations.resx` に追加されている
- [ ] 関連する場合は条件が宣言されている（[条件システム](#the-condition-system) を参照）
- [ ] `dotnet build` が成功（エラー 0 件）
- [ ] `dotnet test` が成功（全テスト緑）
- [ ] `dotnet csharpier .` を実行済み
- [ ] ハードコード文字列なし
- [ ] リバートステップが適切に記録されている（該当する場合）
- [ ] UI 変更のスクリーンショットあり

---

<h1 id="issue-guidelines">Issue ガイドライン</h1>

- **バグ報告**：Bug Report テンプレートを使用。再現手順、期待/実際の動作、`%localappdata%\optimizerDuck\optimizerDuck.log` のログ + システム仕様を含めてください。
- **機能リクエスト**：ユースケース、解決する問題、動作の仕方を説明します。
- **最適化の提案**：レジストリパス、サービス名、CLI コマンドを含めます。ドキュメントや信頼できる情報源へのリンクを付けます。
- **質問**：GitHub Discussions または [Discord](https://discord.gg/tDUBDCYw9Q) を利用してください。

---

<h1 id="faq--troubleshooting">FAQ とトラブルシューティング</h1>

<h3>ビルドが CA1416 エラーで失敗する</h3>

`.editorconfig` が CA1416 を抑制しています。それでも表示される場合は、master の最新 `.editorconfig` を使用しているか確認してください。このプロジェクトは Windows 専用です — `SupportedOSPlatform` ガードを追加しないでください。

<h3>最適化が UI に表示されない</h3>

チェックリスト：

- カテゴリクラス内の**ネストされた public クラス**ですか？
- カテゴリクラスは `IOptimizationCategory` を実装していますか？
- 最適化クラスは `BaseOptimization` を継承していますか？
- `[Optimization(Id = "...", ...)]` 属性がありますか？
- ローカライズキーは `Translations.resx` に追加されていますか？
- 最適化カテゴリはプリロード済みですか？（`OptimizationRegistry.IsPreloaded` を確認）
- `Condition` がブロックしていませんか？（次の質問を参照）

<h3>最適化/設定が「非対応」（ブロック）と表示される</h3>

- 属性に宣言された `Condition = typeof(...)` を確認してください。
- 条件型が `ICondition` を実装し、具象型で、public のパラメータなしコンストラクタを持つことを確認してください（`ConditionValidation` が起動時に強制します）。
- アイテムがブロックされるのは**未適用**の場合のみです。適用済みアイテムは常に通常のカードを表示します。

<h3>カスタマイズ設定が表示されない</h3>

- `[CustomizeSetting(Section = ..., Icon = ...)]` がありますか？（`Icon` は必須です。）
- `Section` の値のスペルは正しいですか？
- カテゴリクラスは正しい `[CustomizeCategory(PageType = ...)]` 属性を使っていますか？
- `Condition` がブロックしていませんか？

<h3>テスト後にリバートデータファイルがない</h3>

リバートデータを確認するテストは `%localappdata%\optimizerDuck\Revert\` のファイルを期待します。テストのクリーンアップは `finally` ブロックで実行されます — アサーションがクリーンアップの前に実行されることを確認してください。

<h3>最適化の適用中に UI がフリーズする</h3>

非同期プロバイダー呼び出し（`ChangeServiceStartupTypeAsync`、`CMDAsync`、`PowerShellAsync`）では `ApplyAsync` が `async`/`await` を使っていることを確認してください。`Task.FromResult` や `.Result` / `.Wait()` でのブロックは UI スレッドをフリーズさせます。

<h3>GUID の生成方法</h3>

```powershell
# PowerShell
[guid]::NewGuid()
```

```bash
# コマンドライン（uuidgen が利用可能な場合）
uuidgen
```

<h3>翻訳がキー名として表示される</h3>

`Translations.resx` へのローカライズキーの追加を忘れています。期待されるキーパターンは [ローカライズ](#localization) セクションを確認してください。

<h3>リバート時に「No revert data」エラー</h3>

最適化の `Id` GUID が変更されていないことを確認してください。リバートファイルは `Id` でキー付けされます。GUID を再生成すると、以前に適用した最適化に一致するリバートファイルがなくなります。

<h3>新しいリバートステップタイプの追加方法</h3>

1. `Domain/Revert/Steps/` に `IRevertStep` を実装する新しいクラスを作成します。
2. デシリアライズ用に静的な `FromData(JObject data)` メソッドを追加します。
3. `RevertManager` のリフレクションベースの `_stepRegistry` が自動検出します。
4. `ExecutionScope.RecordStep()` で `revertStep` パラメータとして記録します。

<h3>クラッシュセーフティの仕組み</h3>

- リバートファイルはアトミック書き込み（`.tmp` + `File.Replace`）。
- クラッシュログは `%localappdata%\optimizerDuck\Crashes\crash_*.log` に書き込み。
- `WmiHelper.Initialize()` が起動時に異常終了の WMI クリーンアップを登録。
- `App.xaml.cs` が 3 つのグローバル例外ハンドラを登録。

---

<div align="center">

<h2 id="credits">クレジット</h2>

マージされた PR の貢献者はリリースノートに記載されます。モジュールに大きく貢献した場合、ファイルヘッダー上部に作者タグを追加できます。

---

<h2 id="license">ライセンス</h2>

optimizerDuck に貢献することにより、あなたの貢献はプロジェクトの [GPL v3 ライセンス](../../LICENSE) の下でライセンスされることに同意したものとみなされます。

---

<p><i>optimizerDuck をより良くしてくれてありがとう。</i></p>

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
