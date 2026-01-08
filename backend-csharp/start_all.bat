@echo off
REM ============================================
REM LittleHelper AI - Complete Startup Script
REM For Windows + XAMPP Users
REM ============================================
setlocal enabledelayedexpansion

color 0B
cls
echo.
echo  ===============================================
echo   LittleHelper AI - Complete Startup
echo   Backend: C#/.NET 8 ^| Frontend: React
echo   Database: MySQL (XAMPP)
echo  ===============================================
echo.

REM Configuration
set BACKEND_PORT=8002
set FRONTEND_PORT=3000
set MYSQL_PORT=3306

REM ============================================
REM Step 1: Check Prerequisites
REM ============================================
echo [1/6] Checking prerequisites...
echo.

REM Check .NET SDK
dotnet --version >nul 2>&1
if errorlevel 1 (
    color 0C
    echo [ERROR] .NET SDK is not installed!
    echo.
    echo Please install .NET 8 SDK from:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)
for /f "tokens=*" %%v in ('dotnet --version') do echo     .NET SDK: %%v - OK

REM Check Node.js
node --version >nul 2>&1
if errorlevel 1 (
    color 0C
    echo [ERROR] Node.js is not installed!
    echo.
    echo Please install Node.js from:
    echo https://nodejs.org/
    echo.
    pause
    exit /b 1
)
for /f "tokens=*" %%v in ('node --version') do echo     Node.js: %%v - OK

REM Check Yarn
yarn --version >nul 2>&1
if errorlevel 1 (
    echo     Yarn not found, installing...
    npm install -g yarn
)
for /f "tokens=*" %%v in ('yarn --version') do echo     Yarn: %%v - OK

REM Check MySQL (XAMPP)
echo.
echo [2/6] Checking MySQL (XAMPP)...
netstat -an | findstr ":%MYSQL_PORT%" >nul 2>&1
if errorlevel 1 (
    color 0E
    echo [WARNING] MySQL is not running on port %MYSQL_PORT%!
    echo.
    echo Please start MySQL from XAMPP Control Panel:
    echo   1. Open XAMPP Control Panel
    echo   2. Click "Start" next to MySQL
    echo   3. Wait for MySQL to show "Running"
    echo   4. Run this script again
    echo.
    set /p CONTINUE="Press Enter to continue anyway or Ctrl+C to exit..."
) else (
    echo     MySQL running on port %MYSQL_PORT% - OK
)

REM ============================================
REM Step 3: Setup Database
REM ============================================
echo.
echo [3/6] Database setup...
echo.
echo     The database SQL file is ready at:
echo     %~dp0..\database\littlehelper_ai_complete.sql
echo.
echo     To import:
echo     1. Open phpMyAdmin (http://localhost/phpmyadmin)
echo     2. Click "Import" tab
echo     3. Choose the SQL file above
echo     4. Click "Go"
echo.
set /p DB_READY="Have you already imported the database? (Y/N): "
if /i not "%DB_READY%"=="Y" (
    echo.
    echo Please import the database first, then run this script again.
    echo.
    pause
    exit /b 0
)

REM ============================================
REM Step 4: Install Backend Dependencies
REM ============================================
echo.
echo [4/6] Installing backend dependencies...
cd /d "%~dp0"

if not exist "LittleHelperAI.sln" (
    color 0C
    echo [ERROR] Solution file not found!
    echo Please run this script from the backend-csharp folder.
    pause
    exit /b 1
)

echo     Restoring NuGet packages...
dotnet restore --verbosity quiet
if errorlevel 1 (
    echo [ERROR] Failed to restore packages!
    pause
    exit /b 1
)
echo     Backend dependencies - OK

REM ============================================
REM Step 5: Install Frontend Dependencies
REM ============================================
echo.
echo [5/6] Installing frontend dependencies...
cd /d "%~dp0..\frontend"

if not exist "package.json" (
    color 0C
    echo [ERROR] Frontend package.json not found!
    pause
    exit /b 1
)

if not exist "node_modules" (
    echo     Installing npm packages (this may take a few minutes)...
    call yarn install
) else (
    echo     Node modules already installed - OK
)

REM ============================================
REM Step 6: Start Services
REM ============================================
echo.
echo [6/6] Starting services...
echo.

REM Check if ports are already in use
netstat -an | findstr ":%BACKEND_PORT%" >nul 2>&1
if not errorlevel 1 (
    echo [WARNING] Port %BACKEND_PORT% is already in use.
    echo Please close any application using this port.
)

netstat -an | findstr ":%FRONTEND_PORT%" >nul 2>&1
if not errorlevel 1 (
    echo [WARNING] Port %FRONTEND_PORT% is already in use.
    echo Please close any application using this port.
)

REM Start Backend in new window
echo Starting C# Backend on http://localhost:%BACKEND_PORT%...
start "LittleHelper Backend" cmd /k "cd /d "%~dp0LittleHelperAI.API" && dotnet run --urls=http://localhost:%BACKEND_PORT%"

REM Wait for backend to start
echo Waiting for backend to initialize...
timeout /t 5 /nobreak >nul

REM Start Frontend in new window
echo Starting React Frontend on http://localhost:%FRONTEND_PORT%...
start "LittleHelper Frontend" cmd /k "cd /d "%~dp0..\frontend" && set PORT=%FRONTEND_PORT% && yarn start"

REM ============================================
REM Complete!
REM ============================================
color 0A
echo.
echo  ===============================================
echo   LittleHelper AI Started Successfully!
echo  ===============================================
echo.
echo   Backend API:  http://localhost:%BACKEND_PORT%
echo   Swagger UI:   http://localhost:%BACKEND_PORT%/swagger
echo   Frontend:     http://localhost:%FRONTEND_PORT%
echo.
echo   Default Login:
echo     Admin: admin@littlehelper.ai / admin123
echo     Test:  test@example.com / test123
echo.
echo   Two command windows have opened:
echo     - Backend (C# .NET)
echo     - Frontend (React)
echo.
echo   Close both windows to stop the application.
echo  ===============================================
echo.

REM Open browser
set /p OPEN_BROWSER="Open browser now? (Y/N): "
if /i "%OPEN_BROWSER%"=="Y" (
    start http://localhost:%FRONTEND_PORT%
)

pause
