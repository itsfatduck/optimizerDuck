# Contributing to optimizerDuck

First off, thank you for considering contributing to optimizerDuck! We're thrilled you're here. This project is open to everyone, and we welcome any contributions, from fixing typos to implementing new features. Your help is essential for making optimizerDuck the best Windows optimization tool it can be.

Our mission is to provide a free, open-source, and powerful tool that enhances system performance, privacy, and simplicity for everyone.

## Code of Conduct

This project and everyone participating in it is governed by the [optimizerDuck Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to the project maintainers via GitHub Issues or on our Discord server.

## How Can I Contribute?

### Reporting Bugs

Bugs are tracked as [GitHub Issues](https://github.com/itsfatduck/optimizerDuck/issues). Before creating a bug report, please check the existing issues to see if someone has already reported it.

When creating a bug report, please include as many details as possible. Fill out the required template, which will help us resolve issues faster.

**Bug Report Template**:
```markdown
**Describe the bug**
A clear and concise description of what the bug is.

**To Reproduce**
Steps to reproduce the behavior:
1. Go to '...'
2. Click on '....'
3. Scroll down to '....'
4. See error

**Expected behavior**
A clear and concise description of what you expected to happen.

**Screenshots**
If applicable, add screenshots to help explain your problem.

**Environment**
- OS: [e.g., Windows 11 Pro 23H2]
- Version: [e.g., 1.3.0]

**Logs**
Please include any relevant logs. You can find the log file at `%APPDATA%\optimizerDuck\optimizerDuck.log`.
```

### Suggesting Features

We love to hear your ideas for improving optimizerDuck! Feature suggestions are tracked as [GitHub Issues](https://github.com/itsfatduck/optimizerDuck/issues). Use the "Feature Request" template and provide a clear explanation of your suggestion.

**Feature Request Template**:
```markdown
**Is your feature request related to a problem?**
A clear and concise description of what the problem is. Ex. I'm always frustrated when [...]

**Describe the solution you'd like**
A clear and concise description of what you want to happen.

**Describe alternatives you've considered**
A clear and concise description of any alternative solutions or features you've considered.

**Additional context**
Add any other context or screenshots about the feature request here.
```

### Pull Requests

Pull requests are the best way to propose changes to the codebase. We actively welcome your pull requests.

1.  Fork the repo and create your branch from `master`.
2.  If you've added code that should be tested, add tests.
3.  If you've changed APIs, update the documentation.
4.  Ensure the test suite passes.
5.  Make sure your code lints.
6.  Issue that pull request!

## Development Setup

Ready to contribute? Here’s how to set up optimizerDuck for local development.

#### Prerequisites

*   [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
*   Git

#### Setup Instructions

```bash
# 1. Clone the repository
git clone https://github.com/itsfatduck/optimizerDuck.git

# 2. Navigate to the project directory
cd optimizerDuck

# 3. Build the project to restore dependencies
dotnet build

# 4. Run the tests to ensure everything is working
dotnet test

# 5. Run the application
dotnet run --project optimizerDuck
```

## Development Workflow

#### Branch Naming

To keep our branch structure organized, please follow this convention:
> **Note:** Current repo doesn't follow this convention fully, but this is the direction for organizing branches in the future.

*   `feature/<description>` for new features (e.g., `feature/add-new-tweak`)
*   `fix/<description>` for bug fixes (e.g., `fix/resolve-crash-on-start`)
*   `docs/<description>` for documentation changes (e.g., `docs/update-readme`)
*   `refactor/<description>` for code refactoring (e.g., `refactor/optimize-registry-service`)
*   `chore/<description>` for build scripts, tooling, etc. (e.g., `chore/update-dependencies`)

#### Commit Messages

We follow the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) specification. This makes the commit history easier to read and helps with automating changelogs.

A commit message should be structured as follows:

```
type(scope): subject

body

footer
```

**Types**: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`

**Examples**:

```
feat(Optimizers): add new tweak to disable Superfetch
```

```
fix(UI): correct typo in the main menu prompt
```

```
docs(CONTRIBUTING): add development setup instructions
```

## Coding Standards

#### Style Guide

*   We follow the standard C# and .NET coding conventions.
*   Code formatting rules are defined in the `.editorconfig` file. Please ensure your editor respects these settings.
*   Use meaningful names for variables, methods, and classes.

#### Best Practices

*   Write self-documenting code where possible.
*   Add XML comments (`///`) for public methods and complex logic.
*   Keep functions and methods small and focused on a single responsibility.
*   Aim for clarity and simplicity.

## Testing

#### Writing Tests

*   New features should be accompanied by unit tests.
*   Bug fixes should include a test case that reproduces the bug and verifies the fix.
*   Tests are located in the `optimizerDuck.Test` project.

#### Running Tests

You can run all tests from the root directory:

```bash
# Run all tests
dotnet test

# Run tests in watch mode
dotnet watch test
```

## Documentation

#### Code Documentation

*   Public APIs, classes, and methods should be documented using XML comments.
*   Explain complex algorithms or business logic with comments in the code.

#### README Updates

*   If you add or change a feature, please update the `README.md` to reflect the changes.
*   Ensure any new tweaks are listed in the "Features" section.

## Pull Request Process

1.  **Before Submitting**
    *   Run `dotnet test` to ensure all tests pass locally.
    *   Ensure your code adheres to the coding standards.
    *   Update the `README.md` and any other relevant documentation.
    *   Self-review your changes to catch any obvious issues.

2.  **PR Description Template**
    Please fill out the pull request template to help reviewers understand your contribution.

    ```markdown
    ## Description
    A brief description of the changes in this pull request.

    ## Type of Change
    - [ ] Bug fix (non-breaking change which fixes an issue)
    - [ ] New feature (non-breaking change which adds functionality)
    - [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
    - [ ] Documentation update
    - [ ] Refactor

    ## Related Issues
    Fixes #(issue number)

    ## Testing
    - [ ] All tests pass locally with my changes.
    - [ ] I have added tests that prove my fix is effective or that my feature works.
    - [ ] I have updated existing tests to accommodate my changes.

    ## Checklist
    - [ ] My code follows the style guidelines of this project.
    - [ ] I have performed a self-review of my own code.
    - [ ] I have commented on my code, particularly in hard-to-understand areas.
    - [ ] I have made corresponding changes to the documentation.
    - [ ] My changes generate no new warnings.
    ```

3.  **Review Process**
    *   One or more maintainers will review your pull request.
    *   Please be responsive to feedback and questions.
    *   Once your PR is approved, it will be merged into the `master` branch.

## Community

#### Getting Help

*   **Discord:** Join our [Discord Server](https://discord.gg/tDUBDCYw9Q) to chat with the community and get help.
*   **GitHub Discussions:** Use [GitHub Discussions](https://github.com/itsfatduck/optimizerDuck/discussions) for questions, ideas, and to show off your results.

## License

By contributing to optimizerDuck, you agree that your contributions will be licensed under the [CC BY-NC-SA 4.0 License](./LICENSE).
