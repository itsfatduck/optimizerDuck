# Contributing to optimizerDuck

First of all, thank you for taking the time to contribute! 🎉 
We want to make contributing to `optimizerDuck` as easy, transparent, and welcoming as possible.

## 💬 Where to Get Help
If you have questions, need help, or want to discuss a feature, feel free to reach out to the author directly:
- **Discord**: contact via project server (link in README)
- **Email**: itsfatduck@gmail.com
- **GitHub Issues**: Open a discussion or an issue.

## 🛠️ Setting Up the Project

1. **Clone the repository:**
   ```bash
   git clone https://github.com/itsfatduck/optimizerDuck.git
   cd optimizerDuck
   ```
2. **Build the project:**
   Execute the `build.bat` script in the root directory or run:
   ```bash
   dotnet build optimizerDuck.slnx
   ```

## 🏗️ Project Structure & Module Organization

To help you get started quickly, here is how the code is organized:

- **`optimizerDuck/`**: The core C# Windows application source.
  - `UI/`: Contains XAML views, pages, and components.
  - `Services/`: Houses business logic and system operations (e.g., `DiskCleanupService.cs`, `OptimizationService.cs`).
  - `Common/`: Shared utilities, helpers, and data converters.
  - `Resources/`: Application assets and multi-language `.resx` translation files.
- **`optimizerDuck.Test/`**: The xUnit test suite for validating application logic.

## 📝 Coding Style & Naming Conventions

This project targets modern .NET (C#) and uses XAML for the frontend presentation.

- **Classes & Methods**: Use `PascalCase` (e.g., `OptimizationService`, `GetSettings()`).
- **Variables & Parameters**: Use `camelCase` (e.g., `availableSpace`, `configData`).
- **Linting & Formatting**: Follow standard C# conventions. The project's `.editorconfig` silently suppresses `CA1416` (Platform compatibility) warnings.

## 🌍 Localization Guidelines

optimizerDuck supports multiple languages. If you are adding new features or modifying the UI, please observe the following rules:

1. **No Hardcoded Strings**: Never hardcode user-facing text directly in XAML or C# code.
2. **Resource Files**: All application texts are securely managed in `.resx` files inside `optimizerDuck/Resources/Languages/`.
   - `Translations.resx`: The primary English file. **Always** add your new keys and texts here first.
   - `Translations.[lang].resx`: Files for other supported languages (e.g., `vi-VN`, `zh-Hans`, `ru-RU`).
3. **Adding New Strings**: 
   - Please add any new resource keys to **all** existing `.resx` files.
   - If you do not speak a specific language, you can use machine translation (like Google Translate or AI) as a placeholder. The community will help correct it later!
4. **Using Translations in XAML**:
   Always use the custom extension to bind text:
   ```xaml
   Text="{ext:Loc Your.Translation.Key}"
   ```
## 🧪 Testing Guidelines

We utilize **xUnit** as our testing framework and `coverlet` for test coverage.

- **Test Placement**: Align test project paths with the main project layout (e.g., tests for `Services/` go into `optimizerDuck.Test/Services/`).
- **Naming Convention**: Suffix your test classes with `Tests` (e.g., `DiskCleanupServiceTests.cs`). Describe the behavior being tested clearly formatted in your test method names.
- **Running Tests**: You can execute the test suite via the .NET CLI:
  ```bash
  dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj
  ```

## 🚀 Commit & Pull Request Guidelines

We follow the **Conventional Commits** format for our Git workflow. Please structure your commit messages using these prefixes:

- `feat:` for new capabilities (e.g., `feat: add disk cleanup page and its features`).
- `fix:` for bug resolutions (e.g., `fix: use proper color`).
- `refactor:` for restructurings or enhancements (e.g., `refactor: move converters to common`).

**When submitting a Pull Request:**
1. Prepend the PR title with the appropriate conventional commit type.
2. Ensure all recent changes compile successfully and all tests pass.
3. Provide a brief description of the applied modifications. Link any relevant GitHub issues.
4. Include a visual screenshot or GIF if your pull request modifies or creates UI elements.

Happy optimizing! 🦆
