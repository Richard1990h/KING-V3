@echo off
REM ============================================
REM LittleHelper AI - C#/.NET Setup Script
REM ============================================
setlocal enabledelayedexpansion

echo.
echo ========================================
echo   LittleHelper AI - C#/.NET Backend Setup
echo ========================================
echo.

REM Set colors for output
set "GREEN=[92m"
set "RED=[91m"
set "YELLOW=[93m"
set "CYAN=[96m"
set "RESET=[0m"

set ERRORS=0
set WARNINGS=0

REM ============================================
REM Step 1: Check .NET SDK Installation
REM ============================================
echo %CYAN%[Step 1/10] Checking .NET SDK installation...%RESET%

dotnet --version >nul 2>&1
if errorlevel 1 (
    echo %RED%[ERROR] .NET SDK is not installed or not in PATH%RESET%
    echo Please install .NET 8 SDK from https://dotnet.microsoft.com/download
    set /a ERRORS+=1
    goto :check_summary
) else (
    for /f "tokens=*" %%i in ('dotnet --version 2^>^&1') do set DOTNET_VERSION=%%i
    echo %GREEN%[OK] .NET SDK !DOTNET_VERSION! found%RESET%
    
    REM Check if it's .NET 8+
    echo !DOTNET_VERSION! | findstr /B "8." >nul
    if errorlevel 1 (
        echo %YELLOW%[WARNING] .NET 8.x recommended, found !DOTNET_VERSION!%RESET%
        set /a WARNINGS+=1
    )
)

REM ============================================
REM Step 2: Check MySQL/XAMPP Installation
REM ============================================
echo %CYAN%[Step 2/10] Checking MySQL installation...%RESET%

mysql --version >nul 2>&1
if errorlevel 1 (
    echo %YELLOW%[WARNING] MySQL client not in PATH%RESET%
    echo Checking common XAMPP locations...
    
    if exist "C:\xampp\mysql\bin\mysql.exe" (
        echo %GREEN%[OK] MySQL found in XAMPP (C:\xampp)%RESET%
        set "MYSQL_PATH=C:\xampp\mysql\bin"
    ) else if exist "D:\xampp\mysql\bin\mysql.exe" (
        echo %GREEN%[OK] MySQL found in XAMPP (D:\xampp)%RESET%
        set "MYSQL_PATH=D:\xampp\mysql\bin"
    ) else (
        echo %YELLOW%[WARNING] MySQL not found - please ensure XAMPP is installed%RESET%
        set /a WARNINGS+=1
    )
) else (
    for /f "tokens=*" %%i in ('mysql --version 2^>^&1') do set MYSQL_VERSION=%%i
    echo %GREEN%[OK] MySQL found: !MYSQL_VERSION!%RESET%
)

REM ============================================
REM Step 3: Check if MySQL is Running
REM ============================================
echo %CYAN%[Step 3/10] Checking if MySQL is running...%RESET%

netstat -an | findstr ":3306" >nul 2>&1
if errorlevel 1 (
    echo %YELLOW%[WARNING] MySQL does not appear to be running on port 3306%RESET%
    echo Please start MySQL from XAMPP Control Panel
    set /a WARNINGS+=1
) else (
    echo %GREEN%[OK] MySQL is running on port 3306%RESET%
)

REM ============================================
REM Step 4: Check Solution File
REM ============================================
echo %CYAN%[Step 4/10] Checking solution structure...%RESET%

if exist "LittleHelperAI.sln" (
    echo %GREEN%[OK] Solution file found%RESET%
) else (
    echo %RED%[ERROR] LittleHelperAI.sln not found%RESET%
    echo Please run this script from the backend-csharp directory
    set /a ERRORS+=1
    goto :check_summary
)

REM ============================================
REM Step 5: Check Project Files
REM ============================================
echo %CYAN%[Step 5/10] Checking project files...%RESET%

if exist "LittleHelperAI.API\LittleHelperAI.API.csproj" (
    echo %GREEN%[OK] API project found%RESET%
) else (
    echo %RED%[ERROR] API project not found%RESET%
    set /a ERRORS+=1
)

if exist "LittleHelperAI.Agents\LittleHelperAI.Agents.csproj" (
    echo %GREEN%[OK] Agents project found%RESET%
) else (
    echo %RED%[ERROR] Agents project not found%RESET%
    set /a ERRORS+=1
)

if exist "LittleHelperAI.Data\LittleHelperAI.Data.csproj" (
    echo %GREEN%[OK] Data project found%RESET%
) else (
    echo %RED%[ERROR] Data project not found%RESET%
    set /a ERRORS+=1
)

REM ============================================
REM Step 6: Check Configuration
REM ============================================
echo %CYAN%[Step 6/10] Checking configuration...%RESET%

if exist "LittleHelperAI.API\appsettings.json" (
    echo %GREEN%[OK] appsettings.json found%RESET%
    
    REM Check for connection string
    findstr /C:"MySQL" "LittleHelperAI.API\appsettings.json" >nul 2>&1
    if errorlevel 1 (
        echo %YELLOW%[WARNING] MySQL connection string may not be configured%RESET%
        set /a WARNINGS+=1
    ) else (
        echo %GREEN%[OK] MySQL connection string found%RESET%
    )
) else (
    echo %YELLOW%[WARNING] appsettings.json not found - creating default...%RESET%
    (
        echo {
        echo   "ConnectionStrings": {
        echo     "MySQL": "Server=localhost;Database=littlehelper_ai;User=root;Password=;",
        echo     "Redis": ""
        echo   },
        echo   "JWT": {
        echo     "Secret": "littlehelper-ai-secret-key-2024-minimum-32-characters"
        echo   },
        echo   "Stripe": {
        echo     "SecretKey": "sk_test_your_key_here"
        echo   },
        echo   "EmergentLLM": {
        echo     "Key": ""
        echo   },
        echo   "Logging": {
        echo     "LogLevel": {
        echo       "Default": "Information"
        echo     }
        echo   }
        echo }
    ) > "LittleHelperAI.API\appsettings.json"
    echo %GREEN%[OK] Default appsettings.json created%RESET%
    set /a WARNINGS+=1
)

REM ============================================
REM Step 7: Restore NuGet Packages
REM ============================================
echo %CYAN%[Step 7/10] Restoring NuGet packages...%RESET%

dotnet restore --verbosity quiet
if errorlevel 1 (
    echo %RED%[ERROR] Failed to restore NuGet packages%RESET%
    set /a ERRORS+=1
) else (
    echo %GREEN%[OK] NuGet packages restored%RESET%
)

REM ============================================
REM Step 8: Build Solution
REM ============================================
echo %CYAN%[Step 8/10] Building solution...%RESET%

dotnet build --configuration Release --verbosity quiet
if errorlevel 1 (
    echo %RED%[ERROR] Build failed%RESET%
    echo.
    echo Running build with details...
    dotnet build --configuration Release
    set /a ERRORS+=1
) else (
    echo %GREEN%[OK] Build successful%RESET%
)

REM ============================================
REM Step 9: Verify Controllers
REM ============================================
echo %CYAN%[Step 9/10] Verifying controllers...%RESET%

set CONTROLLER_COUNT=0
for %%f in (LittleHelperAI.API\Controllers\*.cs) do set /a CONTROLLER_COUNT+=1

if %CONTROLLER_COUNT% GEQ 7 (
    echo %GREEN%[OK] %CONTROLLER_COUNT% controllers found%RESET%
) else (
    echo %YELLOW%[WARNING] Only %CONTROLLER_COUNT% controllers found (expected 7+)%RESET%
    set /a WARNINGS+=1
)

REM List controllers
echo     Controllers:
for %%f in (LittleHelperAI.API\Controllers\*.cs) do (
    echo       - %%~nf
)

REM ============================================
REM Step 10: Verify Services
REM ============================================
echo %CYAN%[Step 10/10] Verifying services...%RESET%

set SERVICE_COUNT=0
for %%f in (LittleHelperAI.API\Services\*.cs) do set /a SERVICE_COUNT+=1

if %SERVICE_COUNT% GEQ 10 (
    echo %GREEN%[OK] %SERVICE_COUNT% service files found%RESET%
) else (
    echo %YELLOW%[WARNING] Only %SERVICE_COUNT% service files found (expected 10+)%RESET%
    set /a WARNINGS+=1
)

:check_summary
REM ============================================
REM Summary
REM ============================================
echo.
echo ========================================
echo   Setup Summary
echo ========================================

if %ERRORS% EQU 0 (
    if %WARNINGS% EQU 0 (
        echo %GREEN%[100%% READY] All checks passed!%RESET%
        echo.
        echo   Status: FULLY VERIFIED
        echo   API Controllers: Ready
        echo   Services: Ready
        echo   Database: Configured
        echo   Build: Successful
    ) else (
        echo %YELLOW%[READY WITH WARNINGS] %WARNINGS% warning(s) found%RESET%
        echo.
        echo   The application should work but some features may be limited.
    )
) else (
    echo %RED%[NOT READY] %ERRORS% error(s) and %WARNINGS% warning(s) found%RESET%
    echo.
    echo   Please fix the errors above before running the server.
    goto :end
)

echo.
echo ========================================
echo   Start Server
echo ========================================
echo.

set /p START_SERVER="Do you want to start the server now? (Y/N): "
if /i "%START_SERVER%"=="Y" (
    echo.
    echo Starting LittleHelper AI C# Backend on http://localhost:8001
    echo Press Ctrl+C to stop the server
    echo.
    echo Swagger UI will be available at: http://localhost:8001/swagger
    echo.
    
    cd LittleHelperAI.API
    dotnet run --configuration Release
) else (
    echo.
    echo To start the server manually, run:
    echo   cd LittleHelperAI.API
    echo   dotnet run --configuration Release
    echo.
    echo Or with hot reload:
    echo   dotnet watch run
)

:end
echo.
pause
