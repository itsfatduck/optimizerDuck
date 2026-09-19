# Repository Guidelines

Windows-only WPF desktop app that optimizes Windows. Read this before changing code.

## Hard rules
- **Windows only.** `optimizerDuck` targets `net10.0-windows` with `UseWPF=true` and deliberately **no Windows SDK version in the TFM**: a versioned one makes the SDK reference the CsWinRT projection and ships a 50 MB `Microsoft.Windows.SDK.NET.dll` for WinRT APIs this app never calls. `CA1416` is silent because every path is Windows-only. (`optimizerDuck.Test` does carry `10.0.17763.0`.)
- **Elevated.** `app.manifest` requests `requireAdministrator`. A test that writes the registry, a service or a scheduled task needs an elevated shell too.
- **Two projects, nothing else.** `optimizerDuck/` (the app) and `optimizerDuck.Test/`, in `optimizerDuck.slnx` (not `.sln`). Never create a top-level directory outside them.
- **One implementation per Windows operation.** Fix behaviour in the owner, never in a copy. See Providers.
- **One execution path.** Every system-changing run goes through `OperationRunner`. Never add a second framework.
- **No hardcoded UI strings.** `Loc.Instance["Key"]` in C#, `Translations.KeyName` in XAML. Add keys to `Resources/Languages/Translations.resx`.
- **A warning fails the build** (`TreatWarningsAsErrors` + `EnforceCodeStyleInBuild` in both project files; the severity table in `.editorconfig` decides). `AnalysisMode=All` is deliberately off.

## Commands
- `dotnet restore optimizerDuck.slnx`
- `dotnet build optimizerDuck.slnx --configuration Release --no-restore` — the CI-aligned gate.
- `dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build` — all tests. Add `--filter "FullyQualifiedName~TestName"` for one.
- `dotnet run --project optimizerDuck/optimizerDuck.csproj` — run locally (needs admin).
- `publish.bat portable|single [--skip-tests] [--no-pause]` — release artifacts; runs the tests first unless skipped.
- `csharpier format .` / `csharpier check .` — global tool, CI pins 1.3.0. Run it as `csharpier`, never `dotnet csharpier` (that fails as "command not found", which reads like a missing tool). The editor reformats on save, so a touched file may come back with unrelated lines reflowed; that is the formatter, not your edit.
- Full gate before push: build, test, `csharpier check .`. CI (`.github/workflows/ci.yml`) = restore, Release build, test with `--blame-hang --blame-hang-timeout 30s`, format check.

## Layout
- `optimizerDuck/` — the WPF app.
  - `Domain/` — models, contracts, records. No UI dependencies.
    - `Abstractions/` — `IOptimization`, `IOptimizationCategory`, `ICustomizeSetting`, `ICustomizeCategory`, `IRevertStep`, `RevertContext`, `IWindow`
    - `Attributes/` — `[Optimization]`, `[OptimizationCategory]`, `[CustomizeSetting]`, `[CustomizeCategory]`
    - `Conditions/` + `BuiltIn/` — compatibility gating
    - `Execution/` — `OpCall`, `ChangeSet` + `Change` + `ChangeKind`, `ChangeDetail` + `ChangeDetailCodec`, `OpResult`, `OperationSubject`, `OperationRequest`, `ToolSubjects`, `ChangeRecord`
    - `Optimizations/Categories/` — Performance, SecurityAndPrivacy, Gpu, PowerManagement, BloatwareAndServices, UserExperience, AI, plus `Debug` (DEBUG-only, excluded from release)
    - `Optimizations/Models/` — `BaseOptimization`, `ApplyResult`, `OptimizationContext`, `OptimizationResult` and the per-feature item models
    - `Customize/Categories/` — Desktop, Gaming, Preferences, SystemFeatures
    - `Customize/Models/` — `BaseCustomizeSetting`, `RegistryToggle`, `RegistryBinding`, `SettingOption`, `CustomizeControlType`, `RecommendationState`
    - `Revert/` + `Revert/Steps/` — revert data and the persistable steps
    - `UI/` — UI-facing types only: risk/tag/order enums, `OptimizationState`, `SupportedLanguages`, `ChangeKindPresentation`, `ChangeDetailPresentation`
    - `Configuration/AppSettings.cs` — settings persisted to `appsettings.json` (`ShellTimeoutMs` default 120000)
  - `Services/` — business logic: `Conditions/ConditionEvaluator`, `Configuration/{ConfigManager,LanguageManager}`, `Customize/{CustomizeRegistry,CustomizationExecutor}`, `Optimization/{OptimizationRegistry,OptimizationService,OperationRunner,OptimizationValidation}`, `Revert/RevertManager`, `History/ChangeRecordStore`, `System/*`, `System/Primitives/*`, `UI/*`
  - `UI/` — XAML pages, ViewModels, windows, dialogs, controls, styles
  - `Common/` — `Helpers/` (`Shared`, `ReflectionHelper`, `CategoryDiscovery`, `WmiHelper`, `UserErrorSurface`, ...), `Converters/`, `Extensions/`
  - `ApplicationServiceCollectionExtensions.cs` — `AddOptimizerApplication(IConfiguration)`, the whole graph; `App.xaml.cs` only builds the host
  - `Resources/Languages/` — `Translations.resx` (neutral English) + one file per locale
- `optimizerDuck.Test/` — xUnit v3, one project, `InternalsVisibleTo`.
- Data directory `%LocalAppData%\optimizerDuck\`: `Revert/`, `History/`, `Resources/{Downloads,Assets}/`, `Crashes/`, `optimizerDuck.log`, `appsettings.json`.
- Version lives in `optimizerDuck.csproj` (`<Version>`). Read it there; never hardcode it.

## Adding an optimization or customize setting
Reflection discovery, no registration array to update.
- **Optimization**: a **nested class** in the relevant `Domain/Optimizations/Categories/*.cs` file, extending `BaseOptimization`, decorated `[Optimization(Id = "guid", Risk = ..., Tags = ..., Condition = typeof(...)?)]`. That is the only path.
- **Customize setting**: same nesting in `Domain/Customize/Categories/`, extending `BaseCustomizeSetting`, decorated `[CustomizeSetting(Section = ..., Icon = ..., Recommendation = ..., Condition = typeof(...)?)]`. `Icon` is required (`SymbolRegular`); `Recommendation` is `On` | `Off` | `Depends` | `Experimental` | `None`.
- **Category class**: `[OptimizationCategory(typeof(PageClass))]` or `[CustomizeCategory(PageType = typeof(PageClass))]`. Category pages auto-register via `AddAllOptimizationPages()` / `AddAllCustomizeCategoryPages()`.
- **Discovery**: `ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` scans assemblies whose name starts with `optimizerDuck`; `CategoryDiscovery` builds the categories and instantiates their nested items, skipping empty categories. Results are cached. `OptimizationValidation` fails fast on a duplicate or malformed Id during discovery.
- **Preloading**: `OptimizationRegistry.PreloadOptimizationsAsync()` / `EnsurePreloadedAsync()` and `CustomizeRegistry.PreloadCategoriesAsync()` / `EnsurePreloadedAsync()` run discovery on a background thread; `App.xaml.cs` preloads at startup and the pages await `EnsurePreloadedAsync()` before binding.
- **DI**: register through `AddOptimizerApplication(configuration)`. The host sets `ValidateOnBuild`/`ValidateScopes`, so a broken or scoped-into-singleton registration fails at startup, not at apply time. Keep it that way.
- **Results**: an optimization ends `ApplyAsync` with `return context.Changes.ToApplyResult();`. Only an early-out failure constructs `ApplyResult` by hand.

## Execution model
- `OpCall` is the explicit per-operation context: `Changes`, `Logger`, `CancellationToken`. `OptimizationContext` extends it with `Snapshot`, `StreamService`, `Shell`, `PowerPlans`.
- Providers record into `call.Changes` and return `OpResult`. Never both throw and record.
- `ChangeKind` says what a step did: `Change` (modified, carries compensation), `Skip` (already correct), `NotApplicable` (nothing here to act on), `Refused` (Windows refused; not a failure), `Irreversible` (no way back).
- `DidApplyAnything` = `Change` only, and gates revert persistence. `ModifiedSystem` = `Change` + `Irreversible`, and tells a change from nothing to do. So a run whose only step was irreversible is a success, and a run that recorded nothing is "nothing to do", not a failure.
- `OperationRunner` is the one lifecycle: run the work, classify the outcome from the recorded steps, write the record, persist revert data when the subject asks for it. `OperationSubject` (Id + Key + English log name) identifies a run and its record; built-in tool ids live in `ToolSubjects`.
- **Skip policy**: a provider whose mutation needs a privilege it may not have, has a side effect beyond the value it sets, or can be refused on a second call reads the current state first and records a `Skip` when the machine already matches, without calling the mutation (`RegistryService`, `ServiceProcessService`, `ScheduledTaskService`, `PowerPlanChanges`, and the PowerManagement category for hibernation/USB). An item never decides this on its own: a skip recorded at the item level drops the step from the record.
- **Failure policy**: a provider records a failed `Change` with an error and, when a retry can help, a retry action — it does not throw. Only unrecoverable state throws; the runner turns that into a failed step named for the user. Partial work is persisted with `CancellationToken.None`.
- **Retry**: `OptimizationService.RetryFailedStepsWithResultsAsync()` (static) re-invokes the stored retry action with a fresh `OpCall` and appends the recovered steps. The optimize page's dialog and `ToolRunPresenter` both call it. Reverting passes a `RevertManager`, a tool passes none, so a retried tool step writes no revert data.
- **The record**: `OperationRunner` writes `%LocalAppData%\optimizerDuck\History\{id}.json` (one file per subject, atomic, best effort, never part of the revert data). `ChangeRecordStore.TryWrite`/`TryRead` is the only accessor; the details view, the applied badge summary and a retry's attempt counts read it.
- **Tool actions**: `ToolRunPresenter.RunAsync(OperationRequest)` runs an action under the progress dialog, `ReportAsync` reports it (retry dialog for failed steps, snackbar for a failure with no retry). Every tool page uses it, so an action is run and reported one way.

## Providers
- `RegistryService`, `ServiceProcessService`, `ScheduledTaskService` — **static**, stateless; call directly.
- `ShellService` + `ProcessRunner` — DI singletons, reached from optimizations as `context.Shell`.
- `PowerPlanService` — DI singleton, the only owner of power-**scheme** interop; lean: reads take ids, writes take `ILogger? = null` and record nothing. `PowerPlanChanges` is the single edge that turns those results into `Change` + revert step + retry.
- `SystemRestoreService` — the only owner of every System Restore WMI call.
- `RecycleBinService`, `HibernationService`, `UsbPowerService` — static, one Windows operation each (shell Recycle Bin APIs, the documented power information callback, `root\wmi` device power); they fail open instead of throwing. The PowerManagement category records their steps.

## Conditions (compatibility gating)
- `Domain/Conditions/`: `ICondition`, `ConditionBase`, `ConditionResult`, `ConditionState`, `ConditionValidation`, `WindowsBuilds`, `BuiltIn/` — Windows 10, Windows 11, Windows 11 24H2+, CPU brand, GPU brand, minimum RAM, registry key exists, service exists, Recall installed.
- Evaluated by the static `Services/Conditions/ConditionEvaluator`. Both `[Optimization]` and `[CustomizeSetting]` accept an optional `Condition = typeof(T)` where `T : ICondition` with a public parameterless constructor.
- `ConditionResult.Available` / `.Unsupported(title, description)` / `.Error()`. Note the two providers are *factory* delegates (`() => Loc.Instance[...]`), so text is localized lazily. `Error` and an unknown `SystemInfo` **fail open** (never hide).
- Only `Unsupported` blocks, and only when the item is neither applied nor hidden by the user. `ConditionValidation.Validate(...)` fails fast on misconfigured metadata.

## Revert system
- One JSON file per subject: `%LocalAppData%\optimizerDuck\Revert\{id}.json`. Applied state is inferred from file presence.
- `RevertManager` is the API: `SaveRevertDataAsync`, `RevertAsync`, `AppendRevertStepAsync`, `RemoveRevertStepAtIndexAsync`, `RemoveRevertStepsAtIndexesAsync`, `RemoveRevertData`, plus static `IsAppliedAsync`, `GetRevertDataAsync`, `ClearAllRevertData`, `RemoveOrphanedTempFiles`.
- **Atomic writes**: temp file via `FileStream(WriteThrough)` + `Flush(flushToDisk: true)`, then `File.Replace`. Stale `.tmp` files are swept at startup.
- **Concurrency**: a per-file `SemaphoreSlim` (30 s timeout) in `RevertManager.FileLocks`. A lock entry is never disposed while another thread holds or waits on it.
- **Compact layout**: only successful steps persist, re-indexed with no gaps. Re-apply appends; a recovered retry step appends (never overwrites), so LIFO revert still ends at the original backup.
- **Step registry**: `_stepRegistry` is a lazily built name → `Func<JObject, IRevertStep>` map; a new step auto-registers by implementing `IRevertStep` with a public static `FromData(JObject)`. Persisted data with an unknown type becomes a failing step with a clear message, never a silent skip.
- **Steps** (`Domain/Revert/Steps/`): `RegistryRevertStep`, `ServiceRevertStep`, `ScheduledTaskRevertStep`, `ShellRevertStep`, `UsbPowerRevertStep`, `PowerPlanRevertStep`, `PowerSettingRevertStep`, `HibernationRevertStep`. Each verifies its own effect in `ExecuteAsync` (read-back, re-query, re-check, re-read). Access denied is failure, never success; missing required state throws (fail closed).
- `IRevertStep.ExecuteAsync(RevertContext, ILogger)`: `RevertContext` carries `Shell`, `PowerPlans` and the logger, so undo runs through the same DI services and the configured shell timeout.
- `RegistryRevertStep` actions: `RestorePrevious`, `NoPreviousValue`, `RestoreKey`, `RestoreKeyTree`; it tracks the subkeys it created so cleanup can remove them.

## Customize setting API
- `RegistryToggle` takes `OnValues` / `OffValues` (lists; `null` entry = key absent), `DefaultValue`, `IsOptional`, `ValueKind`. There is no `OnValue` / `OffValue` / `TreatMissingAsDefault`.
- Dropdown settings override `ControlType => CustomizeControlType.Dropdown` and `GetOptions()` returning `SettingOption`s built with `Option(key, regPath, regName, value)` or `Option(key, value, params RegistryBinding[])`. The base class auto-reads and auto-writes through the bindings and shows a "Custom" / "Not set" fallback for values outside the options.
- When you override `ApplyAsync`, call `await ExecutePostActionAsync()` yourself (it is guarded by `NeedsPostAction`).
- Setting writes go through `CustomizationExecutor.ApplyWithDebounceAsync(value, applyAction, debounceMs = 400)`: it debounces, then runs one queued apply at a time, so a burst of toggles applies only the last value. `CustomizeItemViewModel` is the caller.

## Localization
- `Domain/UI/SupportedLanguages.cs` is the single source of truth for the language list (17 entries, currently `en-US` … `id-ID`); the UI binds `SupportedLanguages.All`. A language exists only when its `Resources/Languages/Translations.{culture}.resx` also exists, and `LanguageManagerTests.EveryNeutralKey_ResolvesInEverySupportedLanguage` enforces that every neutral key resolves in every locale. Do not hardcode a language list anywhere else.
- `Translations.resx` (English) is the key source. Preserve `{0}` placeholders verbatim and in order, escape XML specials, keep `xml:space="preserve"`.
- Goal is natural localization, not word-for-word translation; each locale must read as if written by a native speaker who knows Windows, PC hardware and optimization software.
- Keep technical terms in English where that locale's users expect it (e.g. Vietnamese: SSD, HDD, RAM, CPU, GPU, Driver, Registry, BIOS, UEFI, PowerShell — never "Trình điều khiển" for Driver), and translate the concepts with a natural localized equivalent where one exists (e.g. vi "Memory Integrity" → "Tính toàn vẹn bộ nhớ"). Decide per locale; conventions differ per language.
- Never copy English sentence structure that sounds wrong in the target language, and never pad to match the source length. No em dashes in any locale — restructure with commas, periods, colons or parentheses natural to the language.
- Consistency order: correct meaning, then natural language, then established terminology, then cross-app consistency. The same source term may need different wording per context ("Startup" as manager vs. at startup).

## Tests (xUnit v3, integration style)
- **No mocking libraries.** Test doubles are hand-written (`StubOptimization`, `TestShell`, `TestRunner`, `EveryOperationDetail`, ...) implementing the real interfaces.
- **Real I/O**: real filesystem (revert JSON), real registry under `HKCU\Software\TestOptimizerDuck*`, real CMD/PowerShell processes.
- **STA**: a WPF-touching test uses a `private static RunInStaThreadAsync` helper (STA thread + `TaskCompletionSource`). There is no shared helper — the test class defines its own, so copy the existing one when a new class needs it.
- **Logging**: `NullLogger<T>.Instance` / `NullLoggerFactory.Instance`.
- **Naming**: `{Method}_{Scenario}_{ExpectedResult}`, e.g. `ApplyAsync_Success_PersistsRevertDataFile`.
- **Cleanup**: `try/finally` or `IDisposable` (`AppDataCleanupFixture`); a test that writes revert data removes both `{id}.json` and `{id}.json.tmp`.
- No coverage gate — cover the changed logic meaningfully, and add or update tests for what you change even when nobody asked.
- CI runs with `--blame-hang --blame-hang-timeout 30s`: no test may hang longer than 30 s.

## Style
- Nullable enabled, file-scoped namespaces, implicit usings. 4-space indent, max line length 100.
- PascalCase types and members, `_camelCase` private fields, `camelCase` locals and parameters.
- Comments are sparse in this codebase; do not add unnecessary ones.
- DI and MVVM via `Microsoft.Extensions.Hosting` + `CommunityToolkit.Mvvm`; pages and ViewModels are singletons.
- `WmiHelper.Initialize()` registers WMI cleanup for abnormal termination; keep the call on the startup path.
- Environment: `net10.0-windows`, WPF, `WPF-UI` + `WPF-UI.DependencyInjection` 4.3.0, `CommunityToolkit.Mvvm` 8.4.2, `Microsoft.Extensions.*` 10.0.10, `Newtonsoft.Json` 13.0.4, `Serilog` 4.4.0 (+ `Serilog.Extensions.Hosting` 10.0.0, `Serilog.Sinks.File` 7.0.0), `System.Management.Automation` 7.6.4, `TaskScheduler` 2.12.2. Tests: `xunit.v3` 3.2.2, `xunit.runner.visualstudio` 3.1.5, `Microsoft.NET.Test.Sdk` 18.8.1, `coverlet.collector` 10.0.1. Versions live in the two csproj files — check there rather than trusting this list.

## Commits and PRs
- Conventional Commits: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `i18n:`, `chore:`.
- Branch from `master`: `feature/<name>` or `fix/<issue-id>`.
- Run `csharpier format .`, then build and test, before committing. Never commit secrets or machine-specific paths.
- PR: clear description, linked issue (`Closes #123`), passing CI, screenshot for UI changes.

<!-- rtk-instructions v2 -->
# RTK — Always Prefix Shell Commands with `rtk`

**Every shell command must be prefixed with `rtk`** (RTK = Rust Token Killer). This filters and compresses output before it reaches the LLM context, saving 60-90% tokens. RTK passes through any command it doesn't recognize.

In a command chain, prefix each command individually:
```bash
rtk git add . && rtk git commit -m "msg" && rtk git push
```

Meta commands: `rtk gain` (savings stats), `rtk discover` (find missed opportunities), `rtk proxy <cmd>` (run raw for debugging).
<!-- /rtk-instructions -->
