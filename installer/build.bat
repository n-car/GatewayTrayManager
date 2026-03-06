@echo off
REM Build and Create Installer for Gateway Tray Manager
REM Requires: .NET 8 SDK, Inno Setup 6.x

echo.
echo ============================================
echo   Gateway Tray Manager - Build Installer
echo ============================================
echo.

REM Set paths
set PROJECT_DIR=..\src\GatewayTrayManager
set PROJECT_FILE=%PROJECT_DIR%\GatewayTrayManager.csproj
set PUBLISH_DIR=%PROJECT_DIR%\bin\Release\net8.0-windows\win-x64\publish

REM Step 1: Build and Publish
echo [1/2] Building and publishing application...
echo.

dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo Build successful!
echo.

REM Step 2: Create Installer
echo [2/2] Creating installer...
echo.

REM Find Inno Setup
set ISCC=
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
    set ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe
) else if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" (
    set ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe
)

if "%ISCC%"=="" (
    echo.
    echo WARNING: Inno Setup 6 not found!
    echo Please install from: https://jrsoftware.org/isdl.php
    echo.
    echo You can manually compile the installer:
    echo   1. Open Inno Setup Compiler
    echo   2. Open: GatewayTrayManager.iss
    echo   3. Press Ctrl+F9 to compile
    echo.
    pause
    exit /b 0
)

echo Found Inno Setup: %ISCC%
echo.

"%ISCC%" GatewayTrayManager.iss

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: Installer creation failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo   Build Complete!
echo ============================================
echo.
echo Installer created in: output\
echo.

REM Open output folder
start output

pause
