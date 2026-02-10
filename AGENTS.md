# Repository Guidelines

## Project Structure & Module Organization
This is a WPF app solution rooted at `optimizerDuck-wpf.slnx`. The main app project is `optimizerDuck/`, and the test project is `optimizerDuck.Test/`. Key folders:
- `optimizerDuck/Core/`: core models, interfaces, and optimizers.
- `optimizerDuck/Common/`: shared helpers and extensions.
- `optimizerDuck/Services/`: app services and managers.
- `optimizerDuck/UI/ViewModels/` and `optimizerDuck/UI/Views/`: MVVM UI layer.
- `optimizerDuck/Resources/`: assets and localization (e.g., `Resources/Images`, `Resources/Languages`).

## Build, Test, and Development Commands
Run from the repo root:
- `dotnet restore optimizerDuck-wpf.slnx` — restore NuGet packages.
- `dotnet build optimizerDuck-wpf.slnx -c Release` — build the full solution.
- `dotnet run --project optimizerDuck/optimizerDuck.csproj` — run the WPF app locally.
- `dotnet test optimizerDuck-wpf.slnx` — run all tests (xUnit).

## Coding Style & Naming Conventions
- Language: C# with nullable reference types enabled (`net10.0-windows`).
- Indentation: 4 spaces for C# and XAML.
- Naming: PascalCase for types/methods, camelCase for locals, `I` prefix for interfaces.
- Architecture: MVVM; keep view logic in ViewModels and UI logic in Views.
- Toolkit: `CommunityToolkit.Mvvm` is used for view models and commands.

## Testing Guidelines
- Framework: xUnit (`optimizerDuck.Test/`).
- Naming: use `ClassNameTests` and place tests alongside feature folders (e.g., `optimizerDuck.Test/Services/`).
- Run: `dotnet test optimizerDuck-wpf.slnx`.

## Commit & Pull Request Guidelines
- Recent commits use a short prefix and colon, e.g., `Refactor: Improve registry & service error handling`.
- Keep messages concise and imperative; avoid single-letter messages.
- PRs should include a short summary, linked issue (if any), and screenshots for UI changes.

## Configuration & Safety Notes
- The app targets Windows (`net10.0-windows`) and runs as a WinExe; build on Windows.
- Optimization actions may change system settings; document new apply/revert steps in `Core/` and `Services/`, and surface them in the UI with clear labels.
