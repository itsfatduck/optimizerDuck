# optimizerDuck

## Project Overview

optimizerDuck is a free and open-source Windows optimization tool built with .NET 10.0. It provides a simple and intuitive command-line interface for applying various tweaks to improve system performance, privacy, and user experience. The tool is designed to be modular, allowing users to select which tweaks to apply. It also prioritizes safety by prompting users to create a system restore point before making any changes.

The application uses the following key technologies:

*   **.NET 10.0**: The core framework for the application.
*   **Spectre.Console**: A library for creating beautiful and interactive command-line interfaces.
*   **Serilog**: A flexible logging framework for .NET applications.
*   **Microsoft.PowerShell.SDK**: To interact with PowerShell for some of the tweaks.

The project is structured into two main parts:

*   **optimizerDuck**: The main application, containing the core logic, UI, and optimization tweaks.
*   **optimizerDuck.Test**: A separate project for unit tests.

## Building and Running

To build and run the project, you will need the .NET 10.0 SDK installed.

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/itsfatduck/optimizerDuck.git
    ```
2.  **Navigate to the project directory:**
    ```bash
    cd optimizerDuck
    ```
3.  **Build the project:**
    ```bash
    dotnet build
    ```
4.  **Run the project:**
    ```bash
    dotnet run --project optimizerDuck
    ```

## Development Conventions

*   **Coding Style**: The project follows the standard C# coding conventions.
*   **Unit Tests**: The project has a separate test project (`optimizerDuck.Test`) for unit tests.
*   **Logging**: The project uses Serilog for logging. Logs are written to a file in the `%APPDATA%/optimizerDuck/` directory.
*   **Contributions**: Contributions are welcome. Please read the `CONTRIBUTING.md` file for more information.
