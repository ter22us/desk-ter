@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
    echo Instaleaza .NET SDK 10 x64 si redeschide acest fisier.
    echo https://dotnet.microsoft.com/download/dotnet/10.0
    goto :failed
)
dotnet --version
if errorlevel 1 goto :failed
dotnet run --project tests\Ter22.Tests\Ter22.Tests.csproj -c Release
if errorlevel 1 goto :failed
dotnet publish src\Ter22.Windows\Ter22.Windows.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=none -o artifacts\windows-x64
if errorlevel 1 goto :failed
copy /y GHID_RO.md artifacts\windows-x64\GHID_RO.md >nul
if errorlevel 1 goto :failed
set "TER22_ISCC="
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "TER22_ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined TER22_ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "TER22_ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined TER22_ISCC if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "TER22_ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not defined TER22_ISCC for %%I in (ISCC.exe) do if not "%%~$PATH:I"=="" set "TER22_ISCC=%%~$PATH:I"
if not defined TER22_ISCC (
    echo Aplicatia a fost creata in artifacts\windows-x64\Ter22.Remote.exe
    echo Instaleaza Inno Setup 6 pentru a genera si instalatorul.
    echo https://jrsoftware.org/isdl.php
    goto :failed
)
"%TER22_ISCC%" installer\Ter22.Remote.iss
if errorlevel 1 goto :failed
echo.
echo Aplicatie: artifacts\windows-x64\Ter22.Remote.exe
echo Instalator: artifacts\installer\Ter22-Remote-Setup-0.1.1.exe
echo.
pause
exit /b 0
:failed
echo.
echo Compilarea nu a fost finalizata. Pastreaza mesajul de eroare de mai sus.
pause
exit /b 1
