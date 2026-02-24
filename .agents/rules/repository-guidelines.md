---
trigger: always_on
---

# Repository Guidelines

Welcome to the `optimizerDuck` repository! This document provides guidelines for contributing to the project, ensuring consistency and maintainability across the codebase.

## Project Structure & Module Organization

The repository is organized into two primary projects:

- **`optimizerDuck/`**: The core C# Windows application source.
  - `UI/`: Contains XAML views, pages, and components.
  - `Services/`: Houses business logic and system operations (e.g., `DiskCleanupService.cs`, `OptimizationService.cs`).
  - `Common/`: Shared utilities, helpers, and data converters.
  - `Resources/`: Application assets and multi-language `.resx` translation files.
- **`optimizerDuck.Test/`**: The xUnit test suite for validating application logic.

*To build the application, execute the `build.bat` script in the root directory or run `dotnet build optimizerDuck.slnx`.*

## Coding Style & Naming Conventions

This project targets modern .NET (C#) and uses XAML for the frontend presentation.

- **Classes & Methods**: Use `PascalCase` (e.g., `OptimizationService`, `GetSettings()`).
- **Variables & Parameters**: Use `camelCase` (e.g., `availableSpace`, `configData`).
- **Localization**: Avoid hardcoding UI text. Update the `.resx` files located in the `optimizerDuck/Resources/` directory for any new user-facing strings.
- **Linting & Formatting**: Follow standard C# conventions. The project's `.editorconfig` silently suppresses `CA1416` (Platform compatibility) warnings.

## Testing Guidelines

We utilize **xUnit** as our testing framework and `coverlet` for test coverage.

- **Test Placement**: Align test project paths with the main project layout (e.g., tests for `Services/` go into `optimizerDuck.Test/Services/`).
- **Naming Convention**: Suffix your test classes with `Tests` (e.g., `DiskCleanupServiceTests.cs`). Describe the behavior being tested clearly formatted in your test method names.
- **Running Tests**: You can execute the test suite via the .NET CLI:
  ```bash
  dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj
  ```

## Commit & Pull Request Guidelines

We follow the **Conventional Commits** format for our Git workflow. Structure your commit messages using the following prefixes:

- `feat:` for new capabilities (e.g., `feat: add disk cleanup page and its features`).
- `fix:` for bug resolutions (e.g., `fix: use proper color`).
- `refactor:` for restructurings or enhancements (e.g., `refactor: move converters to common`).

**When submitting a Pull Request:**
- Prepend the PR title with the appropriate conventional commit type.
- Ensure all recent changes compile successfully and all tests pass.
- Provide a brief description of the applied modifications. Link any relevant GitHub issues.
- Include a visual screenshot or GIF if your pull request modifies or creates UI elements.
