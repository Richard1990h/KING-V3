@echo off
REM ============================================
REM LittleHelper AI - C# Backend Verification
REM Quick health check script
REM ============================================
setlocal enabledelayedexpansion

echo.
echo ========================================
echo   LittleHelper AI C# - Quick Verification
echo ========================================
echo.

set PASS=0
set FAIL=0

REM Check .NET SDK
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [FAIL] .NET SDK not found
    set /a FAIL+=1
) else (
    echo [PASS] .NET SDK installed
    set /a PASS+=1
)

REM Check Solution File
if exist "LittleHelperAI.sln" (
    echo [PASS] Solution file exists
    set /a PASS+=1
) else (
    echo [FAIL] Solution file not found
    set /a FAIL+=1
)

REM Check API Project
if exist "LittleHelperAI.API\LittleHelperAI.API.csproj" (
    echo [PASS] API project exists
    set /a PASS+=1
) else (
    echo [FAIL] API project not found
    set /a FAIL+=1
)

REM Check Agents Project
if exist "LittleHelperAI.Agents\LittleHelperAI.Agents.csproj" (
    echo [PASS] Agents project exists
    set /a PASS+=1
) else (
    echo [FAIL] Agents project not found
    set /a FAIL+=1
)

REM Check Data Project
if exist "LittleHelperAI.Data\LittleHelperAI.Data.csproj" (
    echo [PASS] Data project exists
    set /a PASS+=1
) else (
    echo [FAIL] Data project not found
    set /a FAIL+=1
)

REM Check appsettings.json
if exist "LittleHelperAI.API\appsettings.json" (
    echo [PASS] appsettings.json exists
    set /a PASS+=1
) else (
    echo [FAIL] appsettings.json not found
    set /a FAIL+=1
)

REM Check Controllers
set CTRL_COUNT=0
for %%f in (LittleHelperAI.API\Controllers\*.cs) do set /a CTRL_COUNT+=1
if %CTRL_COUNT% GEQ 7 (
    echo [PASS] %CTRL_COUNT% controllers found
    set /a PASS+=1
) else (
    echo [FAIL] Only %CTRL_COUNT% controllers found
    set /a FAIL+=1
)

REM Check Services
set SVC_COUNT=0
for %%f in (LittleHelperAI.API\Services\*.cs) do set /a SVC_COUNT+=1
if %SVC_COUNT% GEQ 10 (
    echo [PASS] %SVC_COUNT% service files found
    set /a PASS+=1
) else (
    echo [FAIL] Only %SVC_COUNT% service files found
    set /a FAIL+=1
)

REM Try to build
echo.
echo Building solution...
dotnet build --verbosity quiet >nul 2>&1
if errorlevel 1 (
    echo [FAIL] Build failed
    set /a FAIL+=1
) else (
    echo [PASS] Build successful
    set /a PASS+=1
)

REM Check MySQL port
netstat -an | findstr ":3306" >nul 2>&1
if errorlevel 1 (
    echo [WARN] MySQL not running on port 3306
) else (
    echo [PASS] MySQL running on port 3306
    set /a PASS+=1
)

echo.
echo ========================================
set /a TOTAL=%PASS%+%FAIL%
if %TOTAL% EQU 0 set TOTAL=1
set /a PERCENT=(%PASS%*100)/%TOTAL%

if %FAIL% EQU 0 (
    echo   Result: 100%% VERIFIED
    echo   All %PASS% checks passed!
) else (
    echo   Result: %PERCENT%%% Complete
    echo   Passed: %PASS% / Failed: %FAIL%
)
echo ========================================
echo.

pause
