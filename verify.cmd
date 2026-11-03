@echo off
setlocal
cd /d "%~dp0"

echo == build (Release)
dotnet build SimT.sln -c Release --nologo -v q || exit /b 1

echo == tests
dotnet test SimT.sln -c Release --nologo --no-build || exit /b 1

echo == golden replay
dotnet run --project src/SimT.Cli -c Release --no-build -- golden samples/merchant-30d.json samples/merchant-30d.expected.txt || exit /b 1

echo == wpf render smoke
"src\SimT.Host.Wpf\bin\Release\net8.0-windows\SimT.Host.Wpf.exe" --smoke || exit /b 1

echo == verify ok
