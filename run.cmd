@echo off
setlocal
cd /d "%~dp0"

echo == Dang khoi dong Mot Ngay O Tam Quoc (SimT)...
dotnet run --project src/SimT.Host.Wpf --nologo %*
