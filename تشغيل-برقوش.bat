@echo off
chcp 65001 >nul
title BRQOSH — برقوش
echo تشغيل نظام BRQOSH للمحاسبة...
dotnet run --project "%~dp0src\Brqosh.Desktop\Brqosh.Desktop.csproj" -c Debug
pause
