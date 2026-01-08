@echo off
REM ============================================
REM LittleHelper AI - Stop All Services
REM ============================================
echo.
echo Stopping LittleHelper AI services...
echo.

REM Kill dotnet processes for this project
taskkill /f /im dotnet.exe 2>nul
if not errorlevel 1 echo Backend stopped.

REM Kill node processes on frontend port
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :3000') do (
    taskkill /f /pid %%a 2>nul
)
echo Frontend stopped.

echo.
echo All services stopped.
pause
