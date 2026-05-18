<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

[Introduction](#introduction) • [Getting Started](#getting-started) • [Ways to Contribute](#ways-to-contribute) • [Detailed Contribution Guidelines](#detailed-contribution-guidelines) • [Coding Standards](#coding-standards) • [Localization Guidelines](#localization-guidelines) • [Pull Request Process](#pull-request-process) • [Credits](#credits) • [License](#license)

</div>

---

# Introduction

Thank you for your interest in contributing to **optimizerDuck**!

This project helps users optimize and customize Windows in a simple, transparent, and powerful way. Community contributions make the project better, more stable, and accessible to more people.

There are many ways you can contribute:

- Reporting bugs
- Suggesting new features or optimizations
- Improving documentation
- Adding translations
- Contributing code and improvements

Every contribution, big or small, is appreciated.

# Getting Started

### 1. Environment Setup

Before you begin, ensure your development environment is ready. You will need:

- **.NET 10 SDK**: Download from [microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0)
- IDE: [**Visual Studio 2026**](https://visualstudio.microsoft.com/downloads/) (with .NET desktop development workload) or [**JetBrains Rider**](https://www.jetbrains.com/rider/download/). VS Code is also supported with the C# Dev Kit extension.
- [**Git**](https://git-scm.com/install/windows): For version control.
- Windows 10/11 environment for testing natively.

### 2. Fork and Clone

1. Fork this repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/<your-username>/optimizerDuck.git
   cd optimizerDuck
   ```
3. Add the upstream remote to sync changes easily:
   ```bash
   git remote add upstream https://github.com/itsfatduck/optimizerDuck.git
   ```

### 3. Project Initialization

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build optimizerDuck.slnx

# Run tests
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj
```

### 4. Project Structure Overview

```text
optimizerDuck/
├── Common/           # Shared utilities, extensions, and helpers
├── Domain/           # Models, interfaces, attributes (no UI dependencies)
│   ├── Abstractions/ # IOptimization, IFeature, IRevertStep, etc.
│   ├── Attributes/   # [Optimization], [Feature], [OptimizationCategory], etc.
│   ├── Configuration/# AppSettings model
│   ├── Execution/    # ExecutionScope for step tracking
│   ├── Features/     # Feature categories (Desktop, Gaming, Preferences, System)
│   │   ├── Categories/
│   │   └── Models/   # BaseFeature, RegistryToggle
│   ├── Optimizations/# Optimization categories (Performance, Privacy, GPU, etc.)
│   │   ├── Categories/
│   │   └── Models/   # BaseOptimization, ApplyResult, OptimizationContext
│   ├── Revert/       # Revert step types (Registry, Service, ScheduledTask, Shell)
│   └── UI/           # Enums, risk levels, tags, order enums, LanguageOption
├── Services/         # Business logic and system operations
│   ├── Configuration/# ConfigManager, LanguageManager
│   ├── Features/     # BloatwareService, DiskCleanupService, StartupManagerService
│   ├── Managers/     # FeatureManager, OptimizationManager
│   ├── Optimization/ # Service providers (Registry, Shell, ScheduledTask, ServiceProcess)
│   └── Revert/       # RevertManager
├── UI/               # WPF pages, ViewModels, dialogs, styles
│   ├── Controls/     # Custom WPF controls
│   ├── Dialogs/      # Dialog windows
│   ├── Pages/        # App pages (Dashboard, Optimize, Settings, etc.)
│   ├── Styles/       # Fluent design styles
│   ├── ViewModels/   # Page and dialog ViewModels
│   └── Windows/      # Main window
└── Resources/        # Images, localization (.resx files)
```

---

<div align="center">

## Ways to Contribute

</div>

| Contribution Type             | Description                                          |                      Example                       |
| :---------------------------- | :--------------------------------------------------- | :------------------------------------------------: |
| 🆕 **New Optimizations**      | Implement Windows system tweaks                      | GPU/Network/Privacy tweaks, registry modifications |
| 🌟 **New Features**           | Add feature toggles to `Domain/Features/Categories/` |          UI toggles for Windows settings           |
| ✨ **Core App Functionality** | Expand core app functionality                        |       Add new pages, integrations, or tools        |
| ⚡ **Improvements**           | Optimize code and boost performance                  |  Improve scan speed, enhance UI/UX, refactor code  |
| 🐛 **Bug Fixes**              | Resolve code or logic errors                         |     Fix app crashes, correct UI display issues     |
| 🌐 **Translations**           | Translate the app into new languages                 |  Contribute a new language (`.resx`) or fix typos  |
| 📚 **Documentation**          | Update README or write instructions                  |   Write user guides, document APIs, improve docs   |

---

<div align="center">
  <h2>Detailed Contribution Guidelines</h2>
</div>

### Available Service Providers

When writing new optimizations or features, **always use the provided service providers** instead of making direct system calls. These wrapper classes are located in `Services/Optimization/Providers/` and handle logging, privilege management, and **Revert** tracking automatically.

| Service                     | Purpose                                                                                                                           |
| :-------------------------- | :-------------------------------------------------------------------------------------------------------------------------------- |
| **`RegistryService`**       | Read, write, and delete registry keys (`HKLM`, `HKCU`). Automatically records original values so users can safely revert changes. |
| **`ShellService`**          | Run PowerShell or CMD commands. Provides `CMDAsync()` and `PowerShellAsync()` methods.                                            |
| **`ScheduledTaskService`**  | Create, disable, enable, or modify Windows Scheduled Tasks.                                                                       |
| **`ServiceProcessService`** | Manage Windows Services: start, stop, change startup types.                                                                       |

### Creating a New Optimization

Optimizations live in `Domain/Optimizations/Categories/`. Each optimization is a nested class inside a category file that performs a system tweak and returns an `ApplyResult`.

1. **Review existing optimizations**: Check `Domain/Optimizations/Categories/` for the six existing categories (`Performance.cs`, `SecurityAndPrivacy.cs`, `Gpu.cs`, `PowerManagement.cs`, `BloatwareAndServices.cs`, `UserExperience.cs`).
2. **Choose the right category**: If your tweak fits an existing category, add it there as a nested class.
3. **Add to an existing category**:
   - Create a nested class inside the category that inherits from `BaseOptimization` (located in `Domain/Optimizations/Models/BaseOptimization.cs`).
   - Add the `[Optimization]` attribute with a newly generated GUID (`Id`), a `Risk` level (`OptimizationRisk.Safe`, `Moderate`, or `Risky`), and appropriate `Tags` (`OptimizationTags` flags).
   - Implement `ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)` using the service providers. Return `ApplyResult.True()` on success or `ApplyResult.False("error message")` on failure.
4. **Create a new category** (only if necessary):
   - Only create a new category if the tweaks represent a major, distinct area. Avoid hyper-specific categories (e.g., do not create an "NVIDIA" category; use "GPU" instead).
   - Create a new file in `Domain/Optimizations/Categories/` that implements `IOptimizationCategory`.
   - Apply the `[OptimizationCategory]` attribute with the page type.
   - Add a new enum member to `OptimizationCategoryOrder` in `Domain/UI/OptimizationCategoryOrder.cs`.
   - Register the page type in `UI/Pages/Optimize/Categories/OptimizationPages.cs`.

**Example:**

```csharp
public class Performance : IOptimizationCategory
{
    public string Name => "Performance";
    public OptimizationCategoryOrder Order => OptimizationCategoryOrder.Performance;
    public ObservableCollection<IOptimization> Optimizations { get; } = [];

    [Optimization(Id = "a1b2c3d4-...", Risk = OptimizationRisk.Safe, Tags = OptimizationTags.Performance)]
    public class MyNewTweak : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context)
        {
            // Use RegistryService, ShellService, etc.
            return Task.FromResult(ApplyResult.True());
        }
    }
}
```

### Creating a New Feature

Features live in `Domain/Features/Categories/`. Features are UI toggles that flip Windows settings ON or OFF.

1. **Review existing categories**: Check `Domain/Features/Categories/` for the four existing categories (`Desktop.cs`, `SystemFeatures.cs`, `Preferences.cs`, `Gaming.cs`).
2. **Add to an existing category**:
   - Create a nested class inside the category that inherits from `BaseFeature` (located in `Domain/Features/Models/BaseFeature.cs`).
   - Add the `[Feature]` attribute with a `Section` name and an `Icon` (from Wpf.Ui `SymbolRegular`).
   - For simple registry toggles, override `RegistryToggles` with a collection of `RegistryToggle` objects.
   - For complex logic, override `EnableAsync()`, `DisableAsync()`, and `GetStateAsync()`.
3. **Create a new category** (only if necessary):
   - Create a new file in `Domain/Features/Categories/` that implements `IFeatureCategory`.
   - Apply the `[FeatureCategory]` attribute.
   - Add a new enum member to `FeatureCategoryOrder` in `Domain/UI/FeatureCategoryOrder.cs`.
   - Register the page type in `UI/Pages/Features/Categories/FeatureCategoryPages.cs`.

**Example (simple registry toggle):**

```csharp
[Feature(Section = "Taskbar", Icon = SymbolRegular.AlignCenter24)]
public class TaskbarAlignment : BaseFeature
{
    protected override IEnumerable<RegistryToggle> RegistryToggles =>
    [
        new RegistryToggle(RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
            "TaskbarAl", 0, 1)
    ];
}
```

**Example (complex logic):**

```csharp
[Feature(Section = "Gaming", Icon = SymbolRegular.Games24)]
public class GameMode : BaseFeature
{
    public override Task<bool> GetStateAsync()
    {
        // Custom state detection logic
    }

    public override Task EnableAsync()
    {
        // Custom enable logic
    }

    public override Task DisableAsync()
    {
        // Custom disable logic
    }
}
```

### Building New App Functionality

If you want to build entirely new functionality (e.g., a new tool like "Startup Manager"):

1. **Discuss first**: Open a GitHub Issue describing the feature, its use case, and proposed design. Wait for maintainer feedback before heavy coding.
2. **Implementation**:
   - **Service**: Create business logic in `Services/`. Register it in `App.xaml.cs` via DI.
   - **ViewModel**: Create in `UI/ViewModels/Pages/`. Inherit from `ViewModel` (in `UI/ViewModels/ViewModel.cs`).
   - **View**: Create a `.xaml` page in `UI/Pages/`.
   - **Routing**: Register the ViewModel and Page as singletons in `App.xaml.cs`. Add to the NavigationView menu if appropriate.

### Bug Fixes and Improvements

- Reproduce the bug first, then write the fix.
- Use standard error handling: wrap risky calls and return `ApplyResult.False("reason")` rather than throwing uncaught exceptions.
- If modifying performance-critical code, try to profile the change to verify the improvement.
- Run `dotnet test` before submitting to ensure existing tests still pass.

### Translations

optimizerDuck supports full localization via `.resx` resource files.

1. Do not edit `Translations.Designer.cs` directly.
2. Use [ResXManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager) in Visual Studio, or Rider's built-in resource manager, to edit `Resources/Languages/Translations.resx`.
3. To add a **new language**, use the tool to create a new culture-specific `.resx` file (e.g., `Translations.fr-FR.resx`).
4. Translate all values while preserving format parameters (e.g., `{0}`, `{1}`).
5. Keep strings concise. Some UI cards have strict width constraints.
6. After adding a new `.resx` file, register the language in `SettingsViewModel.cs`:

   ```csharp
   public ObservableCollection<LanguageOption> Languages { get; } =
   [
       new() { DisplayName = "English", Culture = new CultureInfo("en-US") },
       // ...
       new() { DisplayName = "Français", Culture = new CultureInfo("fr-FR") },
   ];
   ```

---

<div align="center">

## Localization Guidelines

</div>

**Never hardcode strings** in UI or services. All display text, logs, and dialogs must use the resource manager.

- **In C# code**:

  ```csharp
  using optimizerDuck.Resources.Languages;

  // Direct key access
  string title = Translations.Features_Desktop_Name;

  // With string formatting
  string message = string.Format(Translations.Dashboard_SystemInfo_Storage_DiskInfo, 50.5, 100.0, 49.5);

  // Dynamic key lookup via Loc instance
  string title = Loc.Instance[$"Features.{category}.Name"];
  string message = string.Format(Loc.Instance[$"Dashboard.SystemInfo.Storage.{drive}.DiskInfo"], 50.5, 100.0, 49.5);
  ```

- **In XAML views** (using `LocExtension`):

  ```xaml
  <!-- Without arguments -->
  <ui:TextBlock Text="{ext:Loc Dashboard.Header.Title}" />

  <!-- With bound arguments -->
  <ui:TextBlock Text="{ext:Loc Dashboard.UpdateInfoBar.Message, {Binding ViewModel.LatestVersion}}" />
  ```

---

<div align="center">

## Coding Standards

</div>

We maintain a modern, clean C# codebase.

### Code Style and Formatting

- **Modern C#**: Use C# 10+ features such as file-scoped namespaces, collection expressions (`[]`), primary constructors, and implicit usings where configured.
- **Nullability**: Nullable reference types are enabled (`<Nullable>enable</Nullable>`). Handle null instances carefully with `?` or null-forgiving operators only when certain.
- **Dependency Injection**: Inject services and ViewModels through DI. Avoid instantiating services with `new`.
- **Indentation**: 4 spaces.
- **Formatting**: The project uses [CSharpier](https://csharpier.com/) for code formatting. Run `dotnet csharpier .` before committing, or let your IDE handle it.

### Naming Conventions

| Element                                         | Convention    | Example                         |
| :---------------------------------------------- | :------------ | :------------------------------ |
| Classes, enums, interfaces, methods, properties | `PascalCase`  | `RegistryService`, `ApplyAsync` |
| Private fields                                  | `_camelCase`  | `_registryService`              |
| Local variables, parameters                     | `camelCase`   | `progress`, `context`           |
| Public constants                                | `PascalCase`  | `MaxRetries`                    |
| Private constants                               | `_PascalCase` | `_defaultTimeout`               |

### Error Handling

- Prefer returning `ApplyResult.False("message")` over throwing exceptions in optimization code.
- Catch and log exceptions at the service level.
- Use `try/catch` blocks around system calls that may fail (registry access, process execution).

---

<div align="center">

## Pull Request Process

</div>

1. **Create a branch** from `master`. Never work directly on `master`.
   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/issue-123
   ```
2. **Commit using Conventional Commits**:
   - `feat:` new capabilities or optimizations
   - `fix:` bug resolutions
   - `refactor:` code restructuring without behavior changes
   - `docs:` documentation updates
   - `test:` adding or fixing tests
   - `i18n:` translation updates
   - `chore:` maintenance tasks
3. **Push and open a PR**:
   - Describe **what** changed and **why**.
   - If your PR affects the UI, **include a screenshot or GIF**.
   - Link related issues (`Closes #42`).
4. **Code review**: A maintainer will review your changes. Be open to feedback and ready to make adjustments.

---

<div align="center">

## Issue Guidelines

</div>

- **Bug reports**: Use the Bug Report template. Include steps to reproduce, expected vs. actual behavior, logs (found in `%localappdata%\optimizerDuck\optimizerDuck.log`), and your system specs.
- **Feature requests**: Describe the use case. What problem does this solve? How should it work? The more detail, the better.
- **Questions**: Use GitHub Discussions or join our [Discord server](https://discord.gg/tDUBDCYw9Q).

---

<div align="center">

## Credits

</div>

Contributors who have merged PRs are listed in release notes and on the repository. If you contribute significantly to a specific feature or optimization module, you may add an author tag at the top of the file header.

---

<div align="center">

## License

</div>

By contributing to optimizerDuck, you agree that your contributions will be licensed under the project's [GPL v3 License](../LICENSE).

---

<div align="center">
  <p><i>Thank you for helping make optimizerDuck better for the Windows community!</i></p>

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
