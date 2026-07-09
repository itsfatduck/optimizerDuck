<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

**English** | [日本語](CONTRIBUTING.ja-JP.md) | [Türkçe](CONTRIBUTING.tr-TR.md)

[Introduction](#introduction) • [Getting Started](#getting-started) • [Architecture Overview](#architecture-overview) • [Ways to Contribute](#ways-to-contribute) • [Creating an Optimization](#creating-an-optimization) • [Creating a Customize Setting](#creating-a-customize-setting) • [The Refresh Scope System](#the-refresh-scope-system) • [Building New Features](#building-new-features) • [Revert System](#revert-system) • [Testing](#testing) • [Coding Standards](#coding-standards) • [Localization](#localization) • [Pull Request Process](#pull-request-process) • [Issue Guidelines](#issue-guidelines) • [FAQ & Troubleshooting](#faq--troubleshooting) • [License](#license)

</div>

---

# Introduction

Thanks for contributing to **optimizerDuck** — a free, open-source Windows optimization tool built with WPF on .NET 10.

You can help in many ways:
- Reporting bugs with clear reproduction steps
- Suggesting new optimizations or features (open an issue first)
- Improving documentation and guides
- Adding or fixing translations
- Contributing code: optimizations, customize settings, services, UI improvements

---

# Getting Started

### 1. Environment Setup

| Requirement | Notes |
|---|---|
| **Windows 10/11 x64** | The app runs as admin and makes system changes — Windows-only |
| **.NET 10 SDK** | Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **IDE** | [Visual Studio 2026](https://visualstudio.microsoft.com/) (`.NET desktop development` workload), [JetBrains Rider](https://www.jetbrains.com/rider/), or VS Code + C# Dev Kit |
| **Git** | Version control |

Verify your setup:

```bash
dotnet --version
# Should output 10.x
```

### 2. Fork and Clone

```bash
# Fork on GitHub first, then clone your fork
git clone https://github.com/<your-username>/optimizerDuck.git
cd optimizerDuck

# Add upstream remote to sync with the main repo
git remote add upstream https://github.com/itsfatduck/optimizerDuck.git

# Create a branch for your work (never work on master)
git checkout -b feature/your-feature-name
```

### 3. Restore, Build, Test

The solution uses the `.slnx` format (XML-based solution file, not `.sln`).

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

> If you add new NuGet dependencies, run `dotnet restore` again (then `--no-restore` for subsequent builds).

### 4. Publishing

```bash
publish.bat portable              # Portable folder (recommended for testing)
publish.bat single                # Single-file executable
publish.bat single --skip-tests   # Skip tests for quick iteration
publish.bat portable --no-pause   # Don't pause at the end (CI-friendly)
```

Publishing uses built-in dotnet publish profiles. The `publish.bat` script handles the workflow: it runs tests first (unless `--skip-tests`), then calls `dotnet publish` with the chosen profile.

### 5. Quick Start Checklist

Before your first contribution:

- [ ] Fork + clone the repo
- [ ] `dotnet build` succeeds (0 errors)
- [ ] `dotnet test` passes (all tests green)
- [ ] `dotnet csharpier .` formats without errors
- [ ] Read the [Architecture Overview](#architecture-overview) below

---

# Architecture Overview

### Solution Structure

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
│   │   │       ├── Bloatware/              # AppXPackage model for preinstalled apps
│   │   │       ├── Cleanup/                # CleanupItem for disk cleanup
│   │   │       ├── ScheduledTask/          # ScheduledTaskModel
│   │   │       ├── Services/               # RegistryItem, ServiceItem, ShellResult, ServiceStartupType
│   │   │       └── StartupManager/         # StartupApp, StartupTask models
│   │   ├── Revert/                         # RevertData, RevertResult, revert step types
│   │   │   └── Steps/                      # RegistryRevertStep, ServiceRevertStep, ScheduledTaskRevertStep, ShellRevertStep, UsbPowerRevertStep
│   │   └── UI/                             # Enums: OptimizationRisk, OptimizationTags, OptimizationCategoryOrder, CustomizeOrder, LanguageOption, OptimizationState, RiskVisual, ProcessingProgress
│   │
│   ├── Common/                             # Shared helpers, extensions, converters
│   │   ├── Converters/                     # 20+ WPF value converters (BooleanToVisibility, InverseBoolean, MBToGB, etc.)
│   │   ├── Extensions/                     # StringExtensions, CustomizePageRegistryExtensions, OptimizationPageRegistryExtensions, LanguageExtensions
│   │   ├── Helpers/                        # Shared.cs, ReflectionHelper.cs, SystemRefreshService.cs, EmbeddedResourceHelper.cs, GitHubSourceHelper.cs, WmiHelper.cs, ThemeResource.cs
│   │   └── Native/                         # Native interop helpers (currently available for future use)
│   │
│   ├── Services/                           # Business logic layer
│   │   ├── Configuration/                  # ConfigManager, LanguageManager
│   │   ├── Customize/                      # CustomizeRegistry (reflection-based discovery of customize settings)
│   │   ├── Optimization/                   # OptimizationRegistry, OptimizationService
│   │   │   └── Providers/                  # Static: RegistryService, ShellService, ScheduledTaskService, ServiceProcessService
│   │   ├── Revert/                         # RevertManager (atomic write/read of revert JSON files)
│   │   ├── System/                         # RegistryWatcher, StreamService, SystemInfoService, UpdaterService
│   │   └── UI/                             # BloatwareService, DiskCleanupService, StartupManagerService
│   │
│   ├── UI/                                 # WPF pages, ViewModels, controls, styles
│   │   ├── Behaviors/                      # SmoothScrollBehavior
│   │   ├── Controls/                       # FilledNavigationViewItem (custom nav control)
│   │   ├── Dialogs/                        # ProcessingDialog, OptimizationDetailsDialog, OptimizationResultDialog, RestorePointDialog, LegalDialog, BloatwareConfirmationDialog, ScheduledTaskCreateDialog, ScheduledTaskDetailsDialog, StartupTaskDetailsPanel
│   │   ├── Pages/                          # App pages + sub-folders
│   │   │   ├── Customize/                  # CustomizePage + Categories/ (auto-registered category pages)
│   │   │   ├── Optimize/                   # OptimizePage + Categories/ (auto-registered category pages)
│   │   │   ├── DashboardPage
│   │   │   ├── SettingsPage
│   │   │   ├── BloatwarePage
│   │   │   ├── DiskCleanupPage
│   │   │   ├── StartupManagerPage
│   │   │   └── ScheduledTasksPage
│   │   ├── Styles/                         # FluentDesign.xaml, NavigationViewOverride.xaml, ToolTipOverride.xaml
│   │   ├── ViewModels/                     # Page and dialog ViewModels
│   │   │   ├── Customize/                  # CustomizeItemViewModel, CustomizeCategoryViewModel
│   │   │   ├── Dialogs/                    # ProcessingViewModel, OptimizationDetailsViewModel, OptimizationResultDialogViewModel, BloatwareConfirmationDialogViewModel
│   │   │   ├── Optimizer/                  # OptimizationCategoryViewModel
│   │   │   ├── Pages/                      # DashboardViewModel, OptimizeViewModel, CustomizeViewModel, SettingsViewModel, BloatwareViewModel, DiskCleanupViewModel, StartupManagerViewModel, ScheduledTasksViewModel
│   │   │   └── Windows/                    # MainWindowViewModel
│   │   └── Windows/                        # MainWindow
│   │
│   └── Resources/                          # Images, embedded assets, localization
│       ├── Embedded/                       # Power plans (optimizerDuck.pow), Icons (blank.ico)
│       ├── Images/                         # Duck.png, GitHubLogo, DiscordLogo
│       └── Languages/                      # Translations.resx + 11 locale variants
│
└── optimizerDuck.Test/                     # xUnit v3 test project
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

### Key Design Decisions

| Decision | Rationale |
|---|---|
| **Reflection-based discovery** | No DI registration arrays to update. `ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` scans `optimizerDuck.*` assemblies at startup. New optimizations/settings are auto-discovered. |
| **Static provider services** | `RegistryService`, `ShellService`, `ScheduledTaskService`, `ServiceProcessService` are static classes. They capture revert steps into the ambient `ExecutionScope` — no need to inject or pass context. |
| **File-based revert tracking** | Applied state = file exists on disk (`%localappdata%\optimizerDuck\Revert\{id}.json`). No database. Atomic writes via `File.Replace()`. |
| **Integration-style tests** | Real filesystem, real registry (under `HKCU\Software\TestOptimizerDuck*`), real process execution. No mocking libraries — hand-written test doubles only. |
| **Async service methods** | Provider methods that run external processes are async (`*Async` suffix). Optimization `ApplyAsync` methods should use `async`/`await` to keep the UI responsive. |
| **Static WMI helper** | `WmiHelper.Initialize()` runs at startup to register WMI event cleanup handlers for abnormal process termination. |
| **Pending changes tracking** | `App.HasPendingChanges` property tracks whether applied optimizations haven't been reverted. The app warns on close with options to restart PC/Explorer or exit. |

---

# Ways to Contribute

| Contribution Type | Description | Where to Start |
|---|---|---|
| **New Optimizations** | Registry tweaks, service changes, system tweaks | `Domain/Optimizations/Categories/*.cs` |
| **New Customize Settings** | UI toggles for Windows settings (Game Mode, Mouse Acceleration, Taskbar, etc.) | `Domain/Customize/Categories/*.cs` |
| **New App Features** | New pages, tools, or functionality | Open an issue first |
| **Bug Fixes** | Crash fixes, logic errors, UI issues | Anywhere |
| **Translations** | New languages or fixing existing translations | `Resources/Languages/Translations.*.resx` |
| **Documentation** | README, CONTRIBUTING, etc. | `*.md` files |
| **Testing** | Adding/reviewing tests for existing or new optimizations | `optimizerDuck.Test/` |

---

# Creating an Optimization

### How Discovery Works

At startup:

1. `ReflectionHelper.FindImplementationsInLoadedAssemblies<IOptimizationCategory>()` scans all `optimizerDuck.*` assemblies
2. It finds every class implementing `IOptimizationCategory`
3. For each category, it scans **nested public classes** implementing `IOptimization`
4. All discovered optimizations are instantiated and `OwnerType` is assigned automatically
5. `OptimizationService.UpdateOptimizationStateAsync` scans revert files on disk to mark each optimization as Applied or not
6. `OptimizationRegistry.StartPreload()` runs this on a background thread at startup for non-blocking loading

**Your job**: Create a nested class inside a category, extend `BaseOptimization`, decorate with `[Optimization]`. That's it.

### Optimization Categories

Current categories (in `Domain/Optimizations/Categories/`):

| File | Attribute | Focus |
|---|---|---|
| `Performance.cs` | `[OptimizationCategory(typeof(PerformanceOptimizerPage))]` | RAM tuning, process priority, keyboard latency, multimedia scheduler, accessibility keyboard hotkeys |
| `SecurityAndPrivacy.cs` | `[OptimizationCategory(typeof(SecurityAndPrivacyOptimizerPage))]` | Telemetry, error reporting, advertising ID, location, Cortana, Copilot, content delivery manager, activity history, auto-logger |
| `Gpu.cs` | `[OptimizationCategory(typeof(GpuOptimizerPage))]` | AMD/NVIDIA/Intel registry tweaks, power states, clock gating, ASPM, async flips |
| `PowerManagement.cs` | `[OptimizationCategory(typeof(PowerManagementOptimizerPage))]` | Hibernation, fast startup, USB selective suspend, custom power plan installation, power saving disable |
| `BloatwareAndServices.cs` | `[OptimizationCategory(typeof(BloatwareAndServicesOptimizerPage))]` | OEM preinstalled app blocking, 170+ Windows service startup type optimization |
| `UserExperience.cs` | `[OptimizationCategory(typeof(UserExperienceOptimizerPage))]` | Menu delays, visual effects, taskbar animations, transparency, Start Menu web search |

### Step-by-Step: Add to an Existing Category

Pick the best-matching category file and add a nested class:

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

### Key Rules

| Rule | Detail |
|---|---|
| **`Id` must be a new GUID** | Used for revert file naming and applied-state tracking. Generate with `[guid]::NewGuid()` in PowerShell. |
| **Extend `BaseOptimization`** | Provides `Name`, `ShortDescription`, `Prefix`, `RiskVisual`, `TagDisplays` from attribute + localization keys |
| **Use `async Task<ApplyResult>`** | Not `Task.FromResult()`. Service providers are async — await them to keep the UI responsive. |
| **Return `CompleteFromScope()`** | Derives `ApplyResult` from steps recorded in the ambient `ExecutionScope` |
| **Report progress** | Use `progress.Report(new ProcessingProgress { ... })` to update the UI dialog |
| **Don't catch all exceptions** | Let them bubble up. `ExecutionScope` tracks success/failure. The `OptimizationService` layer handles exceptions. |
| **Don't manually create revert steps** | Static provider services do this automatically via `ExecutionScope.RecordStep()` |
| **Use `context.Logger`** | The optimization context provides a logger — use it for important diagnostic info |
| **Check `context.Snapshot`** | `OptimizationContext.Snapshot` (a `SystemSnapshot`) gives you system info: RAM, GPU info, CPU. Use it for conditional logic (e.g., skip if insufficient RAM). |

### Available Service Providers

These **static** classes handle logging, error handling, and automatic revert step recording.

| Service | Key Methods | Why It's Used |
|---|---|---|
| **`RegistryService`** | `Write()`, `Read<T>()`, `DeleteValue()`, `CreateSubKey()`, `DeleteSubKeyTree()`, `KeyExists()` | Read/write/delete registry keys. Backs up original values for revert. Supports writing multiple RegistryItem entries in one call via params array. |
| **`ShellService`** | `CMDAsync()`, `PowerShellAsync()`, `CMD()` (sync), `PowerShell()` (sync) | Run CMD or PowerShell commands. Prefer async variants. Both sync and async methods accept an optional `revertCommand` parameter. |
| **`ScheduledTaskService`** | `DisableTask()`, `EnableTask()`, `IsTaskEnabled()`, `DeleteTask()` | Manage Windows Scheduled Tasks. |
| **`ServiceProcessService`** | `ChangeServiceStartupTypeAsync()`, `GetStartupTypeAsync()` | Manage Windows Services. Always use async variants. Supports changing multiple services at once via params array. |

> **Methods accepting multiple items via params**: Most write/change methods accept a params array of items (e.g., `RegistryService.Write(item1, item2, item3)`). This is more efficient than multiple individual calls.

Example usage:

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

### Handling Asynchronous Operations

Not all optimizations need `async`/`await`. If your optimization only does synchronous registry writes (no async calls), you can return `Task.FromResult()`:

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

But if you use any async provider (service, shell, task), always `await` them:

```csharp
public override async Task<ApplyResult> ApplyAsync(...)
{
    await ServiceProcessService.ChangeServiceStartupTypeAsync(...);
    return CompleteFromScope();
}
```

### Create a New Category

Only if your optimizations don't fit any existing category. Avoid hyper-specific categories.

1. Create `Domain/Optimizations/Categories/YourCategory.cs`
2. Implement `IOptimizationCategory`
3. Apply `[OptimizationCategory(PageType = typeof(YourPage))]` — you'll also need a XAML page
4. Add a member to `OptimizationCategoryOrder` enum in `Domain/UI/OptimizationCategoryOrder.cs`
5. The XAML page auto-registers via `services.AddAllOptimizationPages()` in `App.xaml.cs`

### Create an Optimization Helper Base Class

If you have multiple optimizations that share the same structure (like GPU tweaks that all iterate over detected GPUs), create an abstract intermediate class:

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

See `Gpu.cs` for a real example with AMD, NVIDIA, and Intel subclasses.

### Localization Keys

Every optimization needs entries in `Translations.resx`. The keys follow a strict convention:

```
Optimizer.{CategoryName}.{OptimizationKey}.Name
Optimizer.{CategoryName}.{OptimizationKey}.ShortDescription
Optimizer.{CategoryName}.{OptimizationKey}.Progress.{CustomKey}
Optimizer.{CategoryName}.{OptimizationKey}.Error.{CustomKey}
```

Where `CategoryName` = category class name (e.g., `Performance`) and `OptimizationKey` = nested class name.

> [!IMPORTANT]
> **Translations required**. If you skip adding these keys, the app displays raw key strings like `"Optimizer.Performance.MyNewTweak.Name"`. Always add entries in `Translations.resx` (English) at minimum.

---

# Creating a Customize Setting

Customize settings are UI controls (toggle switches, dropdowns, number inputs) that flip Windows settings ON or OFF. They live in `Domain/Customize/Categories/`.

### Customize Categories

| File | Attribute | Focus |
|---|---|---|
| `Desktop.cs` | `[CustomizeCategory(PageType = typeof(DesktopFeatureCategory))]` | Desktop icons (This PC, Recycle Bin, Network, User Files, Control Panel), global show/hide icons, shortcut arrow visibility |
| `Preferences.cs` | `[CustomizeCategory(PageType = typeof(PreferencesFeatureCategory))]` | Taskbar alignment, widgets, task view button, end task, dark mode, file extensions, hidden files, clipboard history, search mode, seconds in clock, Bing search, classic context menu |
| `Gaming.cs` | `[CustomizeCategory(PageType = typeof(GamingFeatureCategory))]` | Game Mode, Game Bar, background recording, mouse acceleration, fullscreen optimizations, GPU scheduling |
| `SystemFeatures.cs` | `[CustomizeCategory(PageType = typeof(SystemFeatureCategory))]` | Num Lock on boot, Developer Mode, allow all trusted apps, long paths enabled, battery percentage |

### Step-by-Step: Simple Registry Toggle

For a simple on/off registry toggle, the base class does all the work:

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

### RegistryToggle Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Path` | `string` | required | Full registry key path (e.g., `@"HKCU\Software\..."`) |
| `Name` | `string` | required | Registry value name |
| `OnValue` | `object?` | `1` | Value representing "on" state |
| `OffValue` | `object?` | `0` | Value representing "off" state |
| `DefaultValue` | `object?` | `0` | Fallback when registry value is missing |
| `IsOptional` | `bool` | `false` | If `true`, not required for state detection |
| `TreatMissingAsDefault` | `bool` | `false` | If `true`, missing key uses `DefaultValue` instead of treating as "off" |
| `ValueKind` | `RegistryValueKind` | `DWord` | Registry value type (DWord, String, etc.) |

**State detection logic**: `GetState()` (in `BaseCustomizeSetting`) collects all non-optional `RegistryToggles` and returns `true` only when **every** required toggle matches its `OnValue`.

### Control Types

| Type | Rendered As | Used For |
|---|---|---|
| `Toggle` | On/off switch | Most settings (default) |
| `Dropdown` | ComboBox | Multiple choice (e.g., power plan, search box mode, taskbar alignment) |
| `Option` | Radio button group | Mutually exclusive visual options (e.g., left/center alignment) |
| `NumberInt` | Integer text input | Numeric values (e.g., seconds) |
| `NumberFloat` | Decimal text input | Precision values |
| `String` | Text input | Free-form text |

Override `ControlType` to change the UI control:

```csharp
public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;
```

### Dropdown with Options

For settings with multiple choices:

```csharp
public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;

public override IReadOnlyList<SettingOption>? Options =>
    [
        Option("Never", 0),      // Option() helper reads from Translations.resx:
        Option("Battery", 1),    //   Customize.{Category}.{Feature}.Options.Never
        Option("Always", 2),     //   Customize.{Category}.{Feature}.Options.Always
    ];

public override async Task ApplyAsync(object? value)
{
    var intValue = value is int i ? i : 0;
    RegistryService.Write(new RegistryItem(Path, "ValueName", intValue));
    await ExecutePostActionAsync();  // MUST call when overriding ApplyAsync
}
```

### Dynamic Options (Platform-Aware)

You can conditionally show options based on the Windows version:

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

### CurrentValue for Dropdowns

For dropdowns and option-based controls, implement `CurrentValue` so the UI shows the current system state:

```csharp
public override object? CurrentValue =>
    RegistryService.Read<object>(new RegistryItem(RegPath, RegName));
```

### Custom Logic (Override GetStateAsync / ApplyAsync)

For settings that aren't simple registry toggles (e.g., mouse acceleration combines 3 registry values):

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

### State Detection with Retry

After applying a value, the UI calls `GetStateWithRetryAsync()` (not `GetStateAsync()`). This method:
1. Reads state up to `maxRetries` (default 3) times with `delayMs` (default 80ms) between attempts
2. Returns when two consecutive reads agree on the same value (convergence check)
3. Falls back to the last read value after exhausting retries

This prevents the UI from showing stale values while the registry settles after a write.

### Custom Logic with Non-Registry Dependencies

For settings that involve embedded resource extraction (like replacing shortcut arrows with a blank icon):

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

Use `EmbeddedResourceHelper.TryExtract(resourceName, outputPath)` to extract embedded resources from the assembly to disk.

### The Recommendation System

Each customize setting can declare a recommendation:

```csharp
[CustomizeSetting(
    ...,
    Recommendation = RecommendationState.On    // or Off, Depends, Experimental, None
)]
```

Available states:
- **`On`**: Recommended to turn ON — improves system
- **`Off`**: Recommended to turn OFF — improves system
- **`Depends`**: Depends on user's specific needs/configuration
- **`Experimental`**: May be unstable, use with caution
- **`None`** (default): No recommendation displayed

You can also add a recommendation reason via localization key: `Customize.{Category}.{Feature}.Recommendation.Reason`.

### What to Override per Pattern

| Scenario | Override |
|---|---|
| Simple registry toggle | `RegistryToggles` + `RefreshScope` |
| Multiple registry toggles (e.g., Game Mode: 2 values) | `RegistryToggles` (list them all) |
| Dropdown/Options | `ControlType` → `Dropdown`, `Options`, custom `ApplyAsync`, `CurrentValue` |
| Multi-value logic (e.g., mouse accel: 3 registry values) | `GetStateAsync()` + `ApplyAsync()` + `GetWatchedRegistryPaths()` |
| Setting with no registry interaction | `GetStateAsync()` + `ApplyAsync()` (full custom) |
| Custom refresh behavior | `RefreshScope` (if only changing flags) or `ExecutePostActionAsync()` (full override) |
| State detection with convergence | Use `GetStateWithRetryAsync()` (built-in — don't override) |
| Dynamic options per Windows version | Override `Options` getter with conditional logic |
| Embedded resource extraction | `EmbeddedResourceHelper.TryExtract()` in custom `ApplyAsync` |

### Create a New Category

1. Create `Domain/Customize/Categories/YourCategory.cs`
2. Implement `ICustomizeCategory` with `[CustomizeCategory(PageType = typeof(YourPage))]`
3. Add a member to `CustomizeOrder` enum in `Domain/UI/CustomizeOrder.cs`
4. Create the XAML page (a new class in `UI/Pages/Customize/Categories/`)
5. The page auto-registers via `services.AddAllCustomizeCategoryPages()` in `App.xaml.cs`

### Localization Keys for Customize Settings

```
Customize.{CategoryName}.{SettingKey}.Name
Customize.{CategoryName}.{SettingKey}.Description
Customize.{CategoryName}.{SettingKey}.Options.{OptionKey}    (if using SettingOption)
Customize.{CategoryName}.{SettingKey}.Recommendation.Reason   (if Recommendation != None)
Customize.{CategoryName}.Section.{SectionName}                (for section headers)
```

---

# The Refresh Scope System

When a customize setting changes state, different Windows surfaces need different refresh strategies. The `CustomizeRefreshScope` [Flags] enum controls this granularly.

### Available Flags

| Member | Value | Effect | P/Invoke |
|---|---|---|---|
| `None` | `0` | No refresh | — |
| `Settings` | `1 << 0` | Broadcast `WM_SETTINGCHANGE` so apps re-read registry | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE)` |
| `Associations` | `1 << 1` | Notify shell that file associations or icon cache changed | `SHChangeNotify(SHCNE_ASSOCCHANGED)` |
| `Desktop` | `1 << 2` | Force desktop icon list (`SysListView32`) to repaint | `LVM_REFRESH` + `LVM_UPDATE` |
| `Taskbar` | `1 << 3` | Broadcast taskbar-targeted `WM_SETTINGCHANGE` ("TraySettings") | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, "TraySettings")` |
| `PolicyUpdate` | `1 << 4` | Push `SystemParametersInfo` with `SPIF_SENDCHANGE` for per-user params | `SystemParametersInfo(SPI_SETDESKWALLPAPER)` |
| `Theme` | `1 << 5` | Broadcast `WM_THEMECHANGED` for theme/visual tweaks | `SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED)` |
| `DesktopIconCache` | `1 << 6` | Toggle HideIcons registry + send `WM_COMMAND 0x7402` to desktop | Registry read + `SendMessage(Progman, WM_COMMAND)` |

### Named Composites

| Name | Composition | Use Case |
|---|---|---|
| `Default` | `Settings \| Associations` | General explorer-level settings |
| `DesktopIcons` | `Settings \| Desktop` | Show/hide individual desktop icons (This PC, Recycle Bin) |
| `HideDesktopIcons` | `Settings \| DesktopIconCache` | Global "Hide all desktop icons" toggle |
| `TaskbarSettings` | `Settings \| Taskbar` | Taskbar alignment, widgets, task view, end task |
| `ExplorerView` | `Settings \| Associations \| PolicyUpdate` | File extensions, hidden files, compact view |

### How Refresh Flows

```
Setting toggle → BaseCustomizeSetting.ApplyAsync(value)
  ├─ Writes RegistryToggles (if any)
  ├─ Checks NeedsPostAction (true if RefreshScope != None)
  └─ Task.Run → ExecutePostActionAsync()
       ├─ Checks each CustomizeRefreshScope flag
       ├─ Calls SystemRefreshService methods (P/Invoke)
       └─ Win32 notifications sent to Windows
```

If you override `ApplyAsync`, you **must** call `await ExecutePostActionAsync()` yourself to trigger the refresh. The base class only does this automatically when using the default `RegistryToggles`-based apply.

---

# Building New Features

If you want to add a new page or tool (e.g., a "Network Monitor"):

1. **Open a GitHub Issue first** — describe the feature, use case, and design. Wait for maintainer feedback.
2. **Implementation order**:

```csharp
// 1. Service layer in Services/UI or Services/System/YourService.cs
public class YourService(ILogger<YourService> logger) { ... }

// 2. ViewModel in UI/ViewModels/Pages/YourViewModel.cs
//    Extends ViewModel (which extends ObservableValidator + INavigationAware)

// 3. XAML Page in UI/Pages/YourPage.xaml (+ code-behind)

// 4. Register as singletons in App.xaml.cs
services.AddSingleton<YourViewModel>();
services.AddSingleton<YourPage>();
```

- ViewModels and Pages **must** be registered as singletons in `App.xaml.cs`
- Navigation is handled by WPF UI (`INavigationService`)
- Follow the existing patterns — check `DashboardPage`, `OptimizePage`, `BloatwarePage`, `DiskCleanupPage`, `ScheduledTasksPage`, `StartupManagerPage`, etc.

### DI Registration Pattern (from App.xaml.cs)

```csharp
// Pages + ViewModels — one pair per feature
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

// Customize
services.AddSingleton<CustomizeViewModel>();
services.AddSingleton<CustomizePage>();

// Automatic page registration (category pages using reflection)
services.AddAllCustomizeCategoryPages();   // scans [CustomizeCategory] attributes
services.AddAllOptimizationPages();        // scans [OptimizationCategory] attributes

// Managers
services.AddSingleton<ConfigManager>();
services.AddSingleton<RevertManager>();

// Services
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

### System Services Reference

Additional services available for feature development:

| Service | Purpose |
|---|---|
| `SystemInfoService` | Provides `Snapshot` property (`SystemSnapshot`) with CPU, RAM, GPU info via WMI. Used in optimization context. |
| `StreamService` | Downloads remote resources (e.g., updated power plan files). Used via `OptimizationContext.StreamService`. |
| `UpdaterService` | Checks GitHub releases for updates. Shows update prompt on Dashboard. |
| `RegistryWatcher` | Monitors registry keys for external changes and notifies the UI to refresh. Implements `IRegistryWatcher`. |
| `BloatwareService` | Lists preinstalled AppX packages, categorizes them as Safe/Caution/Dangerous. |
| `DiskCleanupService` | Scans disks for cleanup opportunities (temp files, caches, logs). |
| `StartupManagerService` | Lists and manages startup applications and scheduled tasks. |

---

# Revert System

Every applied optimization creates a JSON file at `%localappdata%\optimizerDuck\Revert\{optimizationId}.json`.

### How It Works

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

### Step Types

| Step Type | Records | Automatically Created By |
|---|---|---|
| **`RegistryRevertStep`** | Original registry value before change | `RegistryService.Write()`, `RegistryService.DeleteValue()`, `RegistryService.CreateSubKey()`, `RegistryService.DeleteSubKeyTree()` |
| **`ServiceRevertStep`** | Original service startup type | `ServiceProcessService.ChangeServiceStartupTypeAsync()` |
| **`ScheduledTaskRevertStep`** | Original task state (enabled/disabled) | `ScheduledTaskService.DisableTask()`, `ScheduledTaskService.EnableTask()` |
| **`ShellRevertStep`** | Shell command to reverse the change | `ShellService.CMDAsync()`, `ShellService.PowerShellAsync()` — pass a `revertCommand` parameter |
| **`UsbPowerRevertStep`** | USB power settings (per-device) | USB-related optimizations (manual via `ExecutionScope.RecordStep()`) |

### Adding a Revert Command to Shell Calls

When calling `CMDAsync` or `PowerShellAsync`, you can optionally pass a `revertCommand` parameter that gets saved for undo:

```csharp
// The revert command "powercfg /h on" will be stored to reverse this change
await ShellService.CMDAsync("powercfg /h off", "powercfg /h on");
```

### Revert Data Format

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

### Key Details

- **Applied state** is inferred from file presence on disk (`RevertManager.IsAppliedAsync(id)`)
- **Atomic writes**: writes to `.tmp` then `File.Replace()` — crash-safe
- **Concurrent access**: per-file `SemaphoreSlim` locks prevent race conditions; 30-second timeout
- **File locking**: `_fileLocks` dictionary (`ConcurrentDictionary<Guid, SemaphoreSlim>`) protects each revert file
- **`ExecutionScope`** uses `AsyncLocal<ExecutionScope?>` for ambient step tracking. No need to pass context through parameters
- **Revert executes steps in reverse order** (last applied = first reverted)
- **Partial success**: revert continues even if some steps fail. Failed steps get retry actions recorded
- **Retry**: `OptimizationService.RetryFailedStepsAsync()` can retry individual failed steps
- **Upsert**: `RevertManager.UpsertRevertStepAtIndexAsync()` can add/replace revert steps at specific indices (used during retry)
- **Step registry**: Revert step deserialization uses reflection-based `_stepRegistry` — new step types auto-register by implementing `IRevertStep` with a static `FromData(JObject)` method

> **Important**: When you call provider services (`RegistryService.Write`, `ShellService.CMDAsync`, etc.), revert steps are recorded automatically. Do NOT manually create revert steps unless you're implementing a custom provider (like `UsbPowerRevertStep`).

---

# Testing

Tests use **xUnit v3** and follow an integration-style approach with real I/O.

### Test Patterns

| Pattern | Detail |
|---|---|
| **No mocking libraries** | All test doubles are hand-written classes implementing interfaces |
| **Real I/O** | Real filesystem (revert JSON files), real registry (`HKCU\Software\TestOptimizerDuck*`), real process execution (CMD, PowerShell) |
| **Cleanup** | Use `try/finally` or `IDisposable` for test artifact cleanup |
| **Naming** | `{Method}_{Scenario}_{ExpectedResult}` — e.g., `ApplyAsync_Success_PersistsRevertDataFile` |
| **Logging** | Use `NullLogger<T>.Instance` / `NullLoggerFactory.Instance` for DI logging parameters |
| **STA thread** | Tests involving `ContentDialogService` or WPF components must use `RunInStaThreadAsync` helper |

### Test Structure

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
│   │   ├── Models/
│   │   │   └── Services/RegistryItemKindDetectionTests.cs
│   │   └── PowerManagementTests.cs
│   └── Revert/Steps/
│       ├── RevertStepSerializationTests.cs
│       └── ScheduledTaskRevertStepTests.cs
└── Services/
    ├── ApplyRevertComprehensiveTests.cs
    ├── OptimizationServiceTests.cs
    ├── OptimizationServiceIntegrationTests.cs
    ├── OptimizationExecutionContextTests.cs
    ├── OptimizationServices/
    │   ├── RegistryServiceTests.cs
    │   ├── ServiceProcessServiceTests.cs
    │   ├── ShellServiceTests.cs
    │   └── ShellPolicyTests.cs
    ├── Managers/
    │   └── RevertManagerTests.cs
    ├── RegistryWatcherTests.cs
    └── SystemInfoServiceTests.cs
```

### Running Tests

```bash
# After building
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build

# Build + test in one step
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release
```

### CI Integration

The CI pipeline (`ci.yml`) runs:

```bash
dotnet restore optimizerDuck.slnx
dotnet build optimizerDuck.slnx --configuration Release --no-restore
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build --blame-hang --blame-hang-timeout 30s
```

The `--blame-hang --blame-hang-timeout 30s` flags ensure tests don't hang longer than 30 seconds, which is critical for integration-style tests that interact with real Windows services.

### Writing Tests for Provider Services

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

### Registering New Tests

When adding tests for a new optimization or service, follow the existing directory structure. Tests for `OptimizationServices/` go in `Services/OptimizationServices/`, and tests for domain models go in the matching `Domain/` subdirectory.

---

# Coding Standards

### Language Features

| Feature | Used? | Notes |
|---|---|---|
| File-scoped namespaces | Yes | `namespace X.Y;` |
| Collection expressions | Yes | `[]` for empty, `[item1, item2]` for lists |
| Primary constructors | Some | Used in simple types |
| Implicit usings | Yes | Enabled in `.csproj` |
| Nullable reference types | Yes | `<Nullable>enable</Nullable>` — handle nulls properly |
| Extension methods (`extension(T type)`) | Yes | C# 13 feature, used in `OptimizationTagsToDisplay` |

### Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Classes, enums, interfaces, methods, properties | `PascalCase` | `RegistryService`, `ApplyAsync` |
| Private fields | `_camelCase` | `_lastError`, `registryService` |
| Local variables, parameters | `camelCase` | `progress`, `serviceName` |
| Async methods | `*Async` suffix | `ChangeServiceStartupTypeAsync`, `CMDAsync` |
| Public constants | `PascalCase` | `MaxRetries` |
| Private constants | `_PascalCase` | `_defaultTimeout` |

### Formatting

| Setting | Value |
|---|---|
| Indentation | 4 spaces (no tabs) |
| End of line | LF |
| Encoding | UTF-8 |
| Max line length | 100 characters |
| Trailing whitespace | Trimmed |
| Final newline | Required |
| Formatter | **CSharpier** — run `dotnet csharpier .` before committing |
| CA1416 | Silenced via `.editorconfig` — all code is Windows-only |

### Code Style

- **No hardcoded strings** — always use `Translations.KeyName` or `Loc.Instance["Key"]`
- **Keep comments sparse** — existing code has almost none. Don't add unnecessary comments.
- **No type error suppression** — handle types properly.
- **Prefer existing libraries** over new dependencies.
- **Prefer small, focused changes** over large refactors.
- **Use `@formatter:off` / `@formatter:on`** comments around large, structured registry write blocks when needed to suppress automatic reformatting.
- **Never commit machine-specific paths or secrets.**

### Dependency Injection

- Services, ViewModels, and Pages are registered as singletons in `App.xaml.cs`
- Use constructor injection: `public class Foo(Bar bar, Baz baz)` or `public class Foo(ILogger<Foo> logger)`
- Static provider services (`RegistryService`, `ShellService`, `ScheduledTaskService`, `ServiceProcessService`) are NOT injected — access them directly
- Test doubles are hand-written (no mocking libraries like Moq)

### Error Handling

| Layer | Practice |
|---|---|
| **Optimizations** | Return `ApplyResult.False("reason")` instead of throwing. Let `ExecutionScope` handle step-level failure tracking. |
| **Provider services** | Use try/catch around system calls, log errors via `ExecutionScope.LogError`. Record failed steps with retry actions. |
| **ViewModels** | Catch exceptions in command handlers, show user-friendly snackbars. |
| **Don't** | Catch exceptions you can't handle. Don't silently swallow all exceptions. |

### Global Error Handling

The `App.xaml.cs` registers three global exception handlers:

- `AppDomain.CurrentDomain.UnhandledException` — catches fatal exceptions
- `TaskScheduler.UnobservedTaskException` — catches unobserved task exceptions
- `DispatcherUnhandledException` — catches unhandled UI thread exceptions

All crash details are logged to `%localappdata%\optimizerDuck\Crashes\crash_*.log`.

---

# Localization

### RESX Files

All user-facing strings live in `Resources/Languages/Translations.resx`. Use the strongly-typed `Translations` class in C#, or `Loc.Instance["Key"]` for dynamic lookup.

- **Do not edit** `Translations.Designer.cs` directly — it's auto-generated
- Use [ResXManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager) (VS) or Rider's built-in resource editor
- Preserve format parameters like `{0}`, `{1}` exactly
- Keep strings concise — some UI cards have width limits

### Available Locales

| Language | File |
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

> **Note**: 11 locale variants total, plus English as the default fallback.

### Adding a New Language

1. Create `Translations.{locale}.resx` (e.g., `Translations.de-DE.resx`) with all the same keys as `Translations.resx`
2. Register the language in `UI/ViewModels/Pages/SettingsViewModel.cs`:

```csharp
new() { DisplayName = "Deutsch", Culture = new CultureInfo("de-DE") },
```

### Hardcoded String Rule

**Never hardcode strings**. Always use:

```csharp
// Strongly typed (recommended)
string title = Translations.Features_Desktop_Name;

// With format args
string msg = string.Format(Translations.Dashboard_SystemInfo_Storage_DiskInfo, used, total, percent);

// Dynamic key lookup (for convention-based keys)
string title = Loc.Instance[$"Optimizer.{category}.{key}.Name"];
```

In XAML:

```xml
<!-- Without args -->
<ui:TextBlock Text="{ext:Loc Dashboard.Header.Title}" />

<!-- With bound args -->
<ui:TextBlock Text="{ext:Loc Dashboard.UpdateInfoBar.Message, {Binding ViewModel.LatestVersion}}" />
```

---

# Pull Request Process

1. **Branch from `master`** — never work directly on master:

   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/issue-number
   ```

2. **Commit with Conventional Commits**:

   | Prefix | When to Use |
   |---|---|
   | `feat:` | New optimizations or features |
   | `fix:` | Bug fixes |
   | `refactor:` | Code restructuring without behavior change |
   | `docs:` | Documentation updates |
   | `test:` | Adding or fixing tests |
   | `i18n:` | Translation updates |
   | `chore:` | Maintenance, build config, dependencies |

3. **Before pushing, verify**:

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

4. **Open the PR**:
   - Describe **what** changed and **why**
   - If your PR has UI changes, **include a screenshot**
   - Link related issues: `Closes #42`
   - Mark as draft if still a work in progress

5. **Review**: A maintainer will review. Be open to feedback and respond promptly.

### PR Checklist

- [ ] Code follows existing patterns (discovery, attributes, async naming)
- [ ] Localization keys added to `Translations.resx` at minimum
- [ ] `dotnet build` succeeds (0 errors)
- [ ] `dotnet test` passes (all tests green)
- [ ] `dotnet csharpier .` has been run
- [ ] No hardcoded strings
- [ ] Revert steps are properly recorded (if applicable)
- [ ] UI changes include a screenshot

---

# Issue Guidelines

- **Bug reports**: Use the Bug Report template. Include steps to reproduce, expected vs actual behavior, and logs from `%localappdata%\optimizerDuck\optimizerDuck.log` + system specs.
- **Feature requests**: Describe the use case, the problem it solves, and how it should work.
- **Optimization suggestions**: Include registry paths, service names, or CLI commands. Link to documentation or credible sources.
- **Questions**: Use GitHub Discussions or join the [Discord server](https://discord.gg/tDUBDCYw9Q).

---

# FAQ & Troubleshooting

### Build fails with "CA1416" errors

The `.editorconfig` silences CA1416. If you're still seeing it, ensure you have the latest `.editorconfig` from master. This project is Windows-only — don't add `SupportedOSPlatform` guards.

### My optimization isn't showing up in the UI

Checklist:
- Is it a **nested public class** inside a category class?
- Does the category class implement `IOptimizationCategory`?
- Does the optimization class extend `BaseOptimization`?
- Does it have `[Optimization(Id = "...", ...)]` attribute?
- Are the localization keys added to `Translations.resx`?
- Has the optimization category been preloaded? (Check `OptimizationRegistry.IsPreloaded`)

### My customize setting isn't showing up

Same checks as above but for `ICustomizeCategory` / `BaseCustomizeSetting`.
- Does it have `[CustomizeSetting(Section = ..., Icon = ...)]`?
- Is the `Section` enum value correctly spelled?
- Does the category class use the correct `[CustomizeCategory(PageType = ...)]` attribute?

### No revert data file after testing

Tests that check revert data expect files in `%localappdata%\optimizerDuck\Revert\`. Test cleanup runs in `finally` blocks — make sure assertions run before cleanup.

### UI freezes when applying an optimization

Ensure your `ApplyAsync` uses `async`/`await` for any provider calls that are async (`ChangeServiceStartupTypeAsync`, `CMDAsync`, `PowerShellAsync`). If you're using `Task.FromResult` or blocking with `.Result` / `.Wait()`, the UI thread will freeze.

### How do I generate a GUID?

```powershell
# PowerShell
[guid]::NewGuid()
```

```bash
# Command line (if uuidgen is available)
uuidgen
```

### Translations showing as key names in the UI

You missed adding localization keys to `Translations.resx`. Check the [Localization](#localization) section for the expected key patterns.

### "No revert data" error when reverting

Check that the optimization's `Id` GUID hasn't changed. Revert files are keyed by `Id`. If you regenerate the GUID, previously applied optimizations won't have matching revert files.

### How do I add a new revert step type?

1. Create a new class in `Domain/Revert/Steps/` that implements `IRevertStep`
2. Add a static `FromData(JObject data)` method for deserialization
3. The `RevertManager`'s reflection-based `_stepRegistry` will auto-discover it
4. Record it via `ExecutionScope.RecordStep()` with your step as the `revertStep` parameter

### How does the app handle crash safety?

- Revert files use atomic writes (`.tmp` + `File.Replace`)
- Crash logging writes to `%localappdata%\optimizerDuck\Crashes\crash_*.log`
- `WmiHelper.Initialize()` at startup registers WMI cleanup for abnormal termination
- App.xaml.cs registers 3 global exception handlers

---

<div align="center">

## Credits

Contributors with merged PRs are listed in release notes. If you contribute significantly to a module, you can add an author tag at the top of the file header.

---

## License

By contributing to optimizerDuck, you agree that your contributions will be licensed under the project's [GPL v3 License](../LICENSE).

---

<p><i>Thanks for making optimizerDuck better.</i></p>

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
