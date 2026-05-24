# Repository Guidelines

## Windows-Only WPF App
- **Windows-only**: Build/run/test only on Windows. Target framework: `net10.0-windows10.0.17763.0` with `UseWPF=true`. `CA1416` (platform compatibility) is silenced — all code is Windows-only.
- **Runs as admin**: `app.manifest` sets `requireAdministrator` UAC level.
- **Solution format**: `.slnx` (not `.sln`).

## Build, Test, Run Commands
- `dotnet restore optimizerDuck.slnx` — restore dependencies.
- `dotnet build optimizerDuck.slnx --configuration Release --no-restore` — CI-aligned build.
- `dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build` — run all tests.
- `dotnet run --project optimizerDuck/optimizerDuck.csproj` — run locally.
- `publish.bat portable` or `publish.bat single --skip-tests` — create release artifacts.

## Project Structure
- `optimizerDuck/` — WPF app:
  - `Domain/` — models, interfaces, attributes (no UI deps)
  - `Services/` — business logic: `Configuration/`, `Features/`, `Optimization/`, `Revert/`, `System/`
  - `UI/` — XAML pages, ViewModels, windows, controls, dialogs, styles
  - `Common/` — extensions, helpers, converters
  - `Resources/` — images, embedded assets, localization (`Resources/Languages/Translations.resx`)
- `optimizerDuck.Test/` — xUnit v3 tests

## Optimization & Feature Discovery (Reflection, No Manual Registration)
- **New optimizations**: Create a **nested class** inside the relevant category class (e.g., `Domain/Optimizations/Categories/Performance.cs`), extend `BaseOptimization`, and decorate with `[Optimization(Id = "guid", Risk = ..., Tags = ...)]`.
- **New feature toggles**: Same nesting pattern inside `Domain/Features/Categories/`, extend `BaseFeature`, decorate with `[Feature(Section = ..., Icon = ..., Recommendation = ...)]`.
- **Category classes**: Must be decorated with `[OptimizationCategory(PageType = typeof(...))]` or `[FeatureCategory(PageType = typeof(...))]`.
- **Discovery**: `ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` scans all `optimizerDuck.*` assemblies — no DI registration array to update.
- **Static provider services**: `RegistryService`, `ServiceProcessService`, `ScheduledTaskService`, `ShellService` are **static classes** (not DI-registered). They capture revert steps into the ambient `ExecutionScope`.

## Revert System
- **File-based**: Each applied optimization creates `Shared.RevertDirectory/{optimizationId}.json`. Applied state is inferred from file presence on disk.
- **Atomic writes**: `RevertManager` writes to `.tmp` then `File.Replace` for crash safety.
- **Step types**: `RegistryRevertStep`, `ServiceRevertStep`, `ScheduledTaskRevertStep`, `ShellRevertStep`, `UsbPowerRevertStep`.
- **`ExecutionScope`**: Uses `AsyncLocal<ExecutionScope?>` for ambient step tracking — no need to pass context through parameters.

## Coding & Style
- Nullable enabled, file-scoped namespaces, implicit usings.
- Indent: 4 spaces. PascalCase types/members, `_camelCase` private fields, `camelCase` locals/params.
- **No hardcoded strings** — use `Translations.resx` keys. Access via `Loc.Instance["Key"]` or `Translations.KeyName`.
- **Keep comments sparse** — existing code has none; do not add them.
- DI via `Microsoft.Extensions.Hosting` + `CommunityToolkit.Mvvm`. Pages + ViewModels registered as singletons.
- Prefer `dotnet_diagnostic.CA1416.severity = silent` in `.editorconfig` (already set). Do not add `SupportedOSPlatform` guards.

## Testing (xUnit v3, Integration-Style)
- **No mocking libraries** — all test doubles are hand-written (`FakeOptimization`, `TestOptimization`, etc.) implementing interfaces directly.
- **Real I/O**: Tests use real filesystem (revert JSON files), real registry (`HKCU\Software\TestOptimizerDuck*`), real process execution (CMD/PowerShell).
- **STA thread**: Tests involving `ContentDialogService` or WPF components must use `RunInStaThreadAsync` helper (STA thread + `TaskCompletionSource`).
- **Logging**: Use `NullLogger<T>.Instance` / `NullLoggerFactory.Instance` for DI logging parameters.
- **Test naming**: `{Method}_{Scenario}_{ExpectedResult}` (e.g., `ApplyAsync_Success_PersistsRevertDataFile`).
- **Cleanup**: Use `try/finally` or `IDisposable` for test artifact cleanup (revert files, registry keys).
- **Coverage**: No coverage gate enforced — prioritize meaningful unit coverage for changed logic.

## Commit & PR
- Conventional Commits: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `i18n:`, `chore:`.
- Branch from `master`: `feature/<name>` or `fix/<issue-id>`.
- PRs: clear description, linked issue (`Closes #123`), passing `dotnet build` + `dotnet test`, screenshot for UI changes.
- Never commit secrets or machine-specific paths.
