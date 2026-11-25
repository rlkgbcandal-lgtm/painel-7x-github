@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo =====================================
echo        COMPILANDO PAINEL 7X
echo =====================================
dotnet publish Painel7X.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 (
  echo.
  echo ERRO: instale o .NET 8 SDK no Windows e tente novamente.
  pause
  exit /b 1
)
echo.
echo EXE gerado em: bin\Release\net8.0-windows\win-x64\publish\Painel7X.exe
pause
