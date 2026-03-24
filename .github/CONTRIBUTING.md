<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

[Introduction](#introduction) • [Getting Started](#getting-started) • [Ways to Contribute](#ways-to-contribute) • [Detailed Contribution Guidelines](#detailed-contribution-guidelines) • [Coding Standards](#-coding-standards) • [Localization Guidelines](#-localization-guidelines) • [Pull Request Process](#-pull-request-process) • [Credits](#-credits) • [License](#-license)

</div>

---

# Introduction

First of all, thank you for your interest in contributing to **optimizerDuck**!

This project is built to help users customize and optimize Windows in a simple, transparent, and powerful way. Contributions from the community are valuable in making the project better, more stable, and accessible to more people around the world.

There are many ways you can contribute:

- Reporting bugs
- Suggesting new features
- Improving documentation
- Adding translations
- Contributing code and improvements

Every contribution, big or small, is greatly appreciated. Thank you for helping make optimizerDuck better!

# Getting Started

### 1. Environment Setup

Before you begin, ensure your development environment is ready. You will need:

- **.NET 10 SDK**: Download from [microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0)
- IDE: [**Visual Studio 2026**](https://visualstudio.microsoft.com/downloads/) or [**JetBrains Rider**](https://www.jetbrains.com/rider/download/) with .NET desktop development workloads installed. VS Code is also supported with the C# Dev Kit.
- [**Git**](https://git-scm.com/install/windows): For version control.
- Windows 10/11 environment for testing natively.

### 2. Fork and Clone

1. Fork this repository on GitHub
2. Clone your fork locally using terminal/PowerShell:
   ```bash
   git clone https://github.com/itsfatduck/optimizerDuck.git
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

---

<div align="center">

## Ways to Contribute

</div>

We welcome a wide variety of contributions. Here is an overview of how you can help:

| Contribution Type             | Description                              |                       Example                       |
| :---------------------------- | :--------------------------------------- | :-------------------------------------------------: |
| 🆕 **New Optimizations**      | Implement Windows system tweaks          | GPU/Network/Privacy tweaks, optimizing the registry |
| 🌟 **New Features**           | Extend app capability in `Core/Features` |     Adding new UI toggles for Windows features      |
| ✨ **Core App Functionality** | Expand core app functionality            |   Add entirely new pages, integrations, or tools    |
| ⚡ **Improvements**           | Optimize code and boost performance      |  Improve scan speed, enhance UI/UX, optimize code   |
| 🐛 **Bug Fixes**              | Resolve code or logic errors             |     Fix app crashes, correct UI display issues      |
| 🌐 **Translations**           | Translate the app into new languages     |  Contribute a new language (`.resx`) or fix typos   |
| 📚 **Documentation**          | Update README or write instructions      |   Write user guides, document APIs, improve docs    |

---

<div align="center">
  <h2>Detailed Contribution Guidelines</h2>
</div>

### 🛠️ Available Services for Operations

When writing new optimizations or features, DO NOT perform direct modifications without using our provided services. These services are wrapper classes located in `Services/Optimization/Providers/` that automatically handle logging, privilege management, and crucial **Revert** tracking.

Here are the main services you should use:

- **`RegistryService`**: For reading, writing, and deleting registry keys (`HKLM`, `HKCU`). Automatically records original states so users can safely revert changes.
- **`ShellService`**: For running PowerShell or CMD commands seamlessly.
- **`ScheduledTaskService`**: For creating, disabling, or modifying Windows Scheduled Tasks.
- **`ServiceProcessService`**: For managing Windows Services (starting, stopping, changing startup types).

### 🆕 Creating a New Optimization

Optimizations reside in `Domain/Optimizations/Categories/`. They perform direct system tweaks and return an `ApplyResult`.

1. **Review Existing Services**: Ensure you know how to use `RegistryService` or `ShellService` to apply your tweak.
2. **Select the Target Category**:
   - Check existing categories in `Domain/Optimizations/Categories/` (e.g., `Performance.cs`, `SecurityAndPrivacy.cs`).
   - If your tweak fits locally within an existing category, add it as a nested class.
3. **Adding to an Existing Category**:
   - Create a class inside the category inheriting from `BaseOptimization`.
   - Add the `[Optimization]` attribute with a strict, newly generated GUID (`Id`), a `Risk` level, and appropriate `Tags`.
   - Implement the `ApplyAsync()` method using the provided services.
4. **Creating a New Category**:
   - Only create a new category if the tweaks represent a major, distinct area of optimization. Avoid creating hyper-specific categories (e.g., do NOT create an "NVIDIA" category; use a general "Performance" or "GPU" category instead).
   - Create a new file, implement `IOptimizationCategory` and apply the `[OptimizationCategory]` attribute.
   - Update the `OptimizationCategoryOrder` in `Domain/UI/OptimizationCategoryOrder.cs` to include your new category enum.

### 🌟 Creating a New Feature

Features reside in `Domain/Features/Categories/`. Features are typically UI toggles that flip Windows settings ON or OFF dynamically.

1. **Select the Target Feature Category**:
   - Check existing Feature Categories in `Domain/Features/Categories/` (e.g., `Desktop.cs`, `SystemFeatures.cs`).
2. **Adding to an Existing Feature Category**:
   - Create a nested class inheriting from `BaseFeature`.
   - Add the `[Feature]` attribute with a designated `Section` and `Icon` (from Wpf.Ui `SymbolRegular`).
   - You can override `RegistryToggles` directly if your feature is just a simple registry flip. Alternatively, override `EnableAsync()`, `DisableAsync()`, and `GetStateAsync()` for complex logic.
3. **Creating a New Feature Category**:
   - Similar to Optimizations, only create a new category file for a significantly different set of features.
   - Implement `IFeatureCategory` and register its enum in `Domain/UI/FeatureCategoryOrder.cs`.

### ✨ Building Completely New App Functionality

If you want to build entirely new functionality (e.g., a completely new tool like "Startup Manager" instead of a small tweak):

1. **Discuss First**: Open a GitHub Issue and label it as an enhancement. Provide mockups, descriptions, and the problems it solves. Wait for maintainers' thoughts before coding heavily.
2. **Implementation Strategy**:
   - **View**: Create a `.xaml` page in `UI/Pages/`.
   - **ViewModel**: Create a `ViewModel` in `UI/ViewModels/Pages/`. Inherit from `ViewModel` in `UI/ViewModels/ViewModel.cs`.
   - **Service**: Inject any needed new Business logic services into the ViewModel.
   - **Routing**: Ensure it is registered in `App.xaml.cs` dependency injection and added to the Navigation menu if appropriate.

### 🐛 Bug Fixes & ⚡ Improvements

- Review the code, reproduce the bug, and write the fix.
- Ensure all logic uses standard error handling (wrapping risky calls and returning `ApplyResult.False("Reason")` rather than throwing uncaught exceptions).
- If modifying performance, try to profile the change to prove the performance gain.

### 🌐 Translations

optimizerDuck supports full localization!

1. Do not touch `Translations.Designer.cs`. Instead, double click on the `Resources/Languages/Translations.resx` file. If you use Visual Studio, use this extension [ResXManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager) to open `Resources/Languages/Translations.resx`.
2. To add a **new language**, use the extension to add a new language or Rider's built-in resource manager to add a new language.
3. Translate the values while preserving format parameters (e.g., `{0}`, `{1}`).
4. Be mindful of wording and string length, as some UI cards have strict bounds.
5. Provide fixes for typos or bad grammar directly in the `.resx` files.

### 📚 Documentation Coverage

Great documentation helps everyone!

- Ensure all README files, code comments, and in-app instructions are kept up-to-date.
- Use simple, descriptive English.
- Use `/// <summary>` tags above complex public methods.

---

<div align="center">

## 🌍 Localization Guidelines

</div>

**Never hardcode strings into the UI or Services**. All display texts, logs, and dialogs must use the Resource manager.

- **In C# Code**:
  Use `Translations.KeyName` (need to add `using optimizerDuck.Resources.Languages;`).

  ```csharp
  // Without arguments
  string title = Translations.Features_Desktop_Name;

  // With arguments string formatting
  string message = string.Format(Translations.Dashboard_SystemInfo_Storage_DiskInfo, 50.5, 100.0, 49.5);
  ```

  If that key seems like dynamic, you can use `Loc.Instance["KeyName"]`.

  ```csharp
  string title = Loc.Instance[$"Features.{category}.Name"];
  string message = string.Format(Loc.Instance[$"Dashboard.SystemInfo.Storage.{drive}.DiskInfo"], 50.5, 100.0, 49.5);
  ```

- **In XAML Views**:
  Use the custom `LocExtension` via `ext:Loc`.

  ```xaml
  <!-- Without arguments -->
  <ui:TextBlock Text="{ext:Loc Dashboard.Header.Title}" />

  <!-- With arguments bound dynamically -->
  <ui:TextBlock Text="{ext:Loc Dashboard.UpdateInfoBar.Message, {Binding ViewModel.LatestVersion}}" />
  ```

---

<div align="center">

## 💻 Coding Standards

</div>

We enforce a modern, clean C# codebase format.

### Code Style & Formatting Rules

- **Modern C#**: Take advantage of C# 10+ features like Primary Constructors, Collection Expressions (`[]`), File-Scoped namespaces, and implicit using directives where configured.
- **Nullability**: Nullable reference types are enabled (`<Nullable>enable</Nullable>`). Suppress warnings carefully or gracefully handle null instances (`?`).
- **Dependency Injection**: Ensure services and ViewModels are injected properly. Do not instantiate large services via `new`.
- **Indentation**: Use 4 spaces for indentation.

### Naming Conventions

- **Classes, Enums, Interfaces, Methods, Properties**: `PascalCase`
- **Private Fields**: `_camelCase` with a leading underscore.
- **Local Variables, Parameters**: `camelCase`
- **Constants**: `PascalCase` for public constants, `_PascalCase` for private constants.

---

<div align="center">

## 🔄 Pull Request Process

</div>

1. **Create a Branch**: Always branch out from `master`. Do not work directly on the `master` branch.
   ```bash
   git checkout -b feature/your-feature-name
   # OR
   git checkout -b fix/issue-123
   ```
2. **Commit Using Conventional Commits**:
   - `feat:` for new capabilities/optimizations
   - `fix:` for bug resolutions
   - `refactor:` for restructuring code
   - `docs:` for documentation updates
   - `test:` for adding/fixing tests
   - `i18n:` for translation updates
3. **Push & Open PR**:
   - Draft a PR description. Outline _what_ changed and _why_.
   - If your PR affects the UI (adding a new page, feature card, etc.), **you must link a screenshot or a GIF**.
   - Link any related GitHub issues (`Closes #42`).
4. **Code Review**: A maintainer will review your code. Be open to feedback!

---

<div align="center">

## 💬 Issue Guidelines

</div>

If you find a bug or have an idea, we want to hear from you via GitHub Issues.

- **Reporting a Bug**: Use the Bug Report template. Provide steps to reproduce, expected behavior, logs (found in `%localappdata%\optimizerDuck\`), and your system specs.
- **Suggesting a Feature**: Provide clear use-cases. What problem does this feature solve? How should it look? The more detail, the better.

---

<div align="center">

## 🎖 Credits

</div>

We love our community!
Contributors who have merged PRs will be listed and mentioned in release notes. If you contribute significantly to a specific feature or optimization module, feel free to add a discrete code author tag at the top of the file headers if desired.

---

<div align="center">

## 📜 License

</div>

By contributing to optimizerDuck, you agree that your contributions will be licensed under the project's [license](../LICENSE).

---

<div align="center">
  <p><i>Thank you for helping make optimizerDuck better for the entire Windows community! 🚀</i></p>
</div>
