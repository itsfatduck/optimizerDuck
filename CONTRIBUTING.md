<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

[Introduction](#introduction) • [Getting Started](#getting-started) • [Ways to Contribute](#ways-to-contribute) • [Detailed Contribution Guidelines](#detailed-contribution-guidelines) • [Coding Standards](#coding-standards) • [Localization Guidelines](#localization-guidelines) • [Pull Request Process](#pull-request-process) • [Credits](#credits) • [License](#license)

</div>

---

# Introduction

Thanks for contributing to **optimizerDuck**. It's a free, open-source Windows optimization tool built with WPF and .NET.

You can help in many ways:
- Reporting bugs
- Suggesting new features or optimizations
- Improving documentation
- Adding translations
- Contributing code and improvements

# Getting Started

### 1. Environment Setup

You need:
- **.NET 10 SDK** — download from [microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Windows 10/11** — the app runs as admin and makes system changes; it's Windows-only
- **IDE**: [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/) (with .NET desktop development workload), [JetBrains Rider](https://www.jetbrains.com/rider/download/), or VS Code with C# Dev Kit
- **Git** for version control

### 2. Fork and Clone

```bash
# Fork on GitHub first, then clone your fork
git clone https://github.com/<your-username>/optimizerDuck.git
cd optimizerDuck

# Add upstream remote to sync with the main repo
git remote add upstream https://github.com/itsfatduck/optimizerDuck.git
```

### 3. Restore and Build

The solution uses `.slnx` format (the new XML-based solution format).

```bash
# Restore dependencies
dotnet restore optimizerDuck.slnx

# Build (CI uses Release, Debug works too)
dotnet build optimizerDuck.slnx --configuration Release --no-restore

# Run tests (xUnit v3)
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build

# Run the app
dotnet run --project optimizerDuck/optimizerDuck.csproj

# Format code with CSharpier
dotnet csharpier .
```

> `--no-restore` and `--no-build` are optional locally, but CI uses them. If you add dependencies, run restore+no-restore.

### 4. Publishing

```batch
publish.bat portable      # Portable folder publish
publish.bat single        # Single-file executable
publish.bat single --skip-tests   # Skip tests for quick iteration
```

### 5. Project Structure

```
optimizerDuck/
├── optimizerDuck.slnx           # Solution file (.slnx format, not .sln)
├── app.manifest                 # requireAdministrator UAC level
├── publish.bat                  # Release publishing script
│
├── optimizerDuck/               # Main WPF app project
│   ├── App.xaml.cs              # DI registration, startup, theme, logging
│   ├── optimizerDuck.csproj     # TFM: net10.0-windows10.0.17763.0, UseWPF=true
│   │
│   ├── Domain/                  # Pure models, interfaces, attributes (no WPF deps)
│   │   ├── Abstractions/        # IOptimization, ICustomizeSetting, IRevertStep, etc.
│   │   ├── Attributes/          # [Optimization], [CustomizeSetting], [OptimizationCategory]
│   │   ├── Configuration/       # AppSettings model
│   │   ├── Execution/           # ExecutionScope — ambient step tracking via AsyncLocal
│   │   ├── Customize/           # Customize settings (Desktop, Gaming, Preferences, System)
│   │   │   ├── Categories/      # Category classes with nested setting classes
│   │   │   └── Models/          # BaseCustomizeSetting, RegistryToggle, SettingOption
│   │   ├── Optimizations/       # Optimizations (Performance, Privacy, GPU, etc.)
│   │   │   ├── Categories/      # Category classes with nested optimization classes
│   │   │   └── Models/          # BaseOptimization, ApplyResult, OptimizationContext
│   │   ├── Revert/              # RevertData, RevertResult, revert step types
│   │   │   └── Steps/           # RegistryRevertStep, ServiceRevertStep, etc.
│   │   └── UI/                  # Enums: OptimizationRisk, OptimizationTags, CategoryOrder
│   │
│   ├── Common/                  # Shared helpers, extensions, converters
│   │   ├── Extensions/
│   │   ├── Converters/
│   │   └── Helpers/             # Shared.cs, ReflectionHelper.cs, WmiHelper.cs
│   │
│   ├── Services/                # Business logic (not static — injected via DI)
│   │   ├── Configuration/       # ConfigManager, LanguageManager
│   │   ├── Customize/           # CustomizeRegistry (discovery via reflection)
│   │   ├── Managers/            # BloatwareService, DiskCleanupService, 
│   │   │                       # StartupManagerService, SystemInfoService, etc.
│   │   ├── Optimization/        # OptimizationRegistry, OptimizationService
│   │   │   └── Providers/       # Static providers: RegistryService, ShellService,
│   │   │                       # ScheduledTaskService, ServiceProcessService
│   │   ├── Revert/              # RevertManager (writes/reads revert JSON files)
│   │   └── UI/                  # ContentDialogService, etc.
│   │
│   ├── UI/                      # WPF pages, ViewModels, controls, styles
│   │   ├── Controls/            # Custom WPF controls
│   │   ├── Dialogs/             # Dialog windows
│   │   ├── Pages/               # App pages + sub-folders (Optimize/, Customize/)
│   │   ├── Styles/              # Fluent design styles
│   │   ├── ViewModels/          # Page and dialog ViewModels
│   │   └── Windows/             # MainWindow and custom windows
│   │
│   └── Resources/               # Images, embedded assets, localization
│       ├── Embedded/            # Embedded resources (icons, power plans)
│       ├── Images/              # Duck.png, logos
│       └── Languages/           # Translations.resx + locale variants
│
└── optimizerDuck.Test/          # xUnit v3 test project
    ├── Common/Helpers/          # SystemRefreshService tests
    ├── Domain/                  # BaseCustomizeSetting, PowerManagement tests
    │   ├── Customize/
    │   ├── Exceptions/
    │   ├── Optimizations/
    │   └── Revert/Steps/
    └── Services/                # RevertManager, OptimizationService, Provider tests
        ├── Managers/
        └── OptimizationServices/
```

---

<div align="center">

## Ways to Contribute

</div>

| Contribution Type | Description | Where to Look |
|---|---|---|
| **New Optimizations** | Registry tweaks, service changes, system tweaks | `Domain/Optimizations/Categories/*.cs` |
| **New Customize Settings** | UI toggles for Windows settings (like Game Mode, Mouse Acceleration) | `Domain/Customize/Categories/*.cs` |
| **New App Features** | New pages, tools, or functionality (Startup Manager, Disk Cleanup, etc.) | New services + ViewModels + Pages |
| **Improvements** | Performance, code cleanup, UI/UX improvements | Anywhere |
| **Bug Fixes** | Crash fixes, logic errors, UI display issues | Anywhere |
| **Translations** | New languages or fixing existing translations | `Resources/Languages/Translations.*.resx` |
| **Documentation** | README, CONTRIBUTING, code comments | `*.md` files |

---

<div align="center">

## Detailed Contribution Guidelines

</div>

### How Discovery Works (Read This First)

This project uses **reflection-based discovery** — you never register optimizations or customize settings manually. There's no DI registration array to update, no service collection to modify.

At startup:

1. `ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` scans all `optimizerDuck.*` assemblies
2. It finds every class that implements `IOptimizationCategory` or `ICustomizeCategory`
3. It then scans each category's **nested public classes** that implement `IOptimization` or `ICustomizeSetting`
4. All discovered items are instantiated automatically and `OwnerType` is assigned

**So your job is simple**: write a class that inherits from `BaseOptimization` or `BaseCustomizeSetting`, put it as a **nested class** inside a category class, and decorate it with the right attribute. Discovery handles the rest.

---

### Available Service Providers

When writing optimizations, use these **static** provider classes. They handle logging, error handling, and — most importantly — automatically record **revert steps** into the ambient `ExecutionScope` so changes can be undone.

| Service | Purpose | Key Methods |
|---|---|---|
| **`RegistryService`** | Read, write, delete registry keys. Backs up original values for revert. | `Write()`, `Read<T>()`, `DeleteValue()`, `CreateSubKey()`, `DeleteSubKeyTree()` |
| **`ShellService`** | Run CMD or PowerShell commands. | `CMDAsync()`, `PowerShellAsync()` |
| **`ScheduledTaskService`** | Create, disable, enable, or modify Windows Scheduled Tasks. | — |
| **`ServiceProcessService`** | Manage Windows Services (start, stop, change startup types). | — |

These are in `Services/Optimization/Providers/`. They are **static classes** — you don't inject them. They capture revert steps through `ExecutionScope.Current`.

---

### Creating a New Optimization

#### 1. Understand the Pattern

Optimizations live in `Domain/Optimizations/Categories/`. Each category is a class implementing `IOptimizationCategory`, and each optimization inside it is a **nested class** inheriting from `BaseOptimization`.

Current categories:
- `Performance.cs`
- `SecurityAndPrivacy.cs`
- `Gpu.cs`
- `PowerManagement.cs`
- `BloatwareAndServices.cs`
- `UserExperience.cs`

#### 2. Add to an Existing Category

Add a nested class inside the category file:

```csharp
[OptimizationCategory(typeof(PerformanceOptimizerPage))]
public class Performance : IOptimizationCategory
{
    public string Name => Loc.Instance[$"Optimizer.{nameof(Performance)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.Performance;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(
        Id = "a1b2c3d4-...",                              // Generate a NEW GUID
        Risk = OptimizationRisk.Safe,                       // Safe / Moderate / Risky
        Tags = OptimizationTags.Performance                 // Flags — combine with |
    )]
    public class MyNewTweak : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context)
        {
            // Your tweak logic here — use RegistryService, ShellService, etc.
            RegistryService.Write(new RegistryItem(
                @"HKLM\SOFTWARE\Something", "ValueName", 1));

            // Return result from the ambient ExecutionScope
            return Task.FromResult(CompleteFromScope());
        }
    }
}
```

Key points:
- `Id` must be a **new GUID** — it's used for revert file naming and applied-state tracking
- Inherit from `BaseOptimization` — it provides `Name`, `ShortDescription`, `Prefix`, `RiskVisual`, `TagDisplays` from the attribute + localization keys
- Implement `ApplyAsync()`. Use the provider services directly. Call `CompleteFromScope()` at the end to derive the result from recorded steps
- Do **not** catch all exceptions — let them bubble up. `ExecutionScope` tracks successes and failures via `RecordStep()`
- Use `progress.Report()` to update the UI during execution

#### 3. Create a New Category

Only if your tweaks don't fit any existing category. Avoid hyper-specific categories (don't create "NVIDIA" — use "GPU").

1. Create `Domain/Optimizations/Categories/YourCategory.cs`
2. Implement `IOptimizationCategory`
3. Apply `[OptimizationCategory(PageType = typeof(YourPage))]` — you'll also need an XAML page for it
4. Add a member to `OptimizationCategoryOrder` enum in `Domain/UI/OptimizationCategoryOrder.cs`
5. Create the corresponding XAML page and register in `App.xaml.cs`

#### 4. Localization Keys

Each optimization implicitly expects these localization keys in `Translations.resx` (convention-based):

```
Optimizer.{CategoryName}.{OptimizationKey}.Name
Optimizer.{CategoryName}.{OptimizationKey}.ShortDescription
Optimizer.{CategoryName}.{OptimizationKey}.Progress.{...}
Optimizer.{CategoryName}.{OptimizationKey}.Error.{...}
```

Where `CategoryName` is the category class name (e.g., `Performance`) and `OptimizationKey` is the nested class name.

> Missing keys return the key string itself — add them in `Translations.resx`!

---

### Creating a New Customize Setting

Customize settings are UI toggles that flip Windows settings ON or OFF. They live in `Domain/Customize/Categories/`.

Current categories:
- `Desktop.cs`
- `Gaming.cs`
- `Preferences.cs`
- `SystemFeatures.cs`

#### Simple Registry Toggle

```csharp
[CustomizeCategory(PageType = typeof(DesktopFeatureCategory))]
public class Desktop : ICustomizeCategory
{
    private enum Sections { Desktop, Taskbar, Widgets }

    public string Name => Loc.Instance[$"Customize.{nameof(Desktop)}.Name"];
    public string Description => Loc.Instance[$"Customize.{nameof(Desktop)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.Desktop24;
    public CustomizeOrder Order { get; init; } = CustomizeOrder.Desktop;
    public ObservableCollection<ICustomizeSetting> Features { get; init; } = [];

    [CustomizeSetting(
        Section = nameof(Sections.Taskbar),
        Icon = SymbolRegular.AlignCenter24,
        Recommendation = RecommendationState.On
    )]
    public class TaskbarAlignment : BaseCustomizeSetting
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "TaskbarAl",
                    OnValue = 0,
                    OffValue = 1,
                    DefaultValue = 1,
                },
            ];
    }
}
```

Key points:
- `Section` is an enum member — it groups settings in the UI
- `Icon` is from `Wpf.Ui.Controls.SymbolRegular`
- `Recommendation`: `On`, `Off`, `Depends`, or `None`
- `RegistryToggle` handles both read (via `GetStateAsync` base implementation) and write (via `ApplyAsync` base implementation)
- Mark toggle as `IsOptional = true` if it's not required for state detection

#### Custom Logic (Override GetStateAsync / ApplyAsync)

For settings that aren't simple registry toggles (e.g., mouse acceleration combines 3 values):

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
        await ExecutePostActionAsync();
    }
}
```

#### Localization Keys for Customize Settings

```
Customize.{CategoryName}.{SettingKey}.Name
Customize.{CategoryName}.{SettingKey}.Description
Customize.{CategoryName}.{SettingKey}.Options.{OptionKey}    (if using SettingOption)
Customize.{CategoryName}.{SettingKey}.Recommendation.Reason   (if Recommendation != None)
Customize.{CategoryName}.Section.{SectionName}                (for section headers)
```

---

### Building New App Functionality

If you want to add a new page or tool (e.g., a "Startup Manager"):

1. **Open a GitHub Issue first** — describe the feature, use case, and design. Wait for feedback.
2. **Implementation order**:

```csharp
// 1. Service layer in Services/Managers/YourService.cs
public class YourService { ... }

// 2. ViewModel in UI/ViewModels/Pages/YourViewModel.cs
//    Extends CommunityToolkit.Mvvm.ObservableObject

// 3. XAML Page in UI/Pages/YourPage.xaml (+ code-behind)

// 4. Register in App.xaml.cs
services.AddSingleton<YourViewModel>();
services.AddSingleton<YourPage>();
```

- ViewModels and Pages must be registered as **singletons** in `App.xaml.cs`
- Navigation is handled by WPF UI (`INavigationService`)
- Follow the same pattern as existing pages (check `DashboardPage`, `OptimizePage`, etc.)

---

### About the Revert System

Every applied optimization creates a JSON file at `%localappdata%\optimizerDuck\Revert\{optimizationId}.json`.

- **Applied state** is inferred from file presence on disk (`RevertManager.IsAppliedAsync(id)`)
- **File format**: `RevertData` with schema version + ordered array of `RevertStepData`
- **Atomic writes**: `RevertManager` writes to `.tmp` then calls `File.Replace()` — crash-safe
- **Step types**: `RegistryRevertStep`, `ServiceRevertStep`, `ScheduledTaskRevertStep`, `ShellRevertStep`, `UsbPowerRevertStep`
- **`ExecutionScope`**: Uses `AsyncLocal<ExecutionScope?>` for ambient step tracking. Provider services record steps via `ExecutionScope.RecordStep()`. No need to pass context through parameters

> **Important**: When you call `RegistryService.Write()`, `ShellService.CMD()`, etc., revert steps are automatically recorded. You don't need to manually create revert steps.

---

### Bug Fixes and Improvements

- Reproduce the bug first, then fix it
- For optimization code: handle errors gracefully — return `ApplyResult.False("reason")` or let `ExecutionScope` track failures
- Don't throw uncaught exceptions in optimization code unless it's a programming error
- Run `dotnet test` before submitting

---

### Translations (Localization)

#### RESX Files

All user-facing strings live in `Resources/Languages/Translations.resx`. Use the strongly-typed `Translations` class in C#, or `Loc.Instance["Key"]` for dynamic lookup.

- **Do not edit** `Translations.Designer.cs` directly — it's auto-generated
- Use [ResXManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager) (VS) or Rider's built-in resource editor
- To add a language: create `Translations.{locale}.resx` (e.g., `Translations.vi-VN.resx`)
- Preserve format parameters like `{0}`, `{1}` exactly
- Keep strings concise — some UI cards have width limits

#### Registering a New Language

In `SettingsViewModel.cs`, add the language to the `Languages` collection:

```csharp
new() { DisplayName = "Tiếng Việt", Culture = new CultureInfo("vi-VN") },
```

#### Hardcoded String Rule

**Never hardcode strings**. Always use:

```csharp
// Strongly typed (recommended)
string title = Translations.Features_Desktop_Name;

// With format args
string msg = string.Format(Translations.Dashboard_SystemInfo_Storage_DiskInfo, used, total, percent);

// Dynamic key lookup
string title = Loc.Instance[$"Features.{category}.Name"];
```

In XAML:

```xaml
<!-- Without args -->
<ui:TextBlock Text="{ext:Loc Dashboard.Header.Title}" />

<!-- With bound args -->
<ui:TextBlock Text="{ext:Loc Dashboard.UpdateInfoBar.Message, {Binding ViewModel.LatestVersion}}" />
```

---

<div align="center">

## Coding Standards

</div>

We keep things modern and consistent.

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
| Private fields | `_camelCase` | `_registryService` |
| Local variables, parameters | `camelCase` | `progress`, `context` |
| Public constants | `PascalCase` | `MaxRetries` |
| Private constants | `_PascalCase` | `_defaultTimeout` |

### Formatting

- **4-space indentation** (no tabs)
- **CSharpier** is the formatter. Run `dotnet csharpier .` before committing, or configure your IDE to format on save with CSharpier
- The `.editorconfig` has `CA1416` (platform compatibility) silenced — all code is Windows-only

### Dependency Injection

- Services, ViewModels, and Pages are registered in `App.xaml.cs` as singletons
- Use constructor injection: `public class Foo(Bar bar, Baz baz)`
- Static provider services (`RegistryService`, `ShellService`, etc.) are not injected — they're accessed directly and use `ExecutionScope.Current` for ambient tracking
- Test doubles are hand-written (no mocking libraries)

### Error Handling

- In optimizations: prefer returning `ApplyResult.False("message")` over throwing
- In services: use try/catch around system calls and log
- Let `ExecutionScope` handle step-level failure tracking
- Don't catch exceptions you can't handle

---

<div align="center">

## Testing Guidelines

</div>

Tests use **xUnit v3** and follow an integration-style approach with real I/O.

### Test Patterns

- **No mocking libraries** — all test doubles are hand-written classes implementing interfaces directly
- **Real I/O**: tests use the real filesystem (revert JSON files), real registry (`HKCU\Software\TestOptimizerDuck*`), real process execution
- **Cleanup**: use `try/finally` or `IDisposable` to clean up artifacts (revert files, registry keys)
- **Naming**: `{Method}_{Scenario}_{ExpectedResult}` — e.g., `ApplyAsync_Success_PersistsRevertDataFile`
- **Logging**: Use `NullLogger<T>.Instance` / `NullLoggerFactory.Instance` for DI logging parameters

### Structuring Tests

```
optimizerDuck.Test/
├── Domain/
│   ├── Customize/BaseCustomizeSettingTests.cs
│   ├── Exceptions/StepExecutionExceptionTests.cs
│   ├── Optimizations/
│   │   ├── PowerManagementTests.cs
│   │   └── Models/Services/RegistryItemKindDetectionTests.cs
│   └── Revert/Steps/
│       ├── ScheduledTaskRevertStepTests.cs
│       └── RevertStepSerializationTests.cs
└── Services/
    ├── Managers/RevertManagerTests.cs
    ├── OptimizationServiceTests.cs
    ├── OptimizationServiceIntegrationTests.cs
    ├── OptimizationExecutionContextTests.cs
    ├── OptimizationServices/
    │   ├── RegistryServiceTests.cs
    │   ├── ShellServiceTests.cs
    │   └── ShellPolicyTests.cs
    ├── RegistryWatcherTests.cs
    ├── SystemInfoServiceTests.cs
    └── Common/Helpers/SystemRefreshServiceTests.cs
```

### Running Tests

```bash
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build
# or, after building:
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj
```

---

<div align="center">

## Pull Request Process

</div>

1. **Branch from `master`** — never work directly on master:
   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/issue-number
   ```

2. **Commit with Conventional Commits**:
   - `feat:` — new optimizations or features
   - `fix:` — bug fixes
   - `refactor:` — code restructuring
   - `docs:` — documentation updates
   - `test:` — adding/fixing tests
   - `i18n:` — translation updates
   - `chore:` — maintenance tasks

3. **Before pushing, verify**:
   ```bash
   dotnet build optimizerDuck.slnx --configuration Release
   dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build
   dotnet csharpier .   # Format code
   ```

4. **Open the PR**:
   - Describe **what** changed and **why**
   - If your PR has UI changes, **include a screenshot**
   - Link related issues: `Closes #42`
   - Mark as draft if it's still a work in progress

5. **Review**: A maintainer will review. Be open to feedback.

---

<div align="center">

## Issue Guidelines

</div>

- **Bug reports**: Use the Bug Report template. Include steps to reproduce, expected vs actual behavior, logs from `%localappdata%\optimizerDuck\optimizerDuck.log`, and system specs.
- **Feature requests**: Describe the use case, the problem it solves, and how it should work.
- **Questions**: Use GitHub Discussions or join our [Discord server](https://discord.gg/tDUBDCYw9Q).

---

<div align="center">

## Credits

</div>

Contributors with merged PRs are listed in release notes. If you contribute significantly to a module, you can add an author tag at the top of the file header.

---

<div align="center">

## License

</div>

By contributing to optimizerDuck, you agree that your contributions will be licensed under the project's [GPL v3 License](../LICENSE).

---

<div align="center">
  <p><i>Thanks for making optimizerDuck better.</i></p>

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
