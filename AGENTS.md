# Repository Guidelines

## Windows-Only WPF App
- **Windows-only**: Build/run/test only on Windows. Target framework: `net10.0-windows10.0.17763.0` with `UseWPF=true`. `CA1416` (platform compatibility) is silenced — all code is Windows-only.
- **Runs as admin**: `app.manifest` sets `requireAdministrator` UAC level. Tests that modify system settings or registry need admin too.
- **Solution format**: `.slnx` (not `.sln`).
- **Data directory**: `%LocalAppData%\optimizerDuck\` — holds revert files (`Revert/`), resources (`Resources/`), downloads (`Resources/Downloads/`), assets (`Resources/Assets/`), crash logs (`Crashes/`).
- **Version**: Currently 2.24.2 (defined in `optimizerDuck.csproj`).

## Build, Test, Run Commands
- `dotnet restore optimizerDuck.slnx` — restore dependencies.
- `dotnet build optimizerDuck.slnx --configuration Release --no-restore` — CI-aligned build.
- `dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build` — run all tests.
- `dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build --filter "FullyQualifiedName~TestName"` — run a single test.
- `dotnet run --project optimizerDuck/optimizerDuck.csproj` — run locally (needs admin).
- `publish.bat portable` or `publish.bat single --skip-tests` — create release artifacts.
- `dotnet csharpier .` — format all code with CSharpier.

## Project Structure
- `optimizerDuck/` — WPF app (single project, no sub-projects):
  - `Domain/` — models, interfaces, attributes (no UI deps)
    - `Abstractions/` — `IOptimization`, `ICustomizeSetting`, `IRevertStep`, `ICustomizeCategory`, `IOptimizationCategory`, `IWindow`
    - `Attributes/` — `[Optimization]`, `[CustomizeSetting]`, `[OptimizationCategory]`, `[CustomizeCategory]`
    - `Execution/` — `ExecutionScope` (ambient `AsyncLocal`-based step tracking)
    - `Customize/Categories/` — Desktop, Gaming, Preferences, SystemFeatures (nested setting classes)
    - `Optimizations/Categories/` — Performance, SecurityAndPrivacy, Gpu, PowerManagement, BloatwareAndServices, UserExperience (nested optimization classes)
    - `Optimizations/Models/` — `BaseOptimization`, `ApplyResult`, `OptimizationContext`
    - `Optimizations/Models/Services/` — `RegistryItem`, `ServiceItem` (+ `ServiceStartupType`), `ShellResult`
    - `Optimizations/Models/Bloatware/` — `AppXPackage`
    - `Optimizations/Models/Cleanup/` — `CleanupItem`
    - `Optimizations/Models/ScheduledTask/` — `ScheduledTaskModel`
    - `Optimizations/Models/StartupManager/` — `StartupApp`, `StartupTask`
    - `Customize/Models/` — `BaseCustomizeSetting`, `RegistryToggle`, `CustomizeRefreshScope`, `SettingOption`, `CustomizeControlType`, `RecommendationState`, `CustomizeRecommendationResult`
    - `Revert/` — `RevertData`, `RevertResult`
    - `Revert/Steps/` — `RegistryRevertStep`, `ServiceRevertStep`, `ScheduledTaskRevertStep`, `ShellRevertStep`, `UsbPowerRevertStep`
    - `Configuration/` — `AppSettings`
    - `UI/` — enums: `OptimizationRisk`, `OptimizationTags` (flags), `OptimizationCategoryOrder`, `CustomizeOrder`, `OptimizationSuccessResult`, `OptimizationState` (ObservableObject with relative-time display), `RiskVisual`, `ProcessingProgress`, `LanguageOption`
  - `Services/` — business logic:
    - `Configuration/` — `ConfigManager`, `LanguageManager`
    - `Customize/` — `CustomizeRegistry` (reflection-based discovery)
    - `Optimization/` — `OptimizationRegistry`, `OptimizationService`
    - `Optimization/Providers/` — static: `RegistryService`, `ShellService` (+ `ShellPolicy`), `ScheduledTaskService`, `ServiceProcessService`
    - `Revert/` — `RevertManager` (atomic file-based revert data persistence)
    - `System/` — `RegistryWatcher` (+ `IRegistryWatcher`), `SystemInfoService`, `StreamService`, `UpdaterService`
    - `UI/` — `BloatwareService`, `DiskCleanupService`, `StartupManagerService`
  - `UI/` — XAML pages, ViewModels, windows, controls, dialogs, styles
  - `Common/` — extensions, helpers, converters:
    - `Helpers/` — `Shared.cs` (constants, paths, SafeApps/CautionApps sets), `ReflectionHelper.cs`, `SystemRefreshService.cs` (P/Invoke for Windows refresh), `EmbeddedResourceHelper.cs`, `WmiHelper.cs`, `GitHubSourceHelper.cs`, `ThemeResource.cs`
    - `Converters/` — 20+ WPF value converters
    - `Extensions/` — `StringExtensions`, `CustomizePageRegistryExtensions`, `OptimizationPageRegistryExtensions`, `LanguageExtensions`
  - `Resources/` — images, embedded assets, locale files
    - `Languages/` — `Translations.resx` (default) + 11 locale variants (vi-VN, es-ES, fr-FR, zh-TW, zh-CN, ru-RU, ko-KR, ja-JP, pl-PL, tr-TR, pt-BR)
- `optimizerDuck.Test/` — xUnit v3 tests (single test project with `InternalsVisibleTo`)
- Do **not** create top-level directories outside these two project folders.

## Key NuGet Packages
- `CommunityToolkit.Mvvm` 8.4.2 — MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`)
- `WPF-UI` + `WPF-UI.DependencyInjection` 4.3.0 — Fluent Design controls + navigation
- `Microsoft.Extensions.Hosting` 10.0.9 — DI / hosted service wiring
- `Newtonsoft.Json` 13.0.4 — JSON serialization for revert data
- `Serilog` 4.3.1 + sinks — structured logging
- `TaskScheduler` 2.12.2 — Windows scheduled task management
- `System.Management.Automation` 7.6.3 — PowerShell host integration
- `xunit.v3` 3.2.2 — test framework (with global using `Xunit` in test csproj)

## Optimization & Customize Discovery (Reflection, No Manual Registration)
- **New optimizations**: Create a **nested class** inside the relevant category class (e.g., `Domain/Optimizations/Categories/Performance.cs`), extend `BaseOptimization`, decorate with `[Optimization(Id = "guid", Risk = ..., Tags = ...)]`.
- **New customize settings**: Same nesting pattern inside `Domain/Customize/Categories/`, extend `BaseCustomizeSetting`, decorate with `[CustomizeSetting(Section = ..., Icon = ..., Recommendation = ...)]`. `Icon` is required (`SymbolRegular` enum). `Section` can be a string or enum value. `Recommendation` can be `On`, `Off`, `Depends`, `Experimental`, or `None`.
- **Category classes**: Decorate with `[OptimizationCategory(typeof(PageClass))]` or `[CustomizeCategory(PageType = typeof(PageClass))]`.
- **Discovery**: `ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` scans assemblies whose name starts with `optimizerDuck` — no DI registration array to update. Results are cached in `_implementationCache`.
- **Static provider services**: `RegistryService`, `ServiceProcessService`, `ScheduledTaskService`, `ShellService` are **static classes** (not DI-registered). They capture revert steps into the ambient `ExecutionScope`.
- **`CompleteFromScope()`**: Optimizations call `BaseOptimization.CompleteFromScope()` to build the `ApplyResult` from steps recorded in the ambient `ExecutionScope`. Do not manually construct `ApplyResult`.
- **Preloading**: `OptimizationRegistry.StartPreload()` runs discovery on a background thread at startup. The Optimizations page calls `EnsurePreloadedAsync()` before binding.
- **Customize discovery**: `CustomizeRegistry` uses the same reflection pattern for `ICustomizeSetting` implementations nested inside `ICustomizeCategory` classes.

## Revert System
- **File-based**: Each applied optimization creates `%LocalAppData%\optimizerDuck\Revert\{optimizationId}.json`. Applied state is inferred from file presence on disk.
- **Atomic writes**: `RevertManager` writes to `.tmp` then `File.Replace` for crash safety.
- **Concurrent access**: Per-file `SemaphoreSlim` locks with 30-second timeout prevent race conditions.
- **Step types**: `RegistryRevertStep`, `ServiceRevertStep`, `ScheduledTaskRevertStep`, `ShellRevertStep`, `UsbPowerRevertStep`.
- **`ExecutionScope`**: Uses `AsyncLocal<ExecutionScope?>` for ambient step tracking — no need to pass context through parameters.
- **Scope variants**: `ExecutionScope.Begin()` (creates persistable scope), `ExecutionScope.BeginForLogging()` (logging only, no persistence), `ExecutionScope.BeginForCapture()` (for retry, `OptimizationId = Guid.Empty`).
- **`ExecutionScope.RecordStep()`**: Auto-incremented index. Records name, description, success/fail status, revert step, error, retry action, error detail.
- **`ExecutionScope.RecordStepAtIndex()`**: For retry — preserves original index layout in revert files.
- **`ExecutionScope.Track()`**: Tracks service-level success/fail counts for summary logging.
- **Step registry**: Revert step deserialization uses reflection-based `_stepRegistry` (`ConcurrentDictionary`). New step types auto-register by implementing `IRevertStep` with a static `FromData(JObject)` method.
- **Upsert**: `RevertManager.UpsertRevertStepAtIndexAsync()` can add/replace revert steps at specific indices (used during retry to persist recovered steps).
- **Key methods**: `SaveRevertDataAsync()`, `RevertAsync()`, `IsAppliedAsync(id)`, `GetRevertDataAsync(id)`, `ClearAllRevertData()`.

## Coding & Style
- Nullable enabled, file-scoped namespaces, implicit usings.
- Indent: 4 spaces. PascalCase types/members, `_camelCase` private fields, `camelCase` locals/params.
- Max line length: 100 characters (enforced by `.editorconfig`).
- **No hardcoded UI strings** — use `Loc.Instance["Key"]` (C#) or `Translations.KeyName` (XAML bindings). Add new keys to `Resources/Languages/Translations.resx`.
- **Keep comments sparse** — existing code has almost none; do not add unnecessary ones.
- DI via `Microsoft.Extensions.Hosting` + `CommunityToolkit.Mvvm`. Pages + ViewModels registered as singletons in `App.xaml.cs`.
- `optimizerDuck.csproj` has `<InternalsVisibleTo Include="optimizerDuck.Test" />` — test project can access internal members.
- Category pages auto-register via `services.AddAllCustomizeCategoryPages()` and `services.AddAllOptimizationPages()`.
- ShellService must be initialized at startup: `ShellService.Init(appOptionsMonitor)`.

## Testing (xUnit v3, Integration-Style)
- **No mocking libraries** — all test doubles are hand-written (`FakeOptimization`, `TestOptimization`, etc.) implementing interfaces directly.
- **Real I/O**: Tests use real filesystem (revert JSON files), real registry (`HKCU\Software\TestOptimizerDuck*`), real process execution (CMD/PowerShell).
- **STA thread**: Tests involving WPF components must use `RunInStaThreadAsync` helper (STA thread + `TaskCompletionSource`). The test project itself defines this helper — add it if missing.
- **Logging**: Use `NullLogger<T>.Instance` / `NullLoggerFactory.Instance` for DI logging parameters.
- **Test naming**: `{Method}_{Scenario}_{ExpectedResult}` (e.g., `ApplyAsync_Success_PersistsRevertDataFile`).
- **Cleanup**: Use `try/finally` or `IDisposable` for test artifact cleanup (revert files, registry keys). Tests that create revert data must clean up both `{id}.json` and `{id}.json.tmp`.
- **No coverage gate** — prioritize meaningful unit coverage for changed logic.
- **CI test command** uses `--blame-hang --blame-hang-timeout 30s` — tests must not hang longer than 30s.
- **Test project packages**: `Microsoft.NET.Test.Sdk` 18.6.0, `xunit.v3` 3.2.2, `coverlet.collector` 10.0.1.

## Shell Service Details
- `ShellService.CMD()` and `CMDAsync()` run commands in cmd.exe.
- `ShellService.PowerShell()` and `PowerShellAsync()` run commands with `-EncodedCommand` for safe encoding.
- Both sync and async variants accept an optional `ShellRevertStep` (from `revertCommand` string or `Func<string>`) that gets saved for undo.
- `ShellPolicy` class provides customizable success criteria (default: exit code 0). Use `ShellPolicy.SuccessExitCodes()` or `ShellPolicy.SuccessExitCodeRange()` for non-standard exit codes.
- Default timeout: 120 seconds (configurable via `AppSettings.Optimize.ShellTimeoutMs`).
- UTF-8 encoding is forced for both stdout and stderr.

## Registry Service Details
- Supports multiple root key prefixes: HKLM, HKLM:, HKEY_LOCAL_MACHINE, HKCU, HKCU:, HKEY_CURRENT_USER, HKCR, HKCR:, HKEY_CLASSES_ROOT, HKU, HKU:, HKEY_USERS, HKCC, HKCC:, HKEY_CURRENT_CONFIG.
- `Write()` — writes a value, backing up the previous value for revert. Supports params array for batch writes. Can create missing subkeys.
- `Read<T>()` — reads with type conversion (supports bool, int, string, string[], byte[], enum). Uses `DoNotExpandEnvironmentNames`.
- `DeleteValue()` — deletes a value with backup for revert.
- `CreateSubKey()` — creates a subkey, tracking created intermediate keys for revert cleanup.
- `DeleteSubKeyTree()` — deletes an entire key tree, backing up all values/subkeys first (max depth 15, max 5000 items).
- `KeyExists()` — checks if a registry key exists.
- `CleanupEmptyKeys()` — removes empty keys that were created during apply (sorts by depth, child-first).
- `Write(params RegistryItem[] items)` and `DeleteValue(params RegistryItem[] items)` — batch variants that deduplicate.

## Service Process Service Details
- `GetStartupTypeAsync(serviceName)` — queries current startup type via `sc.exe qc`. Returns `(ServiceStartupType?, bool NotFound)`. Uses locale-independent regex parsing of `sc qc` output.
- `ChangeServiceStartupTypeAsync(ServiceItem)` — changes via `sc.exe config`. Records `ServiceRevertStep` only if the startup type actually changed.
- `ChangeServiceStartupTypeAsync(params ServiceItem[])` — batch variant for multiple services.
- `ServiceStartupType` enum: `Automatic`, `AutomaticDelayedStart`, `Manual`, `Disabled`. Note: Boot (0) and System (1) startup types are not used in this app.
- Uses `sc.exe` with timeouts: 15s for queries, 30s for config changes.

## Scheduled Task Service Details
- Uses `Microsoft.Win32.TaskScheduler` library (TaskScheduler NuGet package).
- `DisableTask(fullPath)` / `EnableTask(fullPath)` — toggle task state with revert step recording.
- `IsTaskEnabled(fullPath)` — checks if a task exists and is enabled.
- `DeleteTask(fullPath)` — deletes a task.
- `GetAllTasks()` — enumerates all tasks recursively with icon extraction via `StartupManagerService.ExtractIcon`.
- `GetStartupTasks()` — filtered to tasks with LogonTrigger or BootTrigger.
- `RegisterTask(folderPath, model)` — registers a new task from `ScheduledTaskModel`.
- `RunTask(fullPath)` / `StopTask(fullPath)` — controls task execution.
- `GetTaskState(fullPath)` — returns current task state string.

## Revert Step Types
- `RegistryRevertStep` — actions: `RestorePrevious`, `NoPreviousValue`, `RestoreKey`, `RestoreKeyTree`. Tracks created subkeys for cleanup.
- `ServiceRevertStep` — stores `ServiceName` and `OriginalStartupType`.
- `ScheduledTaskRevertStep` — stores `FullPath` and `OriginalEnabled` state.
- `ShellRevertStep` — stores `ShellType` (CMD/PowerShell) and `Command`.
- `UsbPowerRevertStep` — stores list of `DeviceState` (InstanceName + Enable) for USB power settings.

## Commit & PR
- Conventional Commits: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `i18n:`, `chore:`.
- Branch from `master`: `feature/<name>` or `fix/<issue-id>`.
- PRs: clear description, linked issue (`Closes #123`), passing CI (build + test), screenshot for UI changes.
- Never commit secrets or machine-specific paths.
- Run `csharpier format .` before committing.
- Verify with: `dotnet build`, `dotnet test`, `csharpier format .` before push.

<!-- rtk-instructions v2 -->
# RTK — Always Prefix Shell Commands with `rtk`

**Every shell command must be prefixed with `rtk`** (RTK = Rust Token Killer). This filters and compresses output before it reaches the LLM context, saving 60-90% tokens. RTK passes through any command it doesn't recognize.

In a command chain, prefix each command individually:
```bash
rtk git add . && rtk git commit -m "msg" && rtk git push
```

Meta commands: `rtk gain` (savings stats), `rtk discover` (find missed opportunities), `rtk proxy <cmd>` (run raw for debugging).
<!-- /rtk-instructions -->
