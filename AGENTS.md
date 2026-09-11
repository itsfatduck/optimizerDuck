# Repository Guidelines

## Windows-Only WPF App
- **Windows-only**: Build/run/test only on Windows. Target framework: `net10.0-windows10.0.17763.0` with `UseWPF=true`. `CA1416` (platform compatibility) is silenced — all code is Windows-only.
- **Runs as admin**: `app.manifest` sets `requireAdministrator` UAC level. Tests that modify system settings or registry need admin too.
- **Solution format**: `.slnx` (not `.sln`).
- **Data directory**: `%LocalAppData%\optimizerDuck\` — holds revert files (`Revert/`), resources (`Resources/`), downloads (`Resources/Downloads/`), assets (`Resources/Assets/`), crash logs (`Crashes/`).
- **Version**: Defined in `optimizerDuck.csproj` (`<Version>`). Don't hardcode it elsewhere; read it from the project file.

## Build, Test, Run Commands
- `dotnet restore optimizerDuck.slnx` — restore dependencies.
- `dotnet build optimizerDuck.slnx --configuration Release --no-restore` — CI-aligned build.
- `dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build` — run all tests.
- `dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build --filter "FullyQualifiedName~TestName"` — run a single test.
- `dotnet run --project optimizerDuck/optimizerDuck.csproj` — run locally (needs admin).
- `publish.bat portable` or `publish.bat single --skip-tests` — create release artifacts.
- `csharpier format .` — format all code with CSharpier (`csharpier check .` to verify without writing).

## Project Structure
- `optimizerDuck/` — WPF app (single project, no sub-projects):
  - `Domain/` — models, interfaces, attributes (no UI deps)
    - `Abstractions/` — `IOptimization`, `ICustomizeSetting`, `IRevertStep`, `ICustomizeCategory`, `IOptimizationCategory`, `IWindow`
    - `Attributes/` — `[Optimization]`, `[CustomizeSetting]`, `[OptimizationCategory]`, `[CustomizeCategory]`
    - `Conditions/` — compatibility condition system: `ICondition`, `ConditionBase`, `ConditionResult`, `ConditionState`, `ConditionValidation`, `WindowsBuilds`, and `BuiltIn/` (Windows version, CPU/GPU brand, min RAM, registry-key/service existence conditions)
    - `Exceptions/` — `StepExecutionException`
    - `Execution/` — `OpCall` (explicit per-operation context: changes, logger, cancellation), `ChangeSet` (thread-safe change collector) + `Change` (one recorded step, also the step result shown to the user), `OpResult` (single-operation result)
    - `Customize/Categories/` — Desktop, Gaming, Preferences, SystemFeatures (nested setting classes)
    - `Optimizations/Categories/` — Performance, SecurityAndPrivacy, Gpu, PowerManagement, BloatwareAndServices, UserExperience, AI (nested optimization classes)
    - `Optimizations/Models/` — `BaseOptimization`, `ApplyResult`, `OptimizationContext`, `OptimizationResult`
    - `Optimizations/Models/Services/` — `RegistryItem`, `ServiceItem` (+ `ServiceStartupType`), `ShellResult`
    - `Optimizations/Models/Bloatware/` — `AppXPackage`
    - `Optimizations/Models/Cleanup/` — `CleanupItem`
    - `Optimizations/Models/ScheduledTask/` — `ScheduledTaskModel`
    - `Optimizations/Models/StartupManager/` — `StartupApp`, `StartupTask`
    - `Customize/Models/` — `BaseCustomizeSetting`, `RegistryToggle`, `RegistryBinding`, `CustomizeRefreshScope`, `SettingOption`, `CustomizeControlType`, `RecommendationState`, `CustomizeRecommendationResult`
    - `Revert/` — `RevertData`, `RevertResult`
    - `Revert/Steps/` — `RegistryRevertStep`, `ServiceRevertStep`, `ScheduledTaskRevertStep`, `ShellRevertStep`, `UsbPowerRevertStep`
    - `Configuration/` — `AppSettings`
    - `UI/` — enums: `OptimizationRisk`, `OptimizationTags` (flags), `OptimizationCategoryOrder`, `CustomizeOrder`, `OptimizationSuccessResult`, `OptimizationState` (ObservableObject with relative-time display), `RiskVisual`, `ProcessingProgress`, `LanguageOption`
    - `Optimizations/Models/Services/` — also `RegistryValues` (shared value equality), `ShellPolicy` (shell success/error policy), `ShellResult`
  - `Services/` — business logic:
    - `Conditions/` — `ConditionEvaluator` (static, fail-open evaluation)
    - `Configuration/` — `ConfigManager`, `LanguageManager`
    - `Customize/` — `CustomizeRegistry` (reflection-based discovery), `CustomizationExecutor` (debounce + sequential serialization for setting writes)
    - `Optimization/` — `OptimizationRegistry`, `OptimizationService`, `OptimizationValidation` (fail-fast Id checks at startup)
    - `Optimization/Providers/` — `RegistryService`, `ScheduledTaskService`, `ServiceProcessService` are **static** (stateless); `ShellService` + `ProcessRunner` are DI singletons (stateful; `ShellService` reads the timeout live from settings). All take an explicit `OpCall`, record into `call.Changes`, log via `call.Logger`, return `OpResult`. `ShellMapping` centralises cmd/PowerShell argument building and result mapping.
  - `Windows/Services/ScStartupTypeParser.cs` — shared parser for `sc.exe qc` START_TYPE output, used by `ServiceProcessService`.
  - `ApplicationServiceCollectionExtensions.cs` — `AddOptimizerApplication(IConfiguration)`: the whole application graph, separated from `App.xaml.cs` so a test can build it
    - `Revert/` — `RevertManager` (atomic file-based revert data persistence)
    - `System/` — `RegistryWatcher` (+ `IRegistryWatcher`), `SystemInfoService` (defines `SystemSnapshot` + models), `StreamService`, `UpdaterService`, `CrossPageEventBus`, `CrossPageEvents`
    - `UI/` — `BloatwareService`, `DiskCleanupService`, `StartupManagerService`
  - `UI/` — XAML pages, ViewModels, windows, controls, dialogs, styles
  - `Common/` — extensions, helpers, converters:
    - `Helpers/` — `Shared.cs` (constants, paths, SafeApps/CautionApps sets), `ReflectionHelper.cs`, `SystemRefreshService.cs` (P/Invoke for Windows refresh), `EmbeddedResourceHelper.cs`, `WmiHelper.cs`, `GitHubSourceHelper.cs`, `ThemeResource.cs`, `HttpClientFactory.cs`, `UiThread.cs`
    - `Converters/` — WPF value converters (BooleanToVisibility, MBToGB, ...)
    - `Extensions/` — `StringExtensions`, `CustomizePageRegistryExtensions`, `OptimizationPageRegistryExtensions`, `LanguageExtensions`
  - `Resources/` — images, embedded assets, locale files
    - `Languages/` — `Translations.resx` (default English) + locale variants. Do **not** hardcode the list of languages; the authoritative lists are the files in this folder and `Languages` in `UI/ViewModels/Pages/SettingsViewModel.cs`.
- `optimizerDuck.Test/` — xUnit v3 tests (single test project with `InternalsVisibleTo`)
- Do **not** create top-level directories outside these two project folders.

## Key NuGet Packages
- `CommunityToolkit.Mvvm` 8.4.2 — MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`)
- `WPF-UI` + `WPF-UI.DependencyInjection` 4.3.0 — Fluent Design controls + navigation
- `Microsoft.Extensions.Hosting` + `Microsoft.Extensions.DependencyInjection` 10.0.10 — DI / hosted service wiring
- `Newtonsoft.Json` 13.0.4 — JSON serialization for revert data
- `Serilog` 4.4.0 + `Serilog.Extensions.Hosting` + `Serilog.Sinks.File` — structured logging
- `TaskScheduler` 2.12.2 — Windows scheduled task management
- `System.Management.Automation` 7.6.4 — PowerShell host integration
- `xunit.v3` 3.2.2 — test framework (with global using `Xunit` in test csproj)

> Package versions live in `optimizerDuck.csproj` / `optimizerDuck.Test.csproj` — verify there rather than trusting this snapshot.

## Optimization & Customize Discovery (Reflection, No Manual Registration)
- **New optimizations**: Create a **nested class** inside the relevant category class (e.g., `Domain/Optimizations/Categories/Performance.cs`), extend `BaseOptimization`, decorate with `[Optimization(Id = "guid", Risk = ..., Tags = ..., Condition = typeof(...)?)]`. This is the only optimization path.
- **New customize settings**: Same nesting pattern inside `Domain/Customize/Categories/`, extend `BaseCustomizeSetting`, decorate with `[CustomizeSetting(Section = ..., Icon = ..., Recommendation = ..., Condition = typeof(...)?)]`. `Icon` is required (`SymbolRegular` enum). `Section` can be a string or enum value. `Recommendation` can be `On`, `Off`, `Depends`, `Experimental`, or `None`.
- **Category classes**: Decorate with `[OptimizationCategory(typeof(PageClass))]` or `[CustomizeCategory(PageType = typeof(PageClass))]`.
- **Discovery**: `ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` scans assemblies whose name starts with `optimizerDuck` — no DI registration array to update. Results are cached in `_implementationCache`.
- **Provider services**: stateless services are static (`RegistryService`, `ServiceProcessService`, `ScheduledTaskService`) — call directly. `ShellService` + `ProcessRunner` are DI singletons, reached from optimizations as `context.Shell`. All take an explicit `OpCall`, record into `call.Changes`, return `OpResult`.
- **Results**: Optimizations end `ApplyAsync` with `return context.Changes.ToApplyResult();`. Do not manually construct `ApplyResult` except for early-out failures.
- **Single execution path**: `BaseOptimization` + `OptimizationContext` (`OpCall`) + static providers + `ShellService`, orchestrated by `OptimizationService`. There is no second framework — do not add one, and do not write a second implementation of an existing Windows operation.
- **One implementation per Windows operation**: `RegistryService`/`ServiceProcessService`/`ScheduledTaskService`/`ShellService` are the single source of truth (plus the shared `ScStartupTypeParser`, `RegistryValues`, `ShellMapping`). Fix behaviour there, not in a copy.
- **DI**: register through `AddOptimizerApplication(configuration)` (the whole app graph); `App.xaml.cs` only builds the host. The host sets `ValidateOnBuild`/`ValidateScopes`, so a broken registration fails at startup instead of at Apply time — keep it that way.
- **Per-step failure policy**: providers record a failed `Change` with an error and (where a retry can help) a retry action; they do not throw. Only truly unrecoverable state throws. `OptimizationService` persists partial work with `CancellationToken.None` and builds the `OptimizationResult`.
- **Preloading**: `OptimizationRegistry.PreloadOptimizationsAsync()` / `EnsurePreloadedAsync()` (and `CustomizeRegistry.PreloadCategoriesAsync()` / `EnsurePreloadedAsync()`) run reflection discovery on a background thread. `App.xaml.cs` preloads at startup; the Optimize/Customize pages call `EnsurePreloadedAsync()` before binding.

## Condition System (Compatibility Gating)
- Conditions live in `Domain/Conditions/` (`ICondition`, `ConditionBase`, `ConditionResult`, `ConditionState`, `ConditionValidation`, `WindowsBuilds`, `BuiltIn/`). They're evaluated by the static `Services/Conditions/ConditionEvaluator.cs`.
- `[Optimization]` and `[CustomizeSetting]` both accept an optional `Condition = typeof(SomeCondition)` where the type implements `ICondition` with a public parameterless constructor.
- `ConditionState` = `Available` | `Unsupported` | `Error`. Only `Unsupported` blocks an item, and only when the item isn't already applied (or hidden by the user). `Error` and an unpopulated `SystemSnapshot` **fail open** (never hide).
- `ConditionValidation.Validate(...)` runs at discovery time to fail fast on misconfigured condition metadata.
- Custom conditions return `ConditionResult.Available` / `ConditionResult.Unsupported(titleProvider, descriptionProvider)` / `ConditionResult.Error()`. User-facing text must go behind localization providers (`() => Loc.Instance[...]`).

## Revert System
- **File-based**: Each applied optimization creates `%LocalAppData%\optimizerDuck\Revert\{optimizationId}.json`. Applied state is inferred from file presence on disk.
- **Atomic writes**: `RevertManager` writes to `.tmp` via `FileStream(WriteThrough)` + `Flush(flushToDisk: true)`, then `File.Replace` for crash safety. Stale `.tmp` files are swept at startup (`RemoveOrphanedTempFiles`).
- **Concurrent access**: Per-file `SemaphoreSlim` locks with 30-second timeout prevent race conditions. Lock entries are never disposed while in use.
- **Compact layout**: only successful steps persist, each with a fresh index; no null gaps. Re-apply appends new entries; recovered retry steps append via `AppendRevertStepAsync` (never overwrite — LIFO revert still ends at the original backup).
- **Step types**: `RegistryRevertStep`, `ServiceRevertStep`, `ScheduledTaskRevertStep`, `ShellRevertStep`, `UsbPowerRevertStep`. Each verifies its own effect inside `ExecuteAsync` (registry read-back, service re-query, task state re-check). Access-denied is failure, never success. Missing `OriginalEnabled` throws (fail-closed).
- **Unknown types**: persisted data with an unregistered step type becomes a failing step with a clear message (never silently skipped); raw payload round-trips.
- **Step registry**: Revert step deserialization uses reflection-based `_stepRegistry` (`ConcurrentDictionary`). New step types auto-register by implementing `IRevertStep` with a static `FromData(JObject)` method.
- **Retry**: `OptimizationService.RetryFailedStepsWithResultsAsync()` re-invokes `Retry` with a fresh `OpCall` and persists recovered steps via `AppendRevertStepAsync`.
- **Key methods**: `SaveRevertDataAsync()`, `RevertAsync()`, `AppendRevertStepAsync()`, `RemoveRevertStepsAtIndexesAsync()`, `IsAppliedAsync(id)`, `GetRevertDataAsync(id)`, `ClearAllRevertData()`, `RemoveOrphanedTempFiles()`.

## Customize Setting API Notes
- `RegistryToggle` uses `OnValues` / `OffValues` (lists; `null` = key absent) + `DefaultValue`, `IsOptional`, `ValueKind`. There is no `OnValue`/`OffValue`/`TreatMissingAsDefault`.
- Dropdown settings override `ControlType => CustomizeControlType.Dropdown` and `GetOptions()` returning `SettingOption`s built with `Option(key, regPath, regName, value)` or `Option(key, value, params RegistryBinding[])`. The base class auto-reads/auto-writes via bindings and shows a "Custom"/"Not set" fallback for out-of-scope values.
- When overriding `ApplyAsync`, call `await ExecutePostActionAsync()` (guarded by `NeedsPostAction`) yourself.

## Coding & Style
- Nullable enabled, file-scoped namespaces, implicit usings.
- Indent: 4 spaces. PascalCase types/members, `_camelCase` private fields, `camelCase` locals/params.
- Max line length: 100 characters (enforced by `.editorconfig`).
- **No hardcoded UI strings** — use `Loc.Instance["Key"]` (C#) or `Translations.KeyName` (XAML bindings). Add new keys to `Resources/Languages/Translations.resx`.
- **Keep comments sparse** — existing code has almost none; do not add unnecessary ones.
- DI via `Microsoft.Extensions.Hosting` + `CommunityToolkit.Mvvm`. Pages + ViewModels registered as singletons in `App.xaml.cs`.
- `optimizerDuck.csproj` has `<InternalsVisibleTo Include="optimizerDuck.Test" />` — test project can access internal members.
- Category pages auto-register via `services.AddAllCustomizeCategoryPages()` and `services.AddAllOptimizationPages()`.
- `ProcessRunner` + `ShellService` come from DI; there is no init step. `OptimizationService` creates the per-apply `ChangeSet`. Revert steps that run commands receive the app shell service from `RevertManager` so the configured timeout applies to undo.
- The full build/format/analysis gate is: `dotnet build ... Release` must be **0 warnings** (`.editorconfig` promotes CA2016/CA1068/CA2250/CA2213/CA2219/IDE0330 to warnings), then `dotnet test`, then `csharpier check .`.
- `WmiHelper.Initialize()` registers WMI cleanup for abnormal termination.

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
- `ShellService.CMDAsync()` and `PowerShellAsync()` run commands in cmd.exe / PowerShell (`-EncodedCommand`). `QueryCMDAsync()` / `QueryPowerShellAsync()` run without recording a change.
- Async variants accept an optional revert step (from `revertCommand` string) that gets saved for undo.
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
