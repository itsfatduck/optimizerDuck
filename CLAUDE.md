# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Development Commands

### Building the Application
This is a .NET 10.0 WPF application with two publish profiles:

```bash
# Portable build (multiple files, smaller executable)
dotnet publish optimizerDuck/optimizerDuck.csproj /p:PublishProfile=Portable

# Single-file build (single large executable)
dotnet publish optimizerDuck/optimizerDuck.csproj /p:PublishProfile=Single
```

Reference: `build.bat` for the interactive build script.

### Testing
```bash
# Run all tests
dotnet test

# Run tests with specific filter
dotnet test --filter "FullyQualifiedName~OptimizerName"

# Run specific test project
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj
```

### General Development
```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Clean solution
dotnet clean
```

## High-Level Architecture

optimizerDuck is a Windows optimization tool built with WPF, following MVVM pattern and modern .NET practices. Understanding the architecture requires knowledge across multiple layers:

### 1. Discovery and Registration System

**Attribute-Based Optimization Discovery**: Optimizations are discovered at startup via reflection using `[Optimization]` attributes. The `OptimizationRegistry` in `Services/OptimizationRegistry.cs` handles this by scanning assemblies for types implementing `IOptimization`.

Key files:
- `OptimizerDuck.Core.Optimizers.BaseOptimization` - Abstract base for all optimizations
- `OptimizerDuck.Services.OptimizationRegistry` - Discovers and registers optimizations
- `OptimizerDuck.Core.Models.Attributes.OptimizationAttribute` - Defines optimization metadata
- `OptimizerDuck.Services.OptimizationPageRegistryExtensions` - DI registration helper

**Category Organization**: Optimizations are grouped by categories (e.g., Performance, Security). Categories use `[OptimizationCategory]` attributes and are ordered via `OptimizationCategoryOrder`.

### 2. Execution and Revert System

**Two-Phase Execution with Revert Support**: Each optimization can modify system state and record a revert plan. This is handled by the `OptimizationService` and `RevertManager`:

- **Apply Phase**: Optimizations execute modifications and capture revert data
- **Revert Data**: Stored as JSON in `%appdata%\optimizerDuck\Revert\{guid}.json`
- **Revert Steps**: Support registry, services, scheduled tasks, and shell command reversions

Key files:
- `OptimizerDuck.Services.OptimizationService` - Coordinates optimization execution
- `OptimizerDuck.Services.Managers.RevertManager` - Manages revert data persistence
- `OptimizerDuck.Core.Models.Execution.OptimizationExecutionContext` - Tracks execution state
- `OptimizerDuck.Core.Models.Revert.*` - Revert data models and step definitions

### 3. Low-Level System Operations

**Service Layer**: System modifications are abstracted through service classes in `Services/OptimizationServices/`:

- `RegistryService` - Windows registry modifications
- `ServiceProcessService` - Windows service control
- `ScheduledTaskService` - Task scheduler operations
- `ShellService` - PowerShell command execution

**Critical Pattern**: These services accept a `password` parameter for privilege elevation. Never hardcode passwords - always accept them as method parameters. The `ShellService.Init()` method configures the elevation approach based on app settings.

### 4. State Management and Tracking

**State Persistence**: Current optimization states are cached in-memory and serialized to disk. The system tracks whether each optimization is applied and when.

- `OptimizationService._stateCache` - In-memory state tracking
- State files stored in `%appdata%\optimizerDuck\`
- State must survive application restarts

### 5. Dependency Injection and App Lifecycle

**DI Container Setup**: The application uses `Microsoft.Extensions.Hosting` and services are registered in `App.xaml.cs`. Key points:

- All major services are registered as singletons
- Optimization pages are registered via `AddAllOptimizationPages()` extension method
- WPF UI services (navigation, dialogs) use WPF-UI framework integration
- Serilog is used for structured logging with a custom `ScopeBlockTextFormatter`

**App Flow**:
1. `App.OnStartup()` builds DI container, validates configuration
2. `OptimizationRegistry.PreloadOptimizations()` discovers optimizations
3. MainWindow shown via navigation service
4. User interactions trigger `OptimizationService.ApplyOptimizationAsync()` or `RevertOptimizationAsync()`
5. `App.OnExit()` flushes logs

### 6. Multi-Language Support

**Resource-Based Localization**: Uses .resx files in `Resources/Languages/`. The `LanguageManager` handles culture switching. Always use resource keys from `Translations.resx` for user-visible strings.

## Important Patterns and Constraints

### Optimization Implementation Pattern

Every optimization must:
1. Inherit from `BaseOptimization`
2. Have `[Optimization]` attribute with unique ID and metadata
3. Implement `IsOptimized()` to check current state
4. Implement `Revert()` to revert changes
5. Record revert steps via `executionContext.RecordRevertStep()`
6. Use async/await for all async operations

### Error Handling and Logging

- Always use `ILogger<T>` for logging (injected via DI)
- Use Serilog's structured logging: `logger.LogInformation("Applied {Count} changes", count)`
- Catch exceptions at optimization boundaries - individual optimization failures shouldn't crash the app
- User-facing errors should use the WPF-UI dialog service

### File System Locations

The app uses these directories (see `Common.Helpers.Shared`):
- `%appdata%\optimizerDuck\` - Root directory for app data
- `%appdata%\optimizerDuck\Revert\` - Revert data storage
- `%appdata%\optimizerDuck\appsettings.json` - Configuration file
- `%appdata%\optimizerDuck\optimizerDuck.log` - Log file

## Testing Approach

The project uses xUnit. When adding new optimizations:
1. Add unit tests for the optimization logic
2. Test revert functionality thoroughly
3. Mock system services using test fakes
4. Integration tests should use a test-friendly DI setup

Reference: `optimizerDuck.Test/Services/Managers/RevertManagerTests.cs` for testing patterns.

## Key Dependencies

- **WPF-UI (4.2.0)**: UI framework, must follow their patterns for controls and dialogs
- **CommunityToolkit.Mvvm (8.4.0)**: MVVM infrastructure, use `[ObservableProperty]` and automatic command generation
- **Serilog (4.3.1)**: Logging framework with structured logging support
- **Microsoft.Extensions.Hosting (10.0.3)**: DI and app lifecycle management

## Important Files

- `optimizerDuck/appsettings.json` (user config, created at runtime) - App configuration including theme, language, elevation password
- `*.pubxml` files - Publish configurations
- `OptimizationService.cs` - Core optimization execution logic
- `BaseOptimization.cs` - Base class for all optimizations
- `RevertManager.cs` - Revert system coordinator
- `app.manifest` - Windows UAC manifest for privilege elevation
