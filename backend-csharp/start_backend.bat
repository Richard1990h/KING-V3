@echo off
REM ============================================
REM LittleHelper AI - Quick Backend Start
REM For when you just need the backend running
REM ============================================
cd /d "%~dp0"

echo.
echo Starting LittleHelper AI Backend...
echo.
echo API URL: http://localhost:8002
echo Swagger: http://localhost:8002/swagger
echo.
echo Press Ctrl+C to stop.
echo.

cd LittleHelperAI.API
dotnet run --urls=http://localhost:8002
