# Repository Guidelines

## Project Structure & Module Organization
- `optimizerDuck/`: main WPF app (`net10.0-windows`), organized by responsibility:
  - `Domain/` for models/abstractions/attributes.
  - `Services/` for system, optimization, feature, and configuration logic.
  - `UI/` for XAML pages, dialogs, windows, controls, and ViewModels.
  - `Resources/` for images and localization (`Resources/Languages/*.resx`).
- `optimizerDuck.Test/`: xUnit v3 unit tests, mirroring service/execution areas.
- `.github/`: CI, issue templates, PR template, and project policies.
- `publish.bat`: interactive/preset publish entry point (`Portable` or `Single`).

## Build, Test, and Development Commands
- `dotnet restore optimizerDuck.slnx`: restore dependencies.
- `dotnet build optimizerDuck.slnx --configuration Release --no-restore`: CI-aligned build.
- `dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build`: run unit tests.
- `dotnet run --project optimizerDuck/optimizerDuck.csproj`: run locally.
- `publish.bat portable` or `publish.bat single --skip-tests`: create release artifacts.

## Coding Style & Naming Conventions
- Use modern C# with nullable references enabled; prefer DI over direct service construction.
- Indentation: 4 spaces; keep formatting consistent with existing file-scoped namespace style.
- Naming:
  - Types/methods/properties: `PascalCase`.
  - Private fields: `_camelCase`.
  - Locals/parameters: `camelCase`.
- Do not hardcode user-facing strings; add keys to `Translations.resx` and related locale files.

## Testing Guidelines
- Framework: xUnit v3 (`[Fact]` tests in `optimizerDuck.Test`).
- Name tests using behavior-focused patterns such as `ApplyAsync_Success_PersistsRevertDataFile`.
- Add/update tests for changes in `Services/`, optimization execution flow, and revert behavior.
- No explicit coverage gate is enforced in CI; prioritize meaningful unit coverage for changed logic.

## Commit & Pull Request Guidelines
- Follow Conventional Commits used in history: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `i18n:`, `chore:`.
- Branch from `master` (`feature/<name>` or `fix/<issue-id>`).
- PRs should include: clear description, linked issue (`Closes #123`), passing `dotnet build`/`dotnet test`, and screenshot/GIF for UI changes.

## Security & Configuration Tips
- Never commit secrets or machine-specific paths.
- For security issues, use private disclosure via `.github/SECURITY.md` guidance (not public issues).
