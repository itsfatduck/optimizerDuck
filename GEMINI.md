# GEMINI.md

This file provides context and instructions for Gemini CLI when working in the **optimizerDuck** repository.

## Project Overview

**optimizerDuck** is a free, open-source Windows optimization tool designed for performance, privacy, and simplicity. It allows users to apply various system tweaks, manage bloatware, cleanup disk space, and manage startup items.

- **Primary Technologies**: .NET 10.0, WPF (Windows Presentation Foundation).
- **Architecture**: MVVM (Model-View-ViewModel) with CommunityToolkit.Mvvm.
- **UI Framework**: [WPF-UI](https://github.com/lepoco/wpfui) for modern Windows 11-style aesthetics.
- **Core Pattern**: Attribute-based optimization discovery and two-phase execution (Apply/Revert).

## Key Components

### 1. Core Engine
- **`BaseOptimization`**: The abstract base class for all system tweaks. Every optimization must implement `Apply()`, `Revert()`, and `IsOptimized()`.
- **`OptimizationRegistry`**: Discovers optimizations at runtime using reflection by scanning for types implementing `IOptimization` within `IOptimizationCategory` classes.
- **`OptimizationService`**: The central service that manages the execution and state of optimizations.
- **`RevertManager`**: Handles the persistence of "revert plans" (JSON files in `%appdata%\optimizerDuck\Revert\`) to allow safe undoing of changes.

### 2. System Operations
- **`RegistryService`**: Wrapper for Windows Registry modifications.
- **`ServiceProcessService`**: Manages Windows services (Start/Stop/Disable).
- **`ScheduledTaskService`**: Manages Windows Task Scheduler items.
- **`ShellService`**: Executes PowerShell commands with support for privilege elevation.

### 3. Application Lifecycle
- **Dependency Injection**: Powered by `Microsoft.Extensions.Hosting`. Services are registered in `App.xaml.cs`.
- **Logging**: Structured logging via Serilog, outputting to `%appdata%\optimizerDuck\optimizerDuck.log`.
- **Configuration**: User settings are stored in `appsettings.json` in the app data directory.
- **Localization**: Multi-language support using `.resx` files in `Resources/Languages/`.

## Building and Running

### Build Commands
```powershell
# Build the solution
dotnet build

# Publish - Portable (Multiple files)
dotnet publish optimizerDuck/optimizerDuck.csproj /p:PublishProfile=Portable

# Publish - Single File (One executable)
dotnet publish optimizerDuck/optimizerDuck.csproj /p:PublishProfile=Single
```

### Testing
```powershell
# Run all unit tests
dotnet test

# Run specific test project
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj
```

## Development Conventions

1.  **MVVM Pattern**: Strictly follow MVVM. Use `ObservableProperty` and `RelayCommand` from `CommunityToolkit.Mvvm`.
2.  **Optimization Implementation**:
    - New optimizations should be added as nested classes within an appropriate category class in `Core/Optimizers/`.
    - Must inherit from `BaseOptimization`.
    - Must record revert steps using `executionContext.RecordRevertStep()`.
3.  **Error Handling**:
    - Catch exceptions within optimizations to prevent app crashes.
    - Use `ILogger<T>` for all logging.
4.  **UI/UX**:
    - Follow WPF-UI design patterns.
    - Use `Translations.resx` for all user-facing strings to support localization.
5.  **Privilege Elevation**:
    - The app requires administrative privileges for most operations.
    - Services requiring elevation should accept a `password` parameter (never hardcoded).

## File System Locations

- **Root Data**: `%appdata%\optimizerDuck\`
- **Revert Data**: `%appdata%\optimizerDuck\Revert\`
- **Settings**: `%appdata%\optimizerDuck\appsettings.json`
- **Logs**: `%appdata%\optimizerDuck\optimizerDuck.log`
