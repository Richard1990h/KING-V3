@echo off
REM ============================================
REM LittleHelper AI - Python Backend Verification
REM Quick health check script
REM ============================================
setlocal enabledelayedexpansion

echo.
echo ========================================
echo   LittleHelper AI - Quick Verification
echo ========================================
echo.

set PASS=0
set FAIL=0

REM Check Python
python --version >nul 2>&1
if errorlevel 1 (
    echo [FAIL] Python not found
    set /a FAIL+=1
) else (
    echo [PASS] Python installed
    set /a PASS+=1
)

REM Check FastAPI
python -c "import fastapi" 2>nul
if errorlevel 1 (
    echo [FAIL] FastAPI not installed
    set /a FAIL+=1
) else (
    echo [PASS] FastAPI installed
    set /a PASS+=1
)

REM Check Motor
python -c "import motor" 2>nul
if errorlevel 1 (
    echo [FAIL] Motor not installed
    set /a FAIL+=1
) else (
    echo [PASS] Motor installed
    set /a PASS+=1
)

REM Check server.py
if exist "server.py" (
    echo [PASS] server.py exists
    set /a PASS+=1
) else (
    echo [FAIL] server.py not found
    set /a FAIL+=1
)

REM Check .env
if exist ".env" (
    echo [PASS] .env file exists
    set /a PASS+=1
) else (
    echo [FAIL] .env file not found
    set /a FAIL+=1
)

REM Check agents directory
if exist "agents" (
    echo [PASS] agents directory exists
    set /a PASS+=1
) else (
    echo [FAIL] agents directory not found
    set /a FAIL+=1
)

echo.
echo ========================================
set /a TOTAL=%PASS%+%FAIL%
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
