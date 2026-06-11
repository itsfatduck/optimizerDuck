# Repository Guidelines

## Windows-Only WPF App
- **Windows-only**: Build/run/test only on Windows. Target framework: `net10.0-windows10.0.17763.0` with `UseWPF=true`. `CA1416` (platform compatibility) is silenced — all code is Windows-only.
- **Runs as admin**: `app.manifest` sets `requireAdministrator` UAC level. Tests that modify system settings or registry need admin too.
- **Solution format**: `.slnx` (not `.sln`).
- **Data directory**: `%LocalAppData%\optimizerDuck\` — holds revert files, resources, downloads, assets.

## Build, Test, Run Commands
- `dotnet restore optimizerDuck.slnx` — restore dependencies.
- `dotnet build optimizerDuck.slnx --configuration Release --no-restore` — CI-aligned build.
- `dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build` — run all tests.
- `dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build --filter "FullyQualifiedName~TestName"` — run a single test.
- `dotnet run --project optimizerDuck/optimizerDuck.csproj` — run locally.
- `publish.bat portable` or `publish.bat single --skip-tests` — create release artifacts.

## Project Structure
- `optimizerDuck/` — WPF app (single project, no sub-projects):
  - `Domain/` — models, interfaces, attributes (no UI deps)
  - `Services/` — business logic: `Configuration/`, `Customize/`, `Optimization/`, `Revert/`, `System/`, `UI/`
  - `UI/` — XAML pages, ViewModels, windows, controls, dialogs, styles
  - `Common/` — extensions, helpers, converters
  - `Resources/` — images, embedded assets, localization (`Resources/Languages/Translations.resx`)
- `optimizerDuck.Test/` — xUnit v3 tests (single test project)
- Do **not** create top-level directories outside these two project folders.

## Key NuGet Packages
- `CommunityToolkit.Mvvm` 8.x — MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`)
- `WPF-UI` + `WPF-UI.DependencyInjection` 4.x — Fluent Design controls + navigation
- `Microsoft.Extensions.Hosting` 10.x — DI / hosted service wiring
- `xunit.v3` 3.x — test framework (with global using `Xunit` in test csproj)

## Optimization & Customize Discovery (Reflection, No Manual Registration)
- **New optimizations**: Create a **nested class** inside the relevant category class (e.g., `Domain/Optimizations/Categories/Performance.cs`), extend `BaseOptimization`, decorate with `[Optimization(Id = "guid", Risk = ..., Tags = ...)]`.
- **New customize settings**: Same nesting pattern inside `Domain/Customize/Categories/`, extend `BaseCustomizeSetting`, decorate with `[CustomizeSetting(Section = ..., Icon = ..., Recommendation = ...)]`. `Icon` is required (`SymbolRegular` enum). `Section` can be a string or enum value.
- **Category classes**: Decorate with `[OptimizationCategory(typeof(PageClass))]` or `[CustomizeCategory(PageType = typeof(PageClass))]`.
- **Discovery**: `ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` scans assemblies whose name starts with `optimizerDuck` — no DI registration array to update.
- **Static provider services**: `RegistryService`, `ServiceProcessService`, `ScheduledTaskService`, `ShellService` are **static classes** (not DI-registered). They capture revert steps into the ambient `ExecutionScope`.
- **`CompleteFromScope()`**: Optimizations call `BaseOptimization.CompleteFromScope()` to build the `ApplyResult` from steps recorded in the ambient `ExecutionScope`. Do not manually construct `ApplyResult`.

## Revert System
- **File-based**: Each applied optimization creates `%LocalAppData%\optimizerDuck\Revert\{optimizationId}.json`. Applied state is inferred from file presence on disk.
- **Atomic writes**: `RevertManager` writes to `.tmp` then `File.Replace` for crash safety.
- **Step types**: `RegistryRevertStep`, `ServiceRevertStep`, `ScheduledTaskRevertStep`, `ShellRevertStep`, `UsbPowerRevertStep`.
- **`ExecutionScope`**: Uses `AsyncLocal<ExecutionScope?>` for ambient step tracking — no need to pass context through parameters.

## Coding & Style
- Nullable enabled, file-scoped namespaces, implicit usings.
- Indent: 4 spaces. PascalCase types/members, `_camelCase` private fields, `camelCase` locals/params.
- Max line length: 100 characters (enforced by `.editorconfig`).
- **No hardcoded UI strings** — use `Loc.Instance["Key"]` (C#) or `Translations.KeyName` (XAML bindings). Add new keys to `Resources/Languages/Translations.resx`.
- **Keep comments sparse** — existing code has none; do not add them.
- DI via `Microsoft.Extensions.Hosting` + `CommunityToolkit.Mvvm`. Pages + ViewModels registered as singletons.
- `optimizerDuck.csproj` has `<InternalsVisibleTo Include="optimizerDuck.Test" />` — test project can access internal members.

## Testing (xUnit v3, Integration-Style)
- **No mocking libraries** — all test doubles are hand-written (`FakeOptimization`, `TestOptimization`, etc.) implementing interfaces directly.
- **Real I/O**: Tests use real filesystem (revert JSON files), real registry (`HKCU\Software\TestOptimizerDuck*`), real process execution (CMD/PowerShell).
- **STA thread**: Tests involving WPF components must use `RunInStaThreadAsync` helper (STA thread + `TaskCompletionSource`). The test project itself defines this helper — add it if missing.
- **Logging**: Use `NullLogger<T>.Instance` / `NullLoggerFactory.Instance` for DI logging parameters.
- **Test naming**: `{Method}_{Scenario}_{ExpectedResult}` (e.g., `ApplyAsync_Success_PersistsRevertDataFile`).
- **Cleanup**: Use `try/finally` or `IDisposable` for test artifact cleanup (revert files, registry keys).
- **No coverage gate** — prioritize meaningful unit coverage for changed logic.
- **CI test command** uses `--blame-hang --blame-hang-timeout 30s` — tests must not hang longer than 30s.

## Commit & PR
- Conventional Commits: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `i18n:`, `chore:`.
- Branch from `master`: `feature/<name>` or `fix/<issue-id>`.
- PRs: clear description, linked issue (`Closes #123`), passing CI (build + test), screenshot for UI changes.
- Never commit secrets or machine-specific paths.

<!-- rtk-instructions v2 -->
# RTK — Always Prefix Shell Commands with `rtk`

**Every shell command must be prefixed with `rtk`** (RTK = Rust Token Killer). This filters and compresses output before it reaches the LLM context, saving 60-90% tokens. RTK passes through any command it doesn't recognize.

In a command chain, prefix each command individually:
```bash
rtk git add . && rtk git commit -m "msg" && rtk git push
```

Meta commands: `rtk gain` (savings stats), `rtk discover` (find missed opportunities), `rtk proxy <cmd>` (run raw for debugging).
<!-- /rtk-instructions -->
