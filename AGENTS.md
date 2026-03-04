# Repository Guidelines

Welcome to the `optimizerDuck` repository! This document provides guidelines for contributing to the project, ensuring consistency and maintainability across the codebase.

## Project Overview

optimizerDuck is a free, open-source Windows optimization tool built with WPF and .NET 10. It provides system performance optimizations, privacy enhancements, disk cleanup, bloatware removal, and startup management features.

## Build, Test & Lint Commands

### Building
```bash
# Build the entire solution
dotnet build optimizerDuck.slnx

# Or use the build script
./build.bat
```

### Testing
```bash
# Run all tests
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj

# Run a single test class
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --filter "FullyQualifiedName~OptimizationServiceTests"

# Run a single test method
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --filter "FullyQualifiedName~OptimizationServiceTests.ApplyAsync_Success_PersistsRevertDataFile"

# Run tests with coverage
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --collect:"XPlat Code Coverage"
```

### Linting
No explicit lint command is configured. The project uses standard C# conventions enforced by the compiler and IDE. The `.editorconfig` suppresses `CA1416` (Platform compatibility) warnings.

## Project Structure & Module Organization

```
optimizerDuck/
├── optimizerDuck/                 # Main application
│   ├── UI/                        # XAML views and view models
│   │   ├── Views/
│   │   │   ├── Pages/            # Page views (Dashboard, Settings, etc.)
│   │   │   ├── Windows/          # Window views (MainWindow)
│   │   │   └── Dialogs/          # Dialog views
│   │   └── ViewModels/           # MVVM view models
│   ├── Services/                  # Business logic and system operations
│   │   ├── OptimizationServices/ # Registry, Shell, Service, ScheduledTask
│   │   └── Managers/             # RevertManager, ConfigManager, LanguageManager
│   ├── Core/
│   │   ├── Optimizers/           # Optimization category implementations
│   │   ├── Models/               # Data models and interfaces
│   │   └── Interfaces/           # IOptimization, IOptimizationCategory
│   ├── Common/
│   │   ├── Converters/           # XAML value converters
│   │   ├── Helpers/              # Utility classes (Shared, ThemeResource)
│   │   └── Extensions/           # Extension methods
│   └── Resources/
│       ├── Languages/            # Localization .resx files
│       └── Images/               # Application assets
│
└── optimizerDuck.Test/            # Test suite
    └── Services/                  # Tests mirror main project structure
```

## Coding Style & Naming Conventions

### C# Conventions

- **Classes, Methods, Properties, Enums**: Use `PascalCase`
  ```csharp
  public class OptimizationService { }
  public void ApplyAsync() { }
  public string OptimizationKey { get; }
  public enum OptimizationRisk { Safe, Moderate, Risky }
  ```

- **Private Fields**: Use `_` prefix with `camelCase`
  ```csharp
  private readonly ILogger _logger;
  private static readonly Dictionary<Guid, bool> _stateCache = new();
  ```

- **Local Variables & Parameters**: Use `camelCase`
  ```csharp
  var optimizationId = optimization.Id;
  public Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress)
  ```

- **Constants**: Use `PascalCase` for public, `_PascalCase` for private
  ```csharp
  public const int MaxRetries = 3;
  private const int Win32Priority = 38;
  ```

### Import Organization

Imports are ordered alphabetically. `System.*` imports come first, followed by third-party libraries, then project namespaces:

```csharp
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Services.Managers;
```

### Primary Constructors (C# 12+)

The project uses primary constructors for dependency injection. Inject dependencies in the class declaration:

```csharp
public class OptimizationService(
    RevertManager revertManager,
    ILoggerFactory loggerFactory,
    IContentDialogService contentDialogService,
    ILogger<OptimizationService> logger)
{
    private readonly ILogger _logger = logger;
    // ...
}
```

### MVVM Pattern with CommunityToolkit.Mvvm

Use `[ObservableProperty]` for bindable properties and `[RelayCommand]` for commands:

```csharp
public partial class SettingsViewModel : ViewModel
{
    [ObservableProperty]
    private ApplicationTheme _currentApplicationTheme;

    [RelayCommand]
    private void OpenRootDir()
    {
        // ...
    }
}
```

### Nullable Reference Types

Nullable reference types are enabled. Use `?` for nullable types and null checks:

```csharp
public Type? OwnerType { get; set; }
private IHost? _host;

if (result != null)
    return result;
```

## Localization

All user-facing text must be localized using the `.resx` files in `optimizerDuck/Resources/Languages/`:

- `Translations.resx` - Default (English)
- Access via `Translations.ResourceName` or `Loc.Instance["Key"]`

Never hardcode UI strings in code. Add new strings to the `.resx` file first.

## Optimization Implementation Pattern

Optimizations follow a specific pattern using attributes and base classes:

```csharp
[OptimizationCategory(typeof(OptimizationPage))]
public class Performance : IOptimizationCategory
{
    public string Name { get; init; } = Loc.Instance[$"Optimizer.{nameof(Performance)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.Performance;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(
        Id = "GUID-HERE",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance)]
    public class DisableBackgroundApps : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context)
        {
            // Use RegistryService, ShellService, etc.
            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Path", "ValueName", 1)
            );
            context.Logger.LogInformation("Applied optimization");
            return Task.FromResult(ApplyResult.True());
        }
    }
}
```

Use `RevertManager.Record()` to enable reverting:

```csharp
RevertManager.Record(new ShellRevertStep
{
    ShellType = ShellType.PowerShell,
    Command = "revert command"
});
```

## Error Handling

- Use `ILogger` for logging errors, warnings, and information
- Use try-catch for operations that may fail (file I/O, shell commands)
- Return meaningful result objects instead of throwing exceptions for expected failures

```csharp
try
{
    // Operation
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to perform operation: {Details}", details);
    return ApplyResult.False("Operation failed");
}
```

## Testing Guidelines

- **Framework**: xUnit with coverlet for coverage
- **Test Class Naming**: Suffix with `Tests` (e.g., `OptimizationServiceTests`)
- **Test Method Naming**: Describe behavior being tested
  ```csharp
  [Fact]
  public async Task ApplyAsync_Success_PersistsRevertDataFile() { }

  [Fact]
  public async Task RevertAsync_WithValidData_RemovesFile() { }
  ```

- **Test Placement**: Mirror the main project structure
  - Tests for `Services/OptimizationService.cs` → `optimizerDuck.Test/Services/OptimizationServiceTests.cs`

- **WPF/STA Tests**: Use the `RunInStaThreadAsync` helper for tests requiring STA thread:
  ```csharp
  private static Task RunInStaThreadAsync(Func<Task> action)
  {
      var tcs = new TaskCompletionSource();
      var thread = new Thread(() =>
      {
          try { action().GetAwaiter().GetResult(); tcs.SetResult(); }
          catch (Exception ex) { tcs.SetException(ex); }
      });
      thread.SetApartmentState(ApartmentState.STA);
      thread.Start();
      return tcs.Task;
  }
  ```

## Key Dependencies

- **WPF-UI** (4.2.0): Modern WPF controls and theming
- **CommunityToolkit.Mvvm** (8.4.0): MVVM helpers
- **Microsoft.Extensions.Hosting**: Dependency injection and configuration
- **Serilog**: Structured logging
- **Newtonsoft.Json**: JSON serialization
- **TaskScheduler**: Windows scheduled task management

## Commit & Pull Request Guidelines

Follow **Conventional Commits** format:

- `feat:` - New features (e.g., `feat: add disk cleanup page`)
- `fix:` - Bug fixes (e.g., `fix: correct registry path`)
- `refactor:` - Code restructuring without behavior changes
- `docs:` - Documentation updates
- `test:` - Adding or modifying tests

### Pull Request Checklist

1. Prepend PR title with conventional commit type
2. Ensure build succeeds: `dotnet build optimizerDuck.slnx`
3. Ensure tests pass: `dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj`
4. Provide description of modifications and link relevant issues
5. Include screenshots/GIF for UI changes
