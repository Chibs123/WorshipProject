@echo off
setlocal
title EasyWorship - Word
cd /d "%~dp0"

rem ------------------------------------------------------------------
rem  Executar.bat - corre a aplicacao sem precisar do Visual Studio.
rem  Precisa do .NET SDK 8 (ou mais recente); se faltar, oferece instalar.
rem  Colocar na mesma pasta do ficheiro EasyWorshipParaWord.csproj.
rem ------------------------------------------------------------------

if not exist "EasyWorshipParaWord.csproj" (
    echo Nao encontrei o ficheiro EasyWorshipParaWord.csproj nesta pasta.
    echo Extrai o .zip todo e abre este ficheiro a partir da pasta extraida.
    echo.
    pause
    exit /b 1
)

rem --- 1) Ver se ha um .NET SDK 8 ou superior instalado ---
set "HAS_SDK="
where dotnet >nul 2>nul
if not errorlevel 1 (
    for /f "tokens=1 delims=." %%v in ('dotnet --list-sdks 2^>nul') do (
        if %%v GEQ 8 set "HAS_SDK=1"
    )
)
if defined HAS_SDK goto :executar

rem --- 2) Sem SDK: oferecer instalar com o winget ---
echo Nao encontrei o .NET SDK 8 neste computador.
echo.
where winget >nul 2>nul
if errorlevel 1 goto :manual

choice /c SN /n /m "Instalar o .NET 8 SDK agora com o winget? [S/N] "
if errorlevel 2 goto :manual

winget install --id Microsoft.DotNet.SDK.8 -e --accept-package-agreements --accept-source-agreements
if errorlevel 1 (
    echo.
    echo A instalacao nao terminou com sucesso.
    goto :manual
)
echo.
echo .NET 8 SDK instalado. Fecha esta janela e abre o Executar.bat outra vez
echo (o Windows so reconhece a instalacao numa janela nova).
echo.
pause
exit /b 0

:manual
echo.
echo Instala o ".NET 8 SDK" a partir de:
echo     https://dotnet.microsoft.com/download/dotnet/8.0
echo Depois abre o Executar.bat outra vez.
start "" "https://dotnet.microsoft.com/download/dotnet/8.0"
echo.
pause
exit /b 1

rem --- 3) Compilar (se for preciso) e abrir a aplicacao ---
:executar
set DOTNET_NOLOGO=1
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_ROLL_FORWARD=LatestMajor
echo A preparar a aplicacao... (a primeira vez demora e precisa de internet para descarregar um pacote)
dotnet run -c Release --nologo
if errorlevel 1 (
    echo.
    echo A aplicacao terminou com erro. Copia a mensagem acima e envia-a para ser corrigida.
    echo.
    pause
)
endlocal
