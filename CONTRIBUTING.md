<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

**English** | [日本語](.github/contributing/CONTRIBUTING.ja-JP.md) | [Türkçe](.github/contributing/CONTRIBUTING.tr-TR.md)

[Introduction](#introduction) • [Getting Started](#getting-started) • [Architecture Overview](#architecture-overview) • [Ways to Contribute](#ways-to-contribute) • [Creating an Optimization](#creating-an-optimization) • [Creating a Customize Setting](#creating-a-customize-setting) • [The Condition System](#the-condition-system) • [The Refresh Scope System](#the-refresh-scope-system) • [Building New Features](#building-new-features) • [Revert System](#revert-system) • [Testing](#testing) • [Coding Standards](#coding-standards) • [Localization](#localization) • [Pull Request Process](#pull-request-process) • [Issue Guidelines](#issue-guidelines) • [FAQ & Troubleshooting](#faq--troubleshooting) • [License](#license)

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
- Writing or reviewing tests

> **New here?** Start with [Getting Started](#getting-started), then read [Architecture Overview](#architecture-overview). The two most common code contributions are [Creating an Optimization](#creating-an-optimization) and [Creating a Customize Setting](#creating-a-customize-setting).

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

# Run the app (needs an elevated prompt — it modifies system settings)
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

`publish.bat` runs the test suite first (unless `--skip-tests` is passed), then calls `dotnet publish` with the chosen profile (`Portable` or `Single`).

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
│   │   ├── Abstractions/                   # IOptimization, ICustomizeSetting, IRevertStep, IWindow,
│   │   │                                   #   ICustomizeCategory, IOptimizationCategory
│   │   ├── Attributes/                     # [Optimization], [CustomizeSetting],
│   │   │                                   #   [OptimizationCategory], [CustomizeCategory]
│   │   ├── Conditions/                     # Compatibility condition system (see "The Condition System")
│   │   │   ├── BuiltIn/                    # Ready-made conditions (Windows version, GPU/CPU brand,
│   │   │   │                               #   min RAM, registry key / service existence, ...)
│   │   │   ├── ICondition.cs               # Condition contract
│   │   │   ├── ConditionBase.cs            # Shared helpers (e.g. OS build parsing)
│   │   │   ├── ConditionResult.cs          # Available / Unsupported / Error outcome
│   │   │   ├── ConditionState.cs           # Outcome enum
│   │   │   ├── ConditionValidation.cs      # Discovery-time metadata validation
│   │   │   └── WindowsBuilds.cs            # OS build number constants
│   │   ├── Configuration/                  # AppSettings model
│   │   ├── Exceptions/                     # StepExecutionException
│   │   ├── Execution/                      # ExecutionScope — ambient step tracking via AsyncLocal
│   │   ├── Customize/                      # Customize settings
│   │   │   ├── Categories/                 # Category classes with nested setting classes
│   │   │   └── Models/                     # BaseCustomizeSetting, RegistryToggle, RegistryBinding,
│   │   │                                   #   CustomizeRefreshScope, SettingOption,
│   │   │                                   #   CustomizeControlType, RecommendationState, ...
│   │   ├── Optimizations/                  # Optimizations
│   │   │   ├── Categories/                 # Category classes with nested optimization classes
│   │   │   └── Models/                     # BaseOptimization, ApplyResult, OptimizationContext,
│   │   │       ├── Bloatware/              # AppXPackage model for preinstalled apps
│   │   │       ├── Cleanup/                # CleanupItem for disk cleanup
│   │   │       ├── ScheduledTask/          # ScheduledTaskModel
│   │   │       ├── Services/               # RegistryItem, ServiceItem, ShellResult, ServiceStartupType
│   │   │       └── StartupManager/         # StartupApp, StartupTask models
│   │   ├── Revert/                         # RevertData, RevertResult, revert step types
│   │   │   └── Steps/                      # RegistryRevertStep, ServiceRevertStep,
│   │   │                                   #   ScheduledTaskRevertStep, ShellRevertStep, UsbPowerRevertStep
│   │   └── UI/                             # Enums: OptimizationRisk, OptimizationTags,
│   │                                       #   OptimizationCategoryOrder, CustomizeOrder,
│   │                                       #   LanguageOption, OptimizationState, RiskVisual,
│   │                                       #   ProcessingProgress, ...
│   │
│   ├── Common/                             # Shared helpers, extensions, converters
│   │   ├── Converters/                     # WPF value converters (BooleanToVisibility, MBToGB, ...)
│   │   ├── Extensions/                     # StringExtensions, page-registry extensions,
│   │   │                                   #   LanguageExtensions
│   │   └── Helpers/                        # Shared.cs, ReflectionHelper.cs, SystemRefreshService.cs,
│   │                                       #   EmbeddedResourceHelper.cs, WmiHelper.cs,
│   │                                       #   GitHubSourceHelper.cs, ThemeResource.cs, ...
│   │
│   ├── Services/                           # Business logic layer
│   │   ├── Conditions/                     # ConditionEvaluator (static evaluation entry point)
│   │   ├── Configuration/                  # ConfigManager, LanguageManager
│   │   ├── Customize/                      # CustomizeRegistry (reflection-based discovery)
│   │   ├── Optimization/                   # OptimizationRegistry, OptimizationService
│   │   │   └── Providers/                  # Static: RegistryService, ShellService (+ ShellPolicy),
│   │   │                                   #   ScheduledTaskService, ServiceProcessService
│   │   ├── Revert/                         # RevertManager (atomic write/read of revert JSON files)
│   │   ├── System/                         # RegistryWatcher (+ IRegistryWatcher), SystemInfoService,
│   │   │                                   #   StreamService, UpdaterService, CrossPageEventBus
│   │   └── UI/                             # BloatwareService, DiskCleanupService, StartupManagerService
│   │
│   ├── UI/                                 # WPF pages, ViewModels, controls, styles
│   │   ├── Behaviors/                      # SmoothScrollBehavior
│   │   ├── Controls/                       # FilledNavigationViewItem, EmptyBadge
│   │   ├── Dialogs/                        # ProcessingDialog, OptimizationDetailsDialog,
│   │   │                                   #   OptimizationResultDialog, RestorePointDialog, LegalDialog,
│   │   │                                   #   BloatwareConfirmationDialog, ScheduledTask dialogs, ...
│   │   ├── Pages/                          # Dashboard, Optimize, Customize, Settings, Bloatware,
│   │   │   ├── Customize/                  # CustomizePage + Categories/ (auto-registered pages)
│   │   │   ├── Optimize/                   # OptimizePage + Categories/ (auto-registered pages)
│   │   │   ├── DiskCleanupPage
│   │   │   ├── StartupManagerPage
│   │   │   └── ScheduledTasksPage
│   │   ├── Styles/                         # FluentDesign.xaml, NavigationViewOverride.xaml, ToolTipOverride.xaml
│   │   ├── ViewModels/                     # Page, dialog and window ViewModels
│   │   └── Windows/                        # MainWindow
│   │
│   └── Resources/                          # Images, embedded assets, localization
│       ├── Embedded/                       # Icons/ and PowerPlans/ (optimizerDuck.pow)
│       ├── Images/                         # Duck.png, GitHub logos, Discord logo
│       └── Languages/                      # Translations.resx (default) + locale variants
│
└── optimizerDuck.Test/                     # xUnit v3 test project (InternalsVisibleTo)
```

> **Don't rely on the tree above as a hard reference.** It's a map, not a specification — folders and files evolve. When in doubt, look at the actual folders. See the [Project Structure](#project-structure) note at the end of this section.

### Key Design Decisions

| Decision | Rationale |
|---|---|
| **Reflection-based discovery** | No DI registration arrays to update. `ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` scans `optimizerDuck.*` assemblies. New optimizations/settings are auto-discovered. |
| **Static provider services** | `RegistryService`, `ShellService`, `ScheduledTaskService`, `ServiceProcessService` are static classes. They record revert steps into the ambient `ExecutionScope` — no need to inject or pass context. |
| **File-based revert tracking** | Applied state = file exists on disk (`%localappdata%\optimizerDuck\Revert\{id}.json`). No database. Atomic writes via `File.Replace()`. |
| **Condition system (fail-open)** | Optimizations and settings can declare compatibility conditions. Evaluation failures never hide an item — see [The Condition System](#the-condition-system). |
| **Integration-style tests** | Real filesystem, real registry (under `HKCU\Software\TestOptimizerDuck*`), real process execution. No mocking libraries — hand-written test doubles only. |
| **Async service methods** | Provider methods that run external processes are async (`*Async` suffix). Optimization `ApplyAsync` methods should use `async`/`await` to keep the UI responsive. |
| **Static WMI helper** | `WmiHelper.Initialize()` runs at startup to register WMI cleanup handlers for abnormal process termination. |
| **Pending changes tracking** | `App.HasPendingChanges` tracks whether applied optimizations haven't been reverted. The app warns on close with options to restart PC/Explorer or exit. |

### Project Structure

The authoritative structure lives in three places, all of which should be kept in sync when you move or rename things:

1. **Folders on disk** — `optimizerDuck/` (the app) and `optimizerDuck.Test/` (tests). There are no other top-level project directories.
2. **`optimizerDuck.csproj`** — embedded resources, images, package references.
3. **`App.xaml.cs`** — DI registrations and startup sequence.

Do **not** create top-level directories outside these two project folders.

---

# Ways to Contribute

| Contribution Type | Description | Where to Start |
|---|---|---|
| **New Optimizations** | Registry tweaks, service changes, system tweaks | `Domain/Optimizations/Categories/*.cs` |
| **New Customize Settings** | UI toggles for Windows settings (Game Mode, Mouse Acceleration, Taskbar, etc.) | `Domain/Customize/Categories/*.cs` |
| **New Conditions** | Compatibility gates for optimizations/settings (Windows version, hardware, ...) | `Domain/Conditions/` |
| **New App Features** | New pages, tools, or functionality | Open an issue first |
| **Bug Fixes** | Crash fixes, logic errors, UI issues | Anywhere |
| **Translations** | New languages or fixing existing translations | `Resources/Languages/Translations.*.resx` |
| **Documentation** | README, CONTRIBUTING, etc. | `*.md` files |
| **Testing** | Adding/reviewing tests for existing or new optimizations | `optimizerDuck.Test/` |

---

# Creating an Optimization

### How Discovery Works

At startup the app calls `OptimizationRegistry.PreloadOptimizationsAsync()`. This runs reflection work on a background thread:

1. `ReflectionHelper.FindImplementationsInLoadedAssemblies<IOptimizationCategory>()` finds every category class.
2. For each category it scans **nested public classes** implementing `IOptimization`.
3. Each optimization is instantiated, `OwnerType` is assigned, and its `[Optimization]` metadata (including any `Condition`) is validated.
4. `OptimizationService.UpdateOptimizationStateAsync` scans revert files on disk to mark each optimization as Applied or not.
5. The Optimize page calls `EnsurePreloadedAsync()` before binding (a no-op if preloading already finished).

**Your job**: Create a nested class inside a category, extend `BaseOptimization`, decorate with `[Optimization]`. That's it — no registration to update.

### Optimization Categories

Categories live in `Domain/Optimizations/Categories/`, one file per category. The exact set changes over time — look at that folder for the authoritative list. As of writing, categories include:

| File | Focus |
|---|---|
| `Performance.cs` | RAM tuning, process priority, keyboard latency, multimedia scheduler, accessibility hotkeys |
| `SecurityAndPrivacy.cs` | Telemetry, error reporting, advertising ID, location, Copilot, activity history, delivery optimization, etc. |
| `Gpu.cs` | AMD/NVIDIA/Intel registry tweaks, power states, clock gating, ASPM, async flips |
| `PowerManagement.cs` | Hibernation, fast startup, USB selective suspend, custom power plan installation |
| `BloatwareAndServices.cs` | OEM preinstalled app blocking, Windows service startup type optimization |
| `UserExperience.cs` | Menu delays, visual effects, taskbar animations, transparency, Start Menu web search |
| `AI.cs` | Windows AI features (Recall, Click To Do) |

Each category class carries a `[OptimizationCategory(typeof(SomePage))]` attribute that links it to its UI page.

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
        Tags = OptimizationTags.Performance,            // Flags — combine with |
        Condition = typeof(Windows11Condition)          // Optional (see "The Condition System")
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
| **Extend `BaseOptimization`** | Provides `Name`, `ShortDescription`, `RiskVisual`, `TagDisplays` from attribute + localization keys. |
| **`OwnerType` is assigned automatically** | Discovery sets it — don't set it yourself. |
| **Use `async Task<ApplyResult>`** | Service providers are async — `await` them to keep the UI responsive. |
| **Return `CompleteFromScope()`** | Derives `ApplyResult` from steps recorded in the ambient `ExecutionScope`. Don't construct `ApplyResult` manually. |
| **Report progress** | Use `progress.Report(new ProcessingProgress { ... })` to update the UI dialog. |
| **Don't catch all exceptions** | Let them bubble up. `ExecutionScope` tracks success/failure; `OptimizationService` handles exceptions. |
| **Don't manually create revert steps** | Static provider services do this automatically via `ExecutionScope.RecordStep()`. |
| **Use `context.Logger`** | The optimization context provides a logger for important diagnostic info. |
| **Use `context.Snapshot`** | `OptimizationContext.Snapshot` (a `SystemSnapshot`) gives system info: RAM, GPU, CPU, OS. Use it for conditional logic. |
| **Use `context.StreamService`** | For optimizations that need to download remote resources (e.g. power plans). |
| **Declare a `Condition` if needed** | Gate the optimization on Windows version or hardware — see [The Condition System](#the-condition-system). |

### Available Service Providers

These **static** classes handle logging, error handling, and automatic revert step recording.

| Service | Key Methods | Why It's Used |
|---|---|---|
| **`RegistryService`** | `Write()`, `Read<T>()`, `DeleteValue()`, `CreateSubKey()`, `DeleteSubKeyTree()`, `KeyExists()`, `CleanupEmptyKeys()` | Read/write/delete registry keys. Backs up original values for revert. Supports batch writes via params array. |
| **`ShellService`** | `CMDAsync()`, `PowerShellAsync()`, `CMD()` (sync), `PowerShell()` (sync) | Run CMD or PowerShell commands. Prefer async variants. Optional `revertCommand` parameter for undo. See `ShellPolicy` for non-standard exit codes. |
| **`ScheduledTaskService`** | `DisableTask()`, `EnableTask()`, `IsTaskEnabled()`, `DeleteTask()`, `GetAllTasks()`, `RegisterTask()`, `RunTask()`, `StopTask()` | Manage Windows Scheduled Tasks. |
| **`ServiceProcessService`** | `ChangeServiceStartupTypeAsync()`, `GetStartupTypeAsync()` | Manage Windows Services. Always use async variants. Supports batch changes via params array. |

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

// Async shell command with revert command
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

Not all optimizations need `async`/`await`. If your optimization only does synchronous registry writes (no async calls), return `Task.FromResult()`:

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

1. Create `Domain/Optimizations/Categories/YourCategory.cs`.
2. Implement `IOptimizationCategory`.
3. Apply `[OptimizationCategory(typeof(YourPage))]` — you'll also need a XAML page (see [Building New Features](#building-new-features)).
4. Add a member to the `OptimizationCategoryOrder` enum in `Domain/UI/OptimizationCategoryOrder.cs` so the category sorts correctly.
5. The XAML page auto-registers via `services.AddAllOptimizationPages()` in `App.xaml.cs`.

### Create an Optimization Helper Base Class

If several optimizations share the same structure (like GPU tweaks that iterate over detected GPUs), create an abstract intermediate class:

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

See `Domain/Optimizations/Categories/Gpu.cs` for a real example with AMD, NVIDIA, and Intel subclasses.

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

Categories live in `Domain/Customize/Categories/`, one file per category — look there for the authoritative list. As of writing:

| File | Focus |
|---|---|
| `Desktop.cs` | Desktop icons (This PC, Recycle Bin, Network, User Files, Control Panel), global show/hide icons, shortcut arrow visibility |
| `Preferences.cs` | Taskbar alignment, widgets, task view button, end task, dark mode, file extensions, hidden files, clipboard history, search mode, seconds in clock, Bing search, classic context menu |
| `Gaming.cs` | Game Mode, Game Bar, background recording, mouse acceleration, fullscreen optimizations, GPU scheduling |
| `SystemFeatures.cs` | Num Lock on boot, Developer Mode, long paths, battery percentage |

Each category class carries a `[CustomizeCategory(PageType = typeof(SomePage))]` attribute linking it to its UI page.

### Step-by-Step: Simple Registry Toggle

For a simple on/off registry toggle, the base class does all the work:

```csharp
private enum Sections { Taskbar, Widgets, Advanced }

[CustomizeSetting(
    Section = nameof(Sections.Taskbar),        // Groups settings in the UI
    Icon = SymbolRegular.AlignCenter24,         // From Wpf.Ui.Controls.SymbolRegular
    Recommendation = RecommendationState.On,    // On / Off / Depends / Experimental / None
    Condition = typeof(Windows11Condition)      // Optional compatibility condition
)]
public class TaskbarAlignment : BaseCustomizeSetting
{
    protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "TaskbarAl",
                OnValues = [0],       // value(s) when toggle is ON
                OffValues = [1],      // value(s) when toggle is OFF
                DefaultValue = 1,     // value = default state (used when key missing)
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
| `OnValues` | `IReadOnlyList<object?>` | `[1]` | Values representing the "on" state. `null` in the list means "key absent = on". |
| `OffValues` | `IReadOnlyList<object?>` | `[0]` | Values representing the "off" state. `null` in the list means "key absent = off". |
| `DefaultValue` | `object?` | `0` | Default state value when the key is missing (used for Reset to Default). |
| `IsOptional` | `bool` | `false` | If `true`, not required for state detection. |
| `ValueKind` | `RegistryValueKind` | `DWord` | Registry value type (DWord, String, etc.). |

**State detection logic**: `GetState()` (in `BaseCustomizeSetting`) collects all non-optional `RegistryToggles` and returns `true` only when **every** required toggle matches one of its `OnValues`.

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

Dropdown options declare a `RegistryBinding` so the base class can auto-read the current value and auto-write on selection. Use the `Option()` helper:

```csharp
[CustomizeSetting(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.AlignCenter24)]
public class TaskbarAlignment : BaseCustomizeSetting
{
    private const string RegPath =
        @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string RegName = "TaskbarAl";

    public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;

    // Option(key, regPath, regName, value) reads its label from
    //   Customize.{Category}.{Feature}.Options.{key}
    protected override IReadOnlyList<SettingOption>? GetOptions() =>
        [Option("Center", RegPath, RegName, 1), Option("Left", RegPath, RegName, 0)];

    protected override CustomizeRefreshScope RefreshScope =>
        CustomizeRefreshScope.TaskbarSettings;
}
```

For options that touch **multiple** registry values, pass explicit bindings:

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

The base class reads the current value from the bindings, shows a memory-only **"Custom"** (or **"Not set"**) fallback when the live value matches no declared option, and writes all bindings of the selected option. You don't need a custom `ApplyAsync` for the common case.

### Dynamic Options (Platform-Aware)

You can conditionally show options based on the Windows version:

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

        if (NeedsPostAction)
            await ExecutePostActionAsync();
    }

    protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Default;
}
```

> When you override `ApplyAsync`, you **must** call `await ExecutePostActionAsync()` yourself (guarded by `NeedsPostAction`). The base class only does this automatically for the default `RegistryToggles`-based and dropdown-binding-based paths.

### State Detection with Retry

After applying a value, the UI calls `GetStateWithRetryAsync()` (not `GetStateAsync()`). This method:

1. Reads state up to `maxRetries` (default 3) times with `delayMs` (default 80ms) between attempts.
2. Returns when two consecutive reads agree on the same value (convergence check).
3. Falls back to the last read value after exhausting retries.

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
[CustomizeSetting(..., Recommendation = RecommendationState.On)]
// Available: On, Off, Depends, Experimental, None
```

- **`On`**: Recommended to turn ON — improves system
- **`Off`**: Recommended to turn OFF — improves system
- **`Depends`**: Depends on user's specific needs/configuration
- **`Experimental`**: May be unstable, use with caution
- **`None`** (default): No recommendation displayed

Add an optional reason via localization key: `Customize.{Category}.{Feature}.Recommendation.Reason`.

### What to Override per Pattern

| Scenario | Override |
|---|---|
| Simple registry toggle | `RegistryToggles` + `RefreshScope` |
| Multiple registry toggles (e.g., Game Mode: 2 values) | `RegistryToggles` (list them all) |
| Dropdown / options | `ControlType` → `Dropdown` + `GetOptions()` with `Option(...)` bindings |
| Multi-value logic (e.g., mouse accel: 3 registry values) | `GetStateAsync()` + `ApplyAsync()` + `GetWatchedRegistryPaths()` |
| Setting with no registry interaction | `GetStateAsync()` + `ApplyAsync()` (full custom) |
| Custom refresh behavior | `RefreshScope` (flags only) or `ExecutePostActionAsync()` (full override) |
| State detection with convergence | `GetStateWithRetryAsync()` (built-in — don't override) |
| Dynamic options per Windows version | Override `GetOptions()` with conditional logic |
| Embedded resource extraction | `EmbeddedResourceHelper.TryExtract()` in custom `ApplyAsync` |
| Compatibility gate | `Condition = typeof(...)` on the `[CustomizeSetting]` attribute |

### Create a New Category

1. Create `Domain/Customize/Categories/YourCategory.cs`.
2. Implement `ICustomizeCategory` with `[CustomizeCategory(PageType = typeof(YourPage))]`.
3. Add a member to the `CustomizeOrder` enum in `Domain/UI/CustomizeOrder.cs`.
4. Create the XAML page (a new class in `UI/Pages/Customize/Categories/`).
5. The page auto-registers via `services.AddAllCustomizeCategoryPages()` in `App.xaml.cs`.

### Localization Keys for Customize Settings

```
Customize.{CategoryName}.{SettingKey}.Name
Customize.{CategoryName}.{SettingKey}.Description
Customize.{CategoryName}.{SettingKey}.Options.{OptionKey}    (if using SettingOption)
Customize.{CategoryName}.{SettingKey}.Recommendation.Reason   (if Recommendation != None)
Customize.{CategoryName}.Section.{SectionName}                (for section headers)
```

---

# The Condition System

Conditions let you gate an optimization or customize setting behind a compatibility check, so the UI can tell the user "this isn't supported on your system" instead of applying something that won't work.

### Core Concepts

Conditions live in `Domain/Conditions/` and are evaluated by the static `ConditionEvaluator` in `Services/Conditions/`.

| Piece | Purpose |
|---|---|
| `ICondition` | The contract: `ConditionResult Evaluate(SystemSnapshot snapshot)`. Implementations need a public parameterless constructor (they're instantiated via reflection). |
| `ConditionBase` | Optional base class with shared helpers (e.g., `TryGetOsBuild` for parsing the OS build number). |
| `ConditionResult` | Outcome: `Available`, `Unsupported(title, description)`, or `Error()`. Localized text is resolved lazily via providers. |
| `ConditionState` | `Available`, `Unsupported`, `Error`. |
| `ConditionValidation` | Validates `Condition = typeof(...)` metadata at discovery time so misconfigurations fail fast at startup. |
| `WindowsBuilds` | OS build-number constants (e.g. `Windows11`, `Windows11_24H2`). |

**Fail-open principle**: only `Unsupported` blocks an item, and only when it isn't already applied (or hidden by the user). `Error` and an unpopulated snapshot never hide anything — incomplete hardware detection must not remove options the user could still use.

### Declaring a Condition

Both attributes accept an optional `Condition`:

```csharp
// Optimization
[Optimization(Id = "...", Risk = OptimizationRisk.Safe,
    Tags = OptimizationTags.Privacy | OptimizationTags.System,
    Condition = typeof(Windows11_24H2OrGreaterCondition))]
public class DisableRecall : BaseOptimization { ... }

// Customize setting
[CustomizeSetting(Section = ..., Icon = SymbolRegular.Grid24,
    Condition = typeof(Windows11Condition))]
public class TaskbarWidgets : BaseCustomizeSetting { ... }
```

### Built-In Conditions

Ready-made conditions live in `Domain/Conditions/BuiltIn/` — check that folder for the full, up-to-date list. Examples include:

| Condition | Matches |
|---|---|
| `Windows10Condition` / `Windows11Condition` | OS version by build number |
| `Windows11_24H2OrGreaterCondition` | Windows 11 24H2 (build 26100) or later |
| `CpuBrandCondition` / `GpuBrandCondition` | CPU/GPU vendor (Intel, AMD, NVIDIA) |
| `MinimumRamCondition` (base) / `SixteenGbRamCondition` | Minimum installed RAM |
| `RegistryKeyExistsCondition` | A registry key exists |
| `ServiceExistsCondition` | A Windows service exists |
| `RecallInstalledCondition` | Windows Recall is present |

### Writing a Custom Condition

```csharp
public sealed class MyCondition : ConditionBase
{
    public override ConditionResult Evaluate(SystemSnapshot snapshot)
    {
        // ConditionBase.TryGetOsBuild parses "22631.xxxx" -> 22631
        if (TryGetOsBuild(snapshot, out var build) && build >= 22000)
            return ConditionResult.Available;

        return ConditionResult.Unsupported(
            () => Loc.Instance["Condition.MyCondition.Title"],
            () => Loc.Instance["Condition.MyCondition.Description"]);
    }
}
```

Guidelines:

- **Return `Available`** when the system passes; **`Unsupported(title, description)`** when it doesn't.
- Use `ConditionResult.Error()` (or throw — `ConditionEvaluator` catches and maps to `Error`) for unexpected failures. Errors don't block.
- Put user-facing text behind localization providers (`() => Loc.Instance[...]`) so the current culture wins at read time.
- Give the class a **public parameterless constructor** (add none, or an explicit empty one).

### How It's Evaluated

1. Discovery calls `ConditionValidation.Validate(...)` to confirm the declared type implements `ICondition` and is constructible.
2. The UI calls `ConditionEvaluator.Evaluate(conditionType, snapshot, logger)`, which caches condition instances and never throws.
3. Items with an `Unsupported` result are shown in a blocked/unsupported state (the user can hide it for the session). Items already applied are never re-blocked.

---

# The Refresh Scope System

When a customize setting changes state, different Windows surfaces need different refresh strategies. The `CustomizeRefreshScope` `[Flags]` enum controls this granularly.

### Available Flags

| Member | Value | Effect | P/Invoke |
|---|---|---|---|
| `None` | `0` | No refresh | — |
| `Settings` | `1 << 0` | Broadcast `WM_SETTINGCHANGE` so apps re-read registry | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE)` |
| `Associations` | `1 << 1` | Notify shell that file associations or icon cache changed | `SHChangeNotify(SHCNE_ASSOCCHANGED)` |
| `Desktop` | `1 << 2` | Force desktop icon list (`SysListView32`) to repaint | `LVM_REFRESH` + `LVM_UPDATE` |
| `Taskbar` | `1 << 3` | Broadcast taskbar-targeted `WM_SETTINGCHANGE` ("TraySettings") | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, "TraySettings")` |
| `PolicyUpdate` | `1 << 4` | Push `SystemParametersInfo` with `SPIF_SENDCHANGE` for per-user params | `SystemParametersInfo` |
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
  ├─ Writes RegistryToggles (if any), or applies the selected option's bindings
  ├─ Checks NeedsPostAction (true if RefreshScope != None)
  └─ Task.Run → ExecutePostActionAsync()
       ├─ Checks each CustomizeRefreshScope flag
       ├─ Calls SystemRefreshService methods (P/Invoke)
       └─ Win32 notifications sent to Windows
```

If you override `ApplyAsync`, you **must** call `await ExecutePostActionAsync()` yourself (see the examples above). The base class only does this automatically when using the default `RegistryToggles`-based or dropdown-binding-based apply.

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

- ViewModels and Pages **must** be registered as singletons in `App.xaml.cs`.
- Navigation is handled by WPF UI (`INavigationService`).
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

> This is a snapshot for orientation — `App.xaml.cs` is the source of truth for the current registrations. Also note the startup calls: `ShellService.Init(appOptionsMonitor)` and `WmiHelper.Initialize()`.

### System Services Reference

| Service | Purpose |
|---|---|
| `SystemInfoService` | Provides the `SystemSnapshot` (CPU, RAM, GPU, OS, disk) used by `OptimizationContext` and the condition system. |
| `StreamService` | Downloads remote resources (e.g., updated power plan files). Used via `OptimizationContext.StreamService`. |
| `UpdaterService` | Checks GitHub releases for updates. Shows update prompt on Dashboard. |
| `RegistryWatcher` | Monitors registry keys for external changes and notifies the UI to refresh. Implements `IRegistryWatcher`. |
| `BloatwareService` | Lists preinstalled AppX packages, categorizes them as Safe/Caution/Dangerous. |
| `DiskCleanupService` | Scans disks for cleanup opportunities (temp files, caches, logs). |
| `StartupManagerService` | Lists and manages startup applications and scheduled tasks. |
| `ConditionEvaluator` | Static entry point for evaluating compatibility conditions (see [The Condition System](#the-condition-system)). |

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

### Scope Variants

| Method | Purpose |
|---|---|
| `ExecutionScope.Begin(optimization, logger)` | Creates a persistable scope for a real apply. |
| `ExecutionScope.BeginForLogging(logger)` | Logging only — records steps but never persists revert data. |
| `ExecutionScope.BeginForCapture(logger)` | For retry: captures steps with `OptimizationId = Guid.Empty`, later re-assigned to the real scope. |

### Step Types

| Step Type | Records | Automatically Created By |
|---|---|---|
| **`RegistryRevertStep`** | Original registry value before change | `RegistryService.Write()`, `DeleteValue()`, `CreateSubKey()`, `DeleteSubKeyTree()` |
| **`ServiceRevertStep`** | Original service startup type | `ServiceProcessService.ChangeServiceStartupTypeAsync()` |
| **`ScheduledTaskRevertStep`** | Original task state (enabled/disabled) | `ScheduledTaskService.DisableTask()`, `EnableTask()` |
| **`ShellRevertStep`** | Shell command to reverse the change | `ShellService.CMDAsync()`, `PowerShellAsync()` — pass a `revertCommand` parameter |
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
    { "Index": 0, "Type": "Registry", "Data": { "..." } },
    null,                    // null gap = failed step at this index
    { "Index": 2, "Type": "Service", "Data": { "..." } }
  ]
}
```

### Key Details

- **Applied state** is inferred from file presence on disk (`RevertManager.IsAppliedAsync(id)`).
- **Atomic writes**: writes to `.tmp` then `File.Replace()` — crash-safe.
- **Concurrent access**: per-file `SemaphoreSlim` locks prevent race conditions; 30-second timeout.
- **`ExecutionScope`** uses `AsyncLocal<ExecutionScope?>` for ambient step tracking. No need to pass context through parameters.
- **Revert executes steps in reverse order** (last applied = first reverted).
- **Partial success**: revert continues even if some steps fail. Failed steps get retry actions recorded.
- **Retry**: `OptimizationService.RetryFailedStepsAsync()` can retry individual failed steps; `RecordStepAtIndex()` preserves the original index layout.
- **Upsert**: `RevertManager.UpsertRevertStepAtIndexAsync()` can add/replace revert steps at specific indices (used during retry).
- **Step registry**: Revert step deserialization uses reflection-based `_stepRegistry` — new step types auto-register by implementing `IRevertStep` with a static `FromData(JObject)` method.

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
| **STA thread** | Tests involving WPF components must use `RunInStaThreadAsync` helper (STA thread + `TaskCompletionSource`) |

### Test Structure

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

Mirror the app's structure: tests for `Services/OptimizationServices/` go in `Services/OptimizationServices/`, tests for domain models go in the matching `Domain/` subdirectory.

### Running Tests

```bash
# After building
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build

# Build + test in one step
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release

# Run a single test by name
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build --filter "FullyQualifiedName~TestName"
```

### CI Integration

The CI pipeline (`.github/workflows/ci.yml`) runs:

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

# Coding Standards

### Language Features

| Feature | Used? | Notes |
|---|---|---|
| File-scoped namespaces | Yes | `namespace X.Y;` |
| Collection expressions | Yes | `[]` for empty, `[item1, item2]` for lists |
| Primary constructors | Yes | Used in services and simple types |
| Implicit usings | Yes | Enabled in `.csproj` |
| Nullable reference types | Yes | `<Nullable>enable</Nullable>` — handle nulls properly |
| Extension methods (`extension(T type)`) | Yes | C# 13 feature, used in `OptimizationTagsToDisplay` |

### Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Classes, enums, interfaces, methods, properties | `PascalCase` | `RegistryService`, `ApplyAsync` |
| Private fields | `_camelCase` | `_lastError` |
| Local variables, parameters | `camelCase` | `progress`, `serviceName` |
| Async methods | `*Async` suffix | `ChangeServiceStartupTypeAsync`, `CMDAsync` |
| Constants | `PascalCase` / `_PascalCase` | `MaxRetries` / `_defaultTimeout` |

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
- **Prefer existing libraries** over new dependencies.
- **Prefer small, focused changes** over large refactors.
- **Never commit machine-specific paths or secrets.**

### Dependency Injection

- Services, ViewModels, and Pages are registered as singletons in `App.xaml.cs`.
- Use constructor injection: `public class Foo(Bar bar, Baz baz)` or `public class Foo(ILogger<Foo> logger)`.
- Static provider services (`RegistryService`, `ShellService`, `ScheduledTaskService`, `ServiceProcessService`) are NOT injected — access them directly.
- Test doubles are hand-written (no mocking libraries).

### Error Handling

| Layer | Practice |
|---|---|
| **Optimizations** | Return `ApplyResult.False("reason")` instead of throwing. Let `ExecutionScope` handle step-level failure tracking. |
| **Provider services** | Use try/catch around system calls, log errors. Record failed steps with retry actions. |
| **ViewModels** | Catch exceptions in command handlers, show user-friendly snackbars. |
| **Conditions** | Return `ConditionResult.Error()` or throw — `ConditionEvaluator` catches and maps to `Error` (which never blocks). |
| **Don't** | Catch exceptions you can't handle. Don't silently swallow all exceptions. |

### Global Error Handling

`App.xaml.cs` registers three global exception handlers:

- `AppDomain.CurrentDomain.UnhandledException` — catches fatal exceptions
- `TaskScheduler.UnobservedTaskException` — catches unobserved task exceptions
- `DispatcherUnhandledException` — catches unhandled UI thread exceptions

All crash details are logged to `%localappdata%\optimizerDuck\Crashes\crash_*.log`.

---

# Localization

### RESX Files

All user-facing strings live in `Resources/Languages/Translations.resx` (the English default). Use the strongly-typed `Translations` class in C#, or `Loc.Instance["Key"]` for dynamic lookup.

- **Do not edit** `Translations.Designer.cs` directly — it's auto-generated.
- Use [ResXManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager) (VS) or Rider's built-in resource editor.
- Preserve format parameters like `{0}`, `{1}` exactly.
- Keep strings concise — some UI cards have width limits.

### Available Locales

The app ships with **more than 15 languages**, and the list keeps growing. Instead of listing them here (which would go stale), check:

- **The locale files themselves**: `optimizerDuck/Resources/Languages/` — one `Translations.{locale}.resx` per language, plus `Translations.resx` as the English default.
- **The registration list**: `Languages` in `UI/ViewModels/Pages/SettingsViewModel.cs` — this is the authoritative list of languages shown in the UI.

### Adding a New Language

1. Create `Translations.{locale}.resx` (e.g., `Translations.de-DE.resx`) with all the same keys as `Translations.resx`.
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
   - Describe **what** changed and **why**.
   - If your PR has UI changes, **include a screenshot**.
   - Link related issues: `Closes #42`.
   - Mark as draft if still a work in progress.

5. **Review**: A maintainer will review. Be open to feedback and respond promptly.

### PR Checklist

- [ ] Code follows existing patterns (discovery, attributes, async naming)
- [ ] Localization keys added to `Translations.resx` at minimum
- [ ] Conditions declared where relevant (see [The Condition System](#the-condition-system))
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
- Does it have an `[Optimization(Id = "...", ...)]` attribute?
- Are the localization keys added to `Translations.resx`?
- Has the optimization category been preloaded? (Check `OptimizationRegistry.IsPreloaded`)
- Is a `Condition` blocking it? (See the next question)

### My optimization/setting shows as "unsupported" (blocked)

- Check the `Condition = typeof(...)` declared on the attribute.
- Confirm the condition type implements `ICondition`, is concrete, and has a public parameterless constructor (`ConditionValidation` enforces this at startup).
- Remember the item only blocks when **not** already applied; applied items always show their normal card.

### My customize setting isn't showing up

- Does it have `[CustomizeSetting(Section = ..., Icon = ...)]`? (`Icon` is required.)
- Is the `Section` value correctly spelled?
- Does the category class use the correct `[CustomizeCategory(PageType = ...)]` attribute?
- Is a `Condition` blocking it?

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

1. Create a new class in `Domain/Revert/Steps/` that implements `IRevertStep`.
2. Add a static `FromData(JObject data)` method for deserialization.
3. The `RevertManager`'s reflection-based `_stepRegistry` will auto-discover it.
4. Record it via `ExecutionScope.RecordStep()` with your step as the `revertStep` parameter.

### How does the app handle crash safety?

- Revert files use atomic writes (`.tmp` + `File.Replace`).
- Crash logging writes to `%localappdata%\optimizerDuck\Crashes\crash_*.log`.
- `WmiHelper.Initialize()` at startup registers WMI cleanup for abnormal termination.
- `App.xaml.cs` registers 3 global exception handlers.

---

<div align="center">

## Credits

Contributors with merged PRs are listed in release notes. If you contribute significantly to a module, you can add an author tag at the top of the file header.

---

## License

By contributing to optimizerDuck, you agree that your contributions will be licensed under the project's [GPL v3 License](./LICENSE).

---

<p><i>Thanks for making optimizerDuck better.</i></p>

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
