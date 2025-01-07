setlocal enableextensions enabledelayedexpansion
echo off

cls

for /f "delims== tokens=1,2" %%G in (build.txt) do set %%G=%%H

set ASSEMBLYNAME=%ASSEMBLYNAME:"=%
set ASSEMBLYVERSION=%ASSEMBLYVERSION:"=%
set CONFIGURATION=%CONFIGURATION:"=%
set OUTDIR=%OUTDIR:"=%
set PROJECTDIR=%PROJECTDIR:"=%
set SOLUTIONDIR=%SOLUTIONDIR:"=%
set SOLUTIONNAME=%SOLUTIONNAME:"=%
set TARGETFILENAME=%TARGETFILENAME:"=%
set TARGETPATH=%TARGETPATH:"=%

echo ASSEMBLYNAME 	-%ASSEMBLYNAME%-
echo ASSEMBYVERSION -%ASSEMBLYVERSION%-
echo CONFIGURATION 	-%CONFIGURATION%-
echo OUTDIR 		-%OUTDIR%-
echo PROJECTDIR   	-%PROJECTDIR%-
echo SOLUTIONDIR  	-%SOLUTIONDIR%-
echo SOLUTIONNAME 	-%SOLUTIONNAME%-
echo TARGETFILENAME -%TARGETFILENAME%-
echo TARGETPATH   	-%TARGETPATH%-

if %CONFIGURATION%==Debug (
	echo "Emby - Debug.Emby configuration"
 
	del /F "%SOLUTIONDIR%..\embyserver-win-x64-4.8.10.0\programdata\plugins\%TARGETFILENAME%"
	copy /Y "%TARGETPATH%" "%SOLUTIONDIR%..\embyserver-win-x64-4.8.10.0\programdata\plugins\"
	copy /Y "%OUTDIR%%SOLUTIONNAME%.pdb" "%SOLUTIONDIR%..\embyserver-win-x64-4.8.10.0\programdata\plugins\"
	goto end:
)

if %CONFIGURATION%==Release (
	echo "Emby - Release.Emby configuration"
	
	del /F "%SOLUTIONDIR%..\embyserver-win-x64-4.8.10.0\programdata\plugins\%TARGETFILENAME%"
	copy /Y "%TARGETPATH%" "%SOLUTIONDIR%..\embyserver-win-x64-4.8.10.0\programdata\plugins\"
	copy /Y "%OUTDIR%%SOLUTIONNAME%.pdb" "%SOLUTIONDIR%..\embyserver-win-x64-4.8.10.0\programdata\plugins\"
	goto end:
)

echo "ERROR: No configuration run"
echo "Configuration: "
echo %CONFIGURATION%

:end

echo "Finished"

SET /P M=Do you want to start the media server (y/n) press ENTER:
IF %M%==Y GOTO RUNMEDIA
IF %M%==y GOTO RUNMEDIA
IF %M%==n GOTO EOF
IF %M%==n GOTO EOF

:RUNMEDIA
"%SOLUTIONDIR%..\embyserver-win-x64-4.8.10.0\system\EmbyServer.exe" 

:EOF

pause

echo on
endlocal