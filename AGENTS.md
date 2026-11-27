# AGENTS.md

Repository guide for AI coding agents working on **optimizerDuck**, an elevated Windows-only WPF optimization utility. Keep changes minimal, evidence-backed, and verified against the build and test gates below.

## Core Invariants
- **Target & Packaging**: Windows-only, elevated (`app.manifest`). App targets `net10.0-windows` (`UseWPF=true`) with **no** Windows SDK version in the TFM (avoids shipping the ~50 MB CsWinRT projection DLL). Keep `<CETCompat>false</CETCompat>` in `optimizerDuck/optimizerDuck.csproj` so Windows 10 does not crash with `0x80131506` (`QueueUserAPC2` is Windows 11+ only).
- **Two Projects Only**: `optimizerDuck/` (app) and `optimizerDuck.Test/` (tests) in `optimizerDuck.slnx`. Never create extra top-level project directories.
- **Single Execution Path**: Every system mutation runs through `OperationRunner` and records into `ChangeSet`. Never add parallel execution pipelines.
- **Single Provider Per Operation**: `RegistryService`, `ServiceProcessService`, and `ScheduledTaskService` are static and stateless. Fix behavior inside the provider; never copy-paste mutation logic.
- **Zero Warnings**: `<TreatWarningsAsErrors>` and `<EnforceCodeStyleInBuild>` are enabled. Any compiler warning or analyzer violation fails the build.
- **No Hardcoded UI Strings**: Use `Loc.Instance["Key"]` in C# and `Translations.KeyName` in XAML.

## Build, Test & Verification Commands
Prefix commands with `rtk` when available. In Windows PowerShell 5.1, chain commands with `; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }` instead of `&&`.

- **CI Gate** (matches `.github/workflows/ci.yml`):
  ```powershell
  dotnet restore optimizerDuck.slnx
  dotnet build optimizerDuck.slnx --configuration Release --no-restore
  dotnet run --project optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build -- -longRunning 30
  ```
- **Fast Filtered Tests** (.NET 10 Microsoft.Testing.Platform; legacy VSTest `--filter "FullyQualifiedName~..."` is obsolete):
  ```powershell
  dotnet test optimizerDuck.slnx --no-build -c Release --filter-class "*<ClassName>*"
  dotnet test optimizerDuck.slnx --no-build -c Release --filter-method "*<MethodName>*"
  # Fastest iteration via compiled test host:
  .\optimizerDuck.Test\bin\Release\net10.0-windows10.0.17763.0\optimizerDuck.Test.exe -class "*<ClassName>*"
  ```
- **Localization Sync Audit**:
  ```powershell
  python .agents/skills/resx-translation/helpers/resx_audit.py optimizerDuck/Resources/Languages --check-designer --no-untranslated
  ```
- **Formatting** (run `csharpier`, never `dotnet csharpier`, when installed):
  ```powershell
  csharpier format .
  csharpier check .
  ```
- **Local App Run**: `dotnet run --project optimizerDuck/optimizerDuck.csproj` (elevated shell required).

## Windows Tweaks: Research & Cross-Version Rules
- **Verify Before Editing**: Never guess registry keys, value types/data, services, scheduled tasks, or ETW AutoLoggers from names or generic forum snippets. Confirm exact behavior and side effects using official Microsoft documentation (Microsoft Learn, Policy CSP, ADMX, Win32) and/or inspecting real Windows binaries, registry dependencies (e.g., WDI modules, service DLLs, ACLs), and live OS behavior.
- **Cover Windows 10 (1507 / 10240) Through Windows 11 (26H2+ / 26300+)**: Never delete a working legacy Windows 10 key simply because modern Windows 11 builds ignore, deprecate, or Capability-ACE-lock it (such as `HKCU\Software\Microsoft\Windows\CurrentVersion\Search`). Branch targets by OS build using `Domain/Conditions/WindowsBuilds.cs` and `Common/Helpers/Shared.cs` (`IsWindows10_2004OrGreater`, `IsWindows11OrGreater`).
- **Testable OS-Branching Builders**: Extract OS-dependent target lists into pure `internal static` helpers (`BuildRegistryItems(...)`, `BuildRegistryToggles(...)`) so unit tests verify every Windows build tier without mutating the host machine.
- **Apply / Probe / Revert Parity**: Apply writes, revert backups, and state probes (`IsSystemAtTargetAsync`, `GetStateAsync`) must evaluate the exact same OS-scoped targets so items never falsely show "Already Optimal" or get stuck on an OS-locked key.
- **Isolate Breaking Side Effects**: If a single key, AutoLogger, or service in a bundle breaks visible Windows functionality (for example, `Autologger\DiagLog` stopping `DPS` from loading `srumsvc.dll` and causing Task Manager's per-process Network column to show `0 Mbps`), split it into a separate opt-in optimization with explicit `Risk`, `RequiresRestart`, and an accurate `TradeOff`.
- **Honor Skip Policy**: Probe current system state first; record `Skip` when already at target instead of re-writing identical values.

## Architecture & Extension Contracts
- **Optimizations**: Nested class in `optimizerDuck/Domain/Optimizations/Categories/*.cs` extending `DeclarativeOptimization` (preferred for static/OS-branched registry, service, and task targets; auto-handles `SetsGroupPolicy`, probing, and apply) or `BaseOptimization`, decorated with `[Optimization(Id = "guid", Risk = ..., Tags = ...)]`. Auto-discovered via reflection.
- **Customize Settings**: Nested class in `optimizerDuck/Domain/Customize/Categories/*.cs` extending `BaseCustomizeSetting`, decorated with `[CustomizeSetting(Section = ..., Icon = ..., Recommendation = ...)]`. Keep `GetStateAsync` aligned with required (`!IsOptional`) toggles.
- **Apply Contract**: Custom `ApplyAsync` methods end with `return context.Changes.ToApplyResult();`. Providers record into `call.Changes` and return `OpResult` — never both throw and record.
- **Revert & Data Storage**: Runtime data lives in `%LocalAppData%\optimizerDuck\` (`Revert/`, `History/`, `Resources/`, `appsettings.json`). Revert files (`Revert/{id}.json`) use atomic temp-file + `FileStream(WriteThrough)` + `File.Replace` guarded by per-file `SemaphoreSlim`. Steps in `Domain/Revert/Steps/` must verify their effect in `ExecuteAsync` (fail closed on missing state; access denied is failure).

## Localization Contract
- **Supported Languages**: `Domain/UI/SupportedLanguages.cs` is the single source of truth (neutral `en-US` in `Translations.resx` plus one satellite `.resx` per supported locale in `optimizerDuck/Resources/Languages/`). Whenever adding or removing a language, keep `SupportedLanguages.cs`, `<SatelliteResourceLanguages>` in `optimizerDuck/optimizerDuck.csproj`, and `Translations.Designer.cs` in sync.
- **Automated Validation**: `LanguageManagerTests` enforces every neutral key resolves across all locales. `OptimizationValidationTests` enforces `ShortDescription`, `TradeOff` (for `Moderate`/`Risky` or `ImpactDirection.TradeOff`), `Metadata.Impacts`, `https://` `Metadata.Sources`, and rejects leaked internal tokens (`=0`, `=1`, `HKLM\`, `HKCU\`, etc.) in copy.
- **Style**: Keep technical terms in English (SSD, RAM, CPU, GPU, Registry, BIOS, Driver, WDI, SRUM, Task Manager). Do not use em dashes (`—`) in translations. Use `.agents/skills/resx-translation/SKILL.md` helpers rather than reading full `.resx` files.

## Testing Rules & Known Footguns
- **No Mocking Libraries**: Write lightweight test doubles (`StubOptimization`, `TestShell`, `TestRunner`) implementing real interfaces.
- **Real I/O Hygiene**: Tests use real filesystem temp paths and `HKCU\Software\TestOptimizerDuck*`. Always clean up in `try/finally` or `IDisposable`.
- **WPF / STA Safety**: Run WPF UI tests via `RunInStaThreadAsync`. **Never call `new Application()` in tests** — it permanently binds process-global `Application.Current` to one STA thread and breaks subsequent UI tests (such as `OptimizeViewModel`). Load resources into a `Window` or `ResourceDictionary` instead (initialize `PackUriHelper.UriSchemePack` if resolving `pack://` URIs).
- **Hang Guard**: Any test hanging past 30s fails CI (`-longRunning 30`).

## Progressive Disclosure Pointers
Consult these only when relevant to the task:
- `.agents/skills/` — repo skills for `.resx` localization, WPF/WinUI, C# async/docs/xUnit, and PowerShell Windows patterns.
- `optimizerDuck/Domain/Conditions/WindowsBuilds.cs` — OS build constants and compatibility conditions.
- `optimizerDuck.Test/Services/OptimizationValidationTests.cs` — authoritative validation rules for all optimizations and metadata.
- `.github/workflows/ci.yml` — exact CI build and test steps.

## Boundaries
- **Always**: Match `CommunityToolkit.Mvvm` patterns, pass `CancellationToken`, verify changes with build + tests before finishing, and clean up test artifacts.
- **Ask First**: Changing NuGet dependencies, editing `optimizerDuck.slnx`, altering `OperationRunner` / core revert semantics, or committing/pushing/closing issues.
- **Never**: Commit secrets or machine-specific paths, disable `<TreatWarningsAsErrors>`, mutate UI controls off the dispatcher thread, or touch unrelated git stashes.
