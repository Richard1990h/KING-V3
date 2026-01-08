@echo off
REM ============================================
REM LittleHelper AI - MongoDB Setup Helper
REM ============================================
setlocal enabledelayedexpansion

echo.
echo ========================================
echo   MongoDB Setup Helper
echo ========================================
echo.

echo Choose your MongoDB setup option:
echo.
echo   1. Install MongoDB Community Server (Local)
echo   2. Configure MongoDB Atlas (Free Cloud)
echo   3. Test existing MongoDB connection
echo   4. Start MongoDB service (if installed)
echo   5. Exit
echo.

set /p CHOICE="Enter choice (1-5): "

if "%CHOICE%"=="1" goto :install_local
if "%CHOICE%"=="2" goto :setup_atlas
if "%CHOICE%"=="3" goto :test_connection
if "%CHOICE%"=="4" goto :start_service
if "%CHOICE%"=="5" goto :end

:install_local
echo.
echo ========================================
echo   Install MongoDB Community Server
echo ========================================
echo.
echo Follow these steps:
echo.
echo 1. Download MongoDB Community Server:
echo    https://www.mongodb.com/try/download/community
echo.
echo 2. Run the installer and select:
echo    - Setup Type: "Complete"
echo    - Check: "Install MongoDB as a Service"
echo    - Check: "Run service as Network Service user"
echo.
echo 3. MongoDB Compass (GUI) is optional but helpful
echo.
echo 4. After installation, MongoDB starts automatically
echo.
echo 5. Default connection: mongodb://localhost:27017
echo.
echo Press any key to open the download page...
pause >nul
start https://www.mongodb.com/try/download/community
goto :end

:setup_atlas
echo.
echo ========================================
echo   Setup MongoDB Atlas (Free Cloud)
echo ========================================
echo.
echo Follow these steps:
echo.
echo 1. Go to MongoDB Atlas and create a free account:
echo    https://www.mongodb.com/cloud/atlas/register
echo.
echo 2. Create a new project (or use default)
echo.
echo 3. Build a Database:
echo    - Choose "FREE" (M0 Sandbox)
echo    - Select a cloud provider and region close to you
echo    - Click "Create"
echo.
echo 4. Set up Database Access:
echo    - Go to "Database Access" in the sidebar
echo    - Click "Add New Database User"
echo    - Choose "Password" authentication
echo    - Enter a username and password (SAVE THESE!)
echo    - Set privileges to "Read and write to any database"
echo    - Click "Add User"
echo.
echo 5. Set up Network Access:
echo    - Go to "Network Access" in the sidebar
echo    - Click "Add IP Address"
echo    - Click "Allow Access from Anywhere" (or add your IP)
echo    - Click "Confirm"
echo.
echo 6. Get your connection string:
echo    - Go to "Database" in the sidebar
echo    - Click "Connect" on your cluster
echo    - Choose "Connect your application"
echo    - Copy the connection string
echo.
echo 7. Update your .env file:
echo    MONGO_URL=mongodb+srv://YOUR_USERNAME:YOUR_PASSWORD@cluster.mongodb.net/?retryWrites=true^&w=majority
echo.
echo Press any key to open MongoDB Atlas...
pause >nul
start https://www.mongodb.com/cloud/atlas/register
goto :end

:test_connection
echo.
echo ========================================
echo   Testing MongoDB Connection
echo ========================================
echo.

REM Check if .env exists and get MONGO_URL
if exist ".env" (
    for /f "tokens=2 delims==" %%a in ('findstr /C:"MONGO_URL" .env') do set MONGO_URL=%%a
    echo Testing connection to: !MONGO_URL!
    echo.
)

REM Try local connection first
echo Testing localhost:27017...
python -c "from pymongo import MongoClient; c=MongoClient('mongodb://localhost:27017', serverSelectionTimeoutMS=5000); c.server_info(); print('SUCCESS: Connected to local MongoDB!')" 2>nul
if errorlevel 1 (
    echo FAILED: Could not connect to local MongoDB
    echo.
    echo Testing connection from .env...
    python -c "from pymongo import MongoClient; import os; from dotenv import load_dotenv; load_dotenv(); url=os.getenv('MONGO_URL'); print(f'Testing: {url[:50]}...'); c=MongoClient(url, serverSelectionTimeoutMS=10000); info=c.server_info(); print(f'SUCCESS: Connected! MongoDB version: {info.get(\"version\", \"unknown\")}')" 2>&1
    if errorlevel 1 (
        echo.
        echo FAILED: Could not connect using .env configuration
        echo Please check your MONGO_URL in .env file
    )
) else (
    echo.
    echo Local MongoDB is working!
)
goto :end

:start_service
echo.
echo ========================================
echo   Starting MongoDB Service
echo ========================================
echo.
echo Attempting to start MongoDB service...
echo (This requires administrator privileges)
echo.

net start MongoDB >nul 2>&1
if errorlevel 1 (
    echo Could not start MongoDB service.
    echo.
    echo Try running this script as Administrator, or:
    echo 1. Press Win+R
    echo 2. Type: services.msc
    echo 3. Find "MongoDB Server"
    echo 4. Right-click and select "Start"
) else (
    echo MongoDB service started successfully!
)
goto :end

:end
echo.
pause
