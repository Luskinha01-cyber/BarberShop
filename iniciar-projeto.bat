@echo off
title BarberShop + WhatsApp Gateway
chcp 65001 >nul
echo ======================================================
echo    Iniciando BarberShop e Gateway do WhatsApp
echo ======================================================

set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"

echo [1/2] Iniciando Gateway do WhatsApp (Porta 8080)...
start "WhatsApp Gateway (Porta 8080)" /d "%~dp0whatsapp-service" cmd /k "node server.js"

timeout /t 2 /nobreak >nul

echo [2/2] Iniciando BarberShop Web (Porta 5296)...
start "BarberShop Web (Porta 5296)" /d "%~dp0BarberShop" cmd /k "dotnet run --launch-profile http"

echo.
echo ======================================================
echo    Servicos em execucao!
echo ======================================================
echo  - Escanear WhatsApp:  http://localhost:8080
echo  - Sistema BarberShop: http://localhost:5296
echo  - Painel Admin:       http://localhost:5296/Admin
echo ======================================================
echo.
pause
