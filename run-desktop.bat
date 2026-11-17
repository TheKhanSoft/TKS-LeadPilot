@echo off
title TKS LeadPilot Launcher
echo ========================================================
echo  Launching TKS LeadPilot Windows Desktop Application...
echo ========================================================

if exist "%~dp0desktop\dist\TKS.LeadPilot.UI.exe" (
    start "" "%~dp0desktop\dist\TKS.LeadPilot.UI.exe"
    exit /b 0
)

if exist "%~dp0desktop\src\TKS.LeadPilot.UI\bin\Debug\net10.0\TKS.LeadPilot.UI.exe" (
    start "" "%~dp0desktop\src\TKS.LeadPilot.UI\bin\Debug\net10.0\TKS.LeadPilot.UI.exe"
    exit /b 0
)

echo Executable not found in dist or bin. Launching via dotnet run...
cd /d "%~dp0desktop\src\TKS.LeadPilot.UI"
dotnet run
