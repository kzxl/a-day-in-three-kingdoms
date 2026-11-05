@echo off
rem Builds the Lite single-file distribution, verifies it, and writes the zip + checksum.
rem Prerequisite: .NET 8 SDK. The artifact needs the .NET 8 Desktop Runtime on the target machine.
setlocal
cd /d "%~dp0"

set VERSION=0.4.1
set OUT=publish\lite
set DIST=dist
set ZIP=%DIST%\SimT-%VERSION%-win-x64.zip

echo == clean
dotnet clean -c Release -v q --nologo || exit /b 1

echo == restore
dotnet restore -v q || exit /b 1

echo == build (Release, warnings are errors)
dotnet build -c Release -warnaserror -v q --nologo || exit /b 1

echo == test gate
dotnet test -c Release --no-build --nologo || exit /b 1

echo == publish (lite single-file)
if exist "%OUT%" rmdir /s /q "%OUT%"
dotnet publish src\SimT.Host.Wpf\SimT.Host.Wpf.csproj -c Release -r win-x64 ^
  --self-contained false ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=embedded ^
  -p:PublishTrimmed=false ^
  -o "%OUT%" -v q --nologo || exit /b 1

echo == verify scenes assets
if not exist "%OUT%\assets\scenes\index.html" exit /b 1
if not exist "%OUT%\assets\scenes\art\manifest.json" exit /b 1

echo == smoke
"%OUT%\SimT.Host.Wpf.exe" --smoke || exit /b 1

echo == quickstart
copy /y QUICKSTART.txt "%OUT%\QUICKSTART.txt" >nul || exit /b 1

echo == package
if not exist "%DIST%" mkdir "%DIST%"
if exist "%ZIP%" del "%ZIP%"
powershell -NoProfile -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::CreateFromDirectory((Convert-Path '%OUT%'), (Join-Path (Get-Location) '%ZIP%'))" || exit /b 1

echo == size and checksum
powershell -NoProfile -Command ^
  "Get-ChildItem '%OUT%' -Recurse -File | Measure-Object Length -Sum | ForEach-Object { '{0:N1} MB in {1} files' -f ($_.Sum/1MB), $_.Count }; " ^
  "Get-ChildItem '%ZIP%' | ForEach-Object { '{0} {1:N1} MB' -f $_.Name, ($_.Length/1MB) }; " ^
  "Get-FileHash '%ZIP%' -Algorithm SHA256 | ForEach-Object { 'SHA256 ' + $_.Hash }"

echo == release artifact ready: %ZIP%
