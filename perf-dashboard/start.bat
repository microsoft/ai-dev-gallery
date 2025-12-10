@echo off
echo Starting AI Dev Gallery Performance Dashboard...
echo.

cd /d "%~dp0"

echo Installing dependencies...
call npm install

echo.
echo Starting dashboard (press Ctrl+C to stop)...
echo.
echo Dashboard will be available at:
echo - Frontend: http://localhost:5173
echo - Backend API: http://localhost:3000
echo.

call npm run dev
