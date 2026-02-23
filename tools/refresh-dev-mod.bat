@echo off
echo ============================================
echo  RimMind Dev - Force Workshop Re-download
echo ============================================
echo.

set WORKSHOP_PATH=%~dp0..\..\..\..\..\workshop\content\294100\3668326181
set GAME_ID=294100

echo Closing RimWorld if running...
taskkill /F /IM RimWorldWin64.exe >nul 2>&1
timeout /t 2 /nobreak >nul

if exist "%WORKSHOP_PATH%" (
    echo Deleting cached mod folder...
    rmdir /s /q "%WORKSHOP_PATH%"
    echo Done.
) else (
    echo No cached mod folder found - will download fresh.
)

echo.
echo Launching RimWorld via Steam (will re-download mod)...
start "" "steam://run/%GAME_ID%"

echo.
echo Steam will re-download the mod automatically.
echo You can close this window.
pause
