@echo off
setlocal EnableDelayedExpansion

echo =========================================
echo   Keystroke Commander — Build Script
echo =========================================
echo.

:: Check for dotnet SDK
where dotnet > nul 2>&1
if errorlevel 1 (
    echo ERROR: dotnet CLI not found in PATH.
    echo Install .NET 8 SDK from: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

for /f "tokens=*" %%a in ('dotnet --version') do set DOTNET_VER=%%a
echo Found .NET SDK: %DOTNET_VER%
echo.

:: Ensure NuGet.org source exists
dotnet nuget list source | findstr /C:"nuget.org" > nul
if errorlevel 1 (
    echo NuGet source missing. Adding nuget.org...
    dotnet nuget add source https://api.nuget.org/v3/index.json --name nuget.org
    if errorlevel 1 (
        echo ERROR: Failed to add NuGet source.
        pause
        exit /b 1
    )
    echo     OK
echo.
)

:: Configuration
set CONFIG=Release
set RID=win-x64
set PUBDIR=bin\%CONFIG%\net8.0-windows\%RID%\publish

echo [1/3] Restoring NuGet packages...
dotnet restore
if errorlevel 1 (
    echo ERROR: Restore failed.
    pause
    exit /b 1
)
echo     OK
echo.

echo [2/3] Building and publishing single-file executable...
dotnet publish ^
    -c %CONFIG% ^
    -r %RID% ^
    --self-contained false ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=false ^
    -p:DebugType=none ^
    -p:DebugSymbols=false
if errorlevel 1 (
    echo ERROR: Build failed.
    pause
    exit /b 1
)
echo     OK
echo.

:: Copy to root for convenience
if exist "%PUBDIR%\KeystrokeCommander.exe" (
    copy /Y "%PUBDIR%\KeystrokeCommander.exe" "KeystrokeCommander.exe" > nul
    echo [3/3] Copied exe to project root.
) else (
    echo [3/3] Exe location: %PUBDIR%\KeystrokeCommander.exe
)
echo.

echo =========================================
echo   BUILD SUCCESSFUL
echo =========================================
echo.
echo Output: %PUBDIR%\KeystrokeCommander.exe
echo.
echo Quick run:
echo   cd %PUBDIR%
echo   KeystrokeCommander.exe
echo.
pause
