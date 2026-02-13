@echo off
echo hi itsfatduck bro :D

echo Available build profiles:
echo 1. Portable
echo 2. Single
set /p buildOption="Choose build profile (1/2): "
if "%buildOption%"=="1" (
    echo Publishing portable build...
    dotnet publish optimizerDuck/optimizerDuck.csproj /p:PublishProfile=Portable
    pause
) else if "%buildOption%"=="2" (
    echo Publishing single build...
    dotnet publish optimizerDuck/optimizerDuck.csproj /p:PublishProfile=Single
    pause
) else (
    echo Invalid choice
    pause
    exit /b 1
)

echo.
echo Do you want to run tests? (y/n)
set /p runTests="Run tests? (y/n): "
if "%runTests%"=="y" (
    echo Running tests...
    dotnet test
    pause
) else (
    echo Skipping tests...
    pause
    exit /b 0
)

echo.
echo Build and test completed!
pause
