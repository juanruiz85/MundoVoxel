@echo off
REM Instala MundoVoxel.Server como servicio de Windows.
REM Uso: InstalarServicioWindows.bat [ruta-publicacion]
setlocal
set PUB=%~1
if "%PUB%"=="" set PUB=%CD%\..\publicar\win

echo ==^> Publicando servidor (win-x64, autocontenido)...
dotnet publish ..\MundoVoxel.Server -c Release -r win-x64 --self-contained -o "%PUB%"

echo ==^> Creando servicio MundoVoxelServer...
sc.exe create MundoVoxelServer binPath= "\"%PUB%\MundoVoxel.Server.exe\"" start= auto
sc.exe description MundoVoxelServer "Servidor multijugador de MundoVoxel (mundos en memoria, puerto 25575)"
sc.exe start MundoVoxelServer

echo.
echo Listo. Comandos utiles:
echo   sc.exe stop MundoVoxelServer
echo   sc.exe start MundoVoxelServer
echo   sc.exe delete MundoVoxelServer
echo   notepad "%PUB%\appsettings.json"
endlocal
