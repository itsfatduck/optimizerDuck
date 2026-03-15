@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
set "PROJECT_PATH=%SCRIPT_DIR%optimizerDuck\optimizerDuck.csproj"
set "TEST_PROJECT=%SCRIPT_DIR%optimizerDuck.Test\optimizerDuck.Test.csproj"
set "CONFIGURATION=Release"
set "PROFILE="
set "RUN_TESTS=1"
set "PAUSE_ON_EXIT=1"

call :parse_args %*
if errorlevel 1 goto :fail

pushd "%SCRIPT_DIR%" >nul || (
    echo Failed to open repository root.
    goto :fail
)

call :ensure_dotnet || goto :cleanup_fail

if not exist "%PROJECT_PATH%" (
    echo Project file not found: "%PROJECT_PATH%"
    goto :cleanup_fail
)

if not defined PROFILE call :choose_profile
if not defined PROFILE goto :cleanup_fail

echo.
echo ========================================
echo optimizerDuck publish
echo ========================================
echo Profile:       %PROFILE%
echo Configuration: %CONFIGURATION%
if "%RUN_TESTS%"=="1" (
    echo Run tests:     Yes
) else (
    echo Run tests:     No
)

if "%RUN_TESTS%"=="1" (
    echo.
    echo [1/2] Running tests...
    dotnet test "%TEST_PROJECT%" -c %CONFIGURATION% --nologo
    if errorlevel 1 (
        echo.
        echo Tests failed. Publish stopped.
        goto :cleanup_fail
    )
) else (
    echo.
    echo [1/2] Skipping tests...
)

echo.
echo [2/2] Publishing %PROFILE% profile...
dotnet publish "%PROJECT_PATH%" -c %CONFIGURATION% --nologo /p:PublishProfile=%PROFILE%
if errorlevel 1 (
    echo.
    echo Publish failed.
    goto :cleanup_fail
)

echo.
echo Publish completed successfully.
goto :cleanup_success

:parse_args
if "%~1"=="" exit /b 0

if /I "%~1"=="portable" (
    set "PROFILE=Portable"
    shift
    goto :parse_args
)

if /I "%~1"=="single" (
    set "PROFILE=Single"
    shift
    goto :parse_args
)

if /I "%~1"=="--portable" (
    set "PROFILE=Portable"
    shift
    goto :parse_args
)

if /I "%~1"=="--single" (
    set "PROFILE=Single"
    shift
    goto :parse_args
)

if /I "%~1"=="--skip-tests" (
    set "RUN_TESTS=0"
    shift
    goto :parse_args
)

if /I "%~1"=="--no-pause" (
    set "PAUSE_ON_EXIT=0"
    shift
    goto :parse_args
)

if /I "%~1"=="--help" goto :help
if /I "%~1"=="-h" goto :help

echo Unknown argument: %~1
echo Use --help to see available options.
exit /b 1

:choose_profile
echo.
echo Available publish profiles:
echo   1. Portable
echo   2. Single
set /p "choice=Choose publish profile (1/2): "

if "%choice%"=="1" (
    set "PROFILE=Portable"
    exit /b 0
)

if "%choice%"=="2" (
    set "PROFILE=Single"
    exit /b 0
)

echo Invalid choice.
exit /b 1

:ensure_dotnet
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo .NET SDK was not found in PATH.
    exit /b 1
)
exit /b 0

:help
echo.
echo Usage:
echo   publish.bat [portable^|single] [--skip-tests] [--no-pause]
echo.
echo Examples:
echo   publish.bat portable
echo   publish.bat single --skip-tests
echo.
exit /b 1

:cleanup_success
popd >nul
if "%PAUSE_ON_EXIT%"=="1" pause
exit /b 0

:cleanup_fail
popd >nul

:fail
if "%PAUSE_ON_EXIT%"=="1" pause
exit /b 1
