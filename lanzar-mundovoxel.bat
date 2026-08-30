@echo off
rem ============================================================================
rem Lanzador de MundoVoxel (Windows)
rem
rem El dotnet de AutoClaw esta primero en el PATH y solo trae el runtime 8.0;
rem el juego necesita .NET 10. Este lanzador fija DOTNET_ROOT al SDK del sistema
rem antes de arrancar el ejecutable, para que el apphost encuentre el runtime.
rem
rem Si mueves el proyecto o cambias la configuracion de compilacion, ajusta la
rem ruta del EXE mas abajo.
rem ============================================================================
set "DOTNET_ROOT=C:\Program Files\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"

set "EXE=%~dp0MundoVoxel.Client\bin\Debug\net10.0-windows10.0.19041.0\win-x64\MundoVoxel.Client.exe"

if not exist "%EXE%" (
    echo No se encontro el ejecutable del juego:
    echo   %EXE%
    echo Compila primero el cliente con:
    echo   dotnet build MundoVoxel.Client\MundoVoxel.Client.csproj -f net10.0-windows10.0.19041.0
    pause
    exit /b 1
)

start "" "%EXE%"
