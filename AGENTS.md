# AGENTS.md

Repository instruction manual for AI coding agents. Windows-only WPF optimization utility.

## Hard Rules
- **Environment**: Windows-only, elevated (`app.manifest` requires admin). Target `net10.0-windows` with `UseWPF=true`. Deliberately no Windows SDK version in app TFM to avoid shipping the 50 MB CsWinRT projection dll.
- **Two Projects Only**: `optimizerDuck/` (app) and `optimizerDuck.Test/` (tests) in `optimizerDuck.slnx`. Never create top-level directories outside them.
- **Single Execution Path**: All system mutations go through `OperationRunner` and record into `ChangeSet`. Never add alternative execution frameworks.
- **Single Implementation**: One provider per Windows operation (`RegistryService`, `ServiceProcessService`, `ScheduledTaskService` are static and stateless). Fix behavior in the provider, never copy.
- **Zero Warnings**: `<TreatWarningsAsErrors>` and `<EnforceCodeStyleInBuild>` are active. Any compiler warning or style violation fails the build.
- **No Hardcoded Strings**: Use `Loc.Instance["Key"]` in C# and `Translations.KeyName` in XAML. Keys live in `Resources/Languages/Translations.resx`.

## Operational Commands
Prefix terminal commands with `rtk` (Rust Token Killer) when available.

- **Build**:
  ```powershell
  dotnet build optimizerDuck.slnx --configuration Release
  ```
- **Test All (.NET 10 MTP Runner)**:
  ```powershell
  dotnet test optimizerDuck.slnx --no-build -c Release
  # Or via console runner with long-running hung test detection:
  dotnet run --project optimizerDuck.Test/optimizerDuck.Test.csproj --no-build -c Release -- -longRunning 30
  ```
- **Test Filter (Class / Method)**:
  ```powershell
  # Using MTP CLI options (no double-dash needed in .NET 10):
  dotnet test optimizerDuck.slnx --no-build --filter-class "*<ClassName>*"
  dotnet test optimizerDuck.slnx --no-build --filter-method "*<MethodName>*"

  # Or directly via compiled test executable (fastest iteration):
  .\optimizerDuck.Test\bin\Release\net10.0-windows10.0.17763.0\optimizerDuck.Test.exe -class "*<ClassName>*"
  ```
  *Note: Legacy VSTest `--filter "FullyQualifiedName~..."` is obsolete; use MTP `--filter-class` or `--filter-method`.*
- **Formatting**:
  ```powershell
  csharpier check .
  csharpier format .
  ```
  *Run as `csharpier`, never `dotnet csharpier`.*
- **Local Run**: `dotnet run --project optimizerDuck/optimizerDuck.csproj` (requires elevated shell).
- **Full CI Gate**: `dotnet build optimizerDuck.slnx -c Release`, test with `-longRunning 30`, `csharpier check .`.

## Extension Patterns
- **Add Optimization**: Nested class in `Domain/Optimizations/Categories/*.cs`, extending `BaseOptimization`, decorated with `[Optimization(Id = "guid", Risk = ..., Tags = ...)]`. Auto-discovered via reflection.
- **Add Customize Setting**: Nested class in `Domain/Customize/Categories/`, extending `BaseCustomizeSetting`, decorated with `[CustomizeSetting(Section = ..., Icon = ..., Recommendation = ...)]`.
- **Apply Return**: End `ApplyAsync` with `return context.Changes.ToApplyResult();`. Providers record into `call.Changes` and return `OpResult`. Never both throw and record.
- **Skip Policy**: Check current state first; record `Skip` when already matching instead of re-applying mutations.

## Architecture & Revert
- **Data Directory**: `%LocalAppData%\optimizerDuck\` (`Revert/`, `History/`, `Resources/`, `appsettings.json`).
- **Revert**: One JSON per subject in `%LocalAppData%\optimizerDuck\Revert\{id}.json`. Atomic writes via temp file + `FileStream(WriteThrough)` + `File.Replace`. Concurrency guarded by per-file `SemaphoreSlim`.
- **Steps**: Persistable steps live in `Domain/Revert/Steps/`. Each verifies its own effect in `ExecuteAsync`. Missing state throws (fail closed); access denied is failure.

## Testing Guidelines
- **xUnit v3 + MTP**: Microsoft.Testing.Platform app (`global.json` specifies `"runner": "Microsoft.Testing.Platform"`).
- **No Mocking Libraries**: Use hand-written test doubles (`StubOptimization`, `TestShell`, `TestRunner`) implementing real interfaces.
- **Real I/O**: Real filesystem, real test registry keys under `HKCU\Software\TestOptimizerDuck*`. Clean up in `try/finally` or `IDisposable`.
- **WPF / STA**: WPF UI tests run through private `RunInStaThreadAsync` helper on STA thread.
- **Hang Timeout**: CI fails if any test hangs over 30s (`-longRunning 30`).

## Gotchas & Misleading Code
- **CETCompat**: `<CETCompat>false</CETCompat>` must remain set in `optimizerDuck.csproj` (Windows 10 lacks `QueueUserAPC2`, crashing with `0x80131506` if shadow stacks are active).
- **Language List**: `Domain/UI/SupportedLanguages.cs` is single source of truth (17 locales). Every neutral key in `Translations.resx` must resolve in all locales (`LanguageManagerTests` enforces this).
- **Localization Style**: Keep technical hardware terms in English (SSD, RAM, CPU, GPU, Registry, BIOS, Driver). Do not use em dashes in translations.

## Boundaries
- **Always**: Match existing MVVM patterns (`CommunityToolkit.Mvvm`), pass cancellation tokens, clean up test artifacts.
- **Ask First**: Modifying project dependencies, touching `optimizerDuck.slnx`, or altering core transaction engine (`OperationRunner`).
- **Never**: Commit secrets or hardcoded user paths, disable `<TreatWarningsAsErrors>`, or mutate UI controls outside dispatcher thread.
