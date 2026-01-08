@echo off
REM ============================================
REM LittleHelper AI - Quick Frontend Start
REM For when you just need the frontend running
REM ============================================
cd /d "%~dp0..\frontend"

echo.
echo Starting LittleHelper AI Frontend...
echo.
echo URL: http://localhost:3000
echo.
echo Press Ctrl+C to stop.
echo.

set PORT=3000
yarn start
