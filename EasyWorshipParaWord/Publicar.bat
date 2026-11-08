@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo A criar o executavel (precisa do .NET SDK 8 e de internet na primeira vez)...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%~dp0Executavel"
if errorlevel 1 (
  echo.
  echo Falhou. Confirma que o .NET SDK 8 esta instalado.
  pause
  exit /b 1
)
echo.
echo Pronto: %~dp0Executavel\EasyWorshipParaWord.exe
echo Esse .exe corre em qualquer Windows 10/11, sem .NET nem Visual Studio.
pause
