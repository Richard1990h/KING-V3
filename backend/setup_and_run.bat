@echo off
REM ============================================
REM LittleHelper AI - Python/FastAPI Setup Script
REM Supports both MySQL (XAMPP) and MongoDB
REM ============================================
setlocal enabledelayedexpansion

echo.
echo ========================================
echo   LittleHelper AI - Python Backend Setup
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
REM Step 1: Check Python Installation
REM ============================================
echo %CYAN%[Step 1/9] Checking Python installation...%RESET%

python --version >nul 2>&1
if errorlevel 1 (
    echo %RED%[ERROR] Python is not installed or not in PATH%RESET%
    echo Please install Python 3.10+ from https://www.python.org/downloads/
    set /a ERRORS+=1
    goto :check_summary
) else (
    for /f "tokens=2" %%i in ('python --version 2^>^&1') do set PYTHON_VERSION=%%i
    echo %GREEN%[OK] Python !PYTHON_VERSION! found%RESET%
)

REM ============================================
REM Step 2: Check pip Installation
REM ============================================
echo %CYAN%[Step 2/9] Checking pip installation...%RESET%

pip --version >nul 2>&1
if errorlevel 1 (
    echo %RED%[ERROR] pip is not installed%RESET%
    set /a ERRORS+=1
) else (
    echo %GREEN%[OK] pip is installed%RESET%
)

REM ============================================
REM Step 3: Create Virtual Environment (optional)
REM ============================================
echo %CYAN%[Step 3/9] Setting up virtual environment...%RESET%

if not exist "venv" (
    echo Creating virtual environment...
    python -m venv venv
    if errorlevel 1 (
        echo %YELLOW%[WARNING] Could not create virtual environment, using global Python%RESET%
        set /a WARNINGS+=1
    ) else (
        echo %GREEN%[OK] Virtual environment created%RESET%
    )
) else (
    echo %GREEN%[OK] Virtual environment already exists%RESET%
)

REM Activate virtual environment if it exists
if exist "venv\Scripts\activate.bat" (
    call venv\Scripts\activate.bat
    echo %GREEN%[OK] Virtual environment activated%RESET%
)

REM ============================================
REM Step 4: Install Dependencies
REM ============================================
echo %CYAN%[Step 4/9] Installing Python dependencies...%RESET%

if exist "requirements.txt" (
    pip install -r requirements.txt --quiet
    if errorlevel 1 (
        echo %RED%[ERROR] Failed to install some dependencies%RESET%
        set /a ERRORS+=1
    ) else (
        echo %GREEN%[OK] Dependencies installed%RESET%
    )
) else (
    echo %YELLOW%[WARNING] requirements.txt not found%RESET%
    set /a WARNINGS+=1
)

REM Install MySQL dependencies
pip install aiomysql pymysql --quiet 2>nul
echo %GREEN%[OK] MySQL drivers installed%RESET%

REM ============================================
REM Step 5: Configure Database
REM ============================================
echo %CYAN%[Step 5/9] Configuring database...%RESET%

REM Check if .env has DB_TYPE setting
if exist ".env" (
    findstr /C:"DB_TYPE" .env >nul 2>&1
    if errorlevel 1 (
        echo %YELLOW%[INFO] Adding MySQL configuration to .env%RESET%
        echo.>> .env
        echo # Database Type: mysql or mongodb>> .env
        echo DB_TYPE=mysql>> .env
        echo MYSQL_HOST=localhost>> .env
        echo MYSQL_PORT=3306>> .env
        echo MYSQL_USER=root>> .env
        echo MYSQL_PASSWORD=>> .env
        echo MYSQL_DATABASE=littlehelper_ai>> .env
    )
) else (
    echo %CYAN%Creating .env file with MySQL configuration...%RESET%
    (
        echo # Database Configuration
        echo DB_TYPE=mysql
        echo.
        echo # MySQL Configuration ^(XAMPP^)
        echo MYSQL_HOST=localhost
        echo MYSQL_PORT=3306
        echo MYSQL_USER=root
        echo MYSQL_PASSWORD=
        echo MYSQL_DATABASE=littlehelper_ai
        echo.
        echo # MongoDB Configuration ^(if using instead^)
        echo MONGO_URL=mongodb://localhost:27017
        echo DB_NAME=littlehelper_ai
        echo.
        echo # Other Settings
        echo CORS_ORIGINS=*
        echo JWT_SECRET=littlehelper-ai-secret-key-2024
        echo STRIPE_SECRET_KEY=sk_test_your_key_here
    ) > .env
    echo %GREEN%[OK] .env file created with MySQL settings%RESET%
)

REM ============================================
REM Step 6: Check MySQL (XAMPP) Status
REM ============================================
echo %CYAN%[Step 6/9] Checking MySQL/XAMPP...%RESET%

netstat -an 2>nul | findstr ":3306" >nul 2>&1
if errorlevel 1 (
    echo %YELLOW%[WARNING] MySQL is not running on port 3306%RESET%
    echo.
    echo %CYAN%Please start MySQL from XAMPP Control Panel:%RESET%
    echo   1. Open XAMPP Control Panel
    echo   2. Click "Start" next to MySQL
    echo   3. Wait for the status to turn green
    echo   4. Run this script again
    echo.
    set /a WARNINGS+=1
) else (
    echo %GREEN%[OK] MySQL is running on port 3306%RESET%
    
    REM Test MySQL connection
    python -c "import pymysql; c=pymysql.connect(host='localhost', user='root', password='', connect_timeout=5); c.close(); print('Connected')" 2>nul
    if errorlevel 1 (
        echo %YELLOW%[WARNING] Could not connect to MySQL%RESET%
        set /a WARNINGS+=1
    ) else (
        echo %GREEN%[OK] MySQL connection successful%RESET%
    )
)

REM ============================================
REM Step 7: Verify Core Modules
REM ============================================
echo %CYAN%[Step 7/9] Verifying core modules...%RESET%

REM Check FastAPI
python -c "import fastapi" 2>nul
if errorlevel 1 (
    echo %RED%[ERROR] FastAPI not installed%RESET%
    set /a ERRORS+=1
) else (
    echo %GREEN%[OK] FastAPI installed%RESET%
)

REM Check aiomysql
python -c "import aiomysql" 2>nul
if errorlevel 1 (
    echo %RED%[ERROR] aiomysql not installed%RESET%
    set /a ERRORS+=1
) else (
    echo %GREEN%[OK] aiomysql installed%RESET%
)

REM Check JWT
python -c "import jwt" 2>nul
if errorlevel 1 (
    echo %RED%[ERROR] PyJWT not installed%RESET%
    set /a ERRORS+=1
) else (
    echo %GREEN%[OK] PyJWT installed%RESET%
)

REM ============================================
REM Step 8: Verify Server Files
REM ============================================
echo %CYAN%[Step 8/9] Verifying server files...%RESET%

if exist "server.py" (
    echo %GREEN%[OK] server.py found%RESET%
) else (
    echo %RED%[ERROR] server.py not found%RESET%
    set /a ERRORS+=1
)

if exist "mysql_adapter.py" (
    echo %GREEN%[OK] mysql_adapter.py found%RESET%
) else (
    echo %RED%[ERROR] mysql_adapter.py not found%RESET%
    set /a ERRORS+=1
)

if exist "agents" (
    echo %GREEN%[OK] agents directory found%RESET%
) else (
    echo %YELLOW%[WARNING] agents directory not found%RESET%
    set /a WARNINGS+=1
)

REM ============================================
REM Step 9: Database Configuration Check
REM ============================================
echo %CYAN%[Step 9/9] Final configuration check...%RESET%

REM Check DB_TYPE in .env
for /f "tokens=2 delims==" %%a in ('findstr /C:"DB_TYPE" .env 2^>nul') do set DB_TYPE_VAL=%%a
echo %GREEN%[OK] Database type: %DB_TYPE_VAL%%RESET%

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
        echo   Database: MySQL ^(XAMPP^)
        echo   User: root
        echo   Password: ^(none^)
        echo   Database will be created automatically
    ) else (
        echo %YELLOW%[READY WITH WARNINGS] %WARNINGS% warning(s) found%RESET%
    )
) else (
    echo %RED%[NOT READY] %ERRORS% error(s) and %WARNINGS% warning(s) found%RESET%
    echo Please fix the errors above before running the server.
    goto :end
)

echo.
echo ========================================
echo   Starting Server
echo ========================================
echo.

set /p START_SERVER="Do you want to start the server now? (Y/N): "
if /i "%START_SERVER%"=="Y" (
    echo.
    echo Starting LittleHelper AI Backend on http://localhost:8001
    echo Using MySQL database ^(XAMPP^)
    echo Press Ctrl+C to stop the server
    echo.
    
    REM Start the server
    python -m uvicorn server:app --host 0.0.0.0 --port 8001 --reload
) else (
    echo.
    echo To start the server manually, run:
    echo   python -m uvicorn server:app --host 0.0.0.0 --port 8001 --reload
)

:end
echo.
pause
