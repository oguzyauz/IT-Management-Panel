@echo off
setlocal
title IT Paneli - Ozet Kutusu

rem Masaustunde duran kucuk ozet penceresini acar ve "her zaman ustte" yapar.
rem Uygulama calismiyorsa once baslat.cmd calistirin.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\ozet-kutusu.ps1"
if errorlevel 1 pause
