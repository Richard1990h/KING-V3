@echo off
REM ============================================
REM LittleHelper AI - Database Import Helper
REM For XAMPP Users
REM ============================================
setlocal enabledelayedexpansion

color 0B
echo.
echo  ===============================================
echo   LittleHelper AI - Database Setup
echo  ===============================================
echo.

set SQL_FILE=%~dp0..\database\littlehelper_ai_complete.sql
set MYSQL_PATH=

REM Find MySQL
if exist "C:\xampp\mysql\bin\mysql.exe" set MYSQL_PATH=C:\xampp\mysql\bin
if exist "D:\xampp\mysql\bin\mysql.exe" set MYSQL_PATH=D:\xampp\mysql\bin
if exist "E:\xampp\mysql\bin\mysql.exe" set MYSQL_PATH=E:\xampp\mysql\bin

mysql --version >nul 2>&1 && set MYSQL_PATH=system

if "%MYSQL_PATH%"=="" (
    echo [ERROR] MySQL not found!
    echo.
    echo Please either:
    echo   1. Add MySQL to your system PATH, or
    echo   2. Use phpMyAdmin to import the database manually:
    echo      - Open http://localhost/phpmyadmin
    echo      - Click "Import"
    echo      - Select: %SQL_FILE%
    echo      - Click "Go"
    echo.
    pause
    exit /b 1
)

echo MySQL found!
echo.
echo This will DROP and recreate the 'littlehelper_ai' database.
echo Any existing data will be lost!
echo.
set /p CONFIRM="Continue? (Y/N): "
if /i not "%CONFIRM%"=="Y" (
    echo Cancelled.
    pause
    exit /b 0
)

echo.
echo Importing database...

if "%MYSQL_PATH%"=="system" (
    mysql -u root < "%SQL_FILE%"
) else (
    "%MYSQL_PATH%\mysql.exe" -u root < "%SQL_FILE%"
)

if errorlevel 1 (
    color 0C
    echo.
    echo [ERROR] Database import failed!
    echo.
    echo Please check:
    echo   1. MySQL is running (check XAMPP Control Panel)
    echo   2. User 'root' with no password can connect
    echo.
    echo Alternative: Use phpMyAdmin to import manually.
) else (
    color 0A
    echo.
    echo  ===============================================
    echo   Database imported successfully!
    echo  ===============================================
    echo.
    echo   Database: littlehelper_ai
    echo   Tables: 18
    echo.
    echo   Default Users:
    echo     Admin: admin@littlehelper.ai / admin123
    echo     Test:  test@example.com / test123
    echo.
)

pause
