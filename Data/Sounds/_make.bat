@echo off
pushd "%~dp0"

rem ////////////////////////////////////////
rem 		Init pack
rem ///////////////////////////////////////

set PACK_NAME=Sounds

call %~dp0\..\..\config.bat
call %~dp0\..\..\setenv.bat

rem ///////////////////////////////////////


set GENERATED_HEADER="sounds.h"
set OUTPUT_FILES_LIST=
set SOUND_CONFIGURATION_FILE=SoundConfiguration.xls
set SOUND_CONFIGURATION_OUT=sounds_config.xml
	
	
rem Clean data
if "%1"=="clean" (
	call _clean.bat
	goto END
)
if "%1"=="" (
	call _clean.bat
)

rem Build data
echo.
echo **************************************************************
echo    Make %PACK_NAME%
echo **************************************************************

rem Generate output files list
setlocal ENABLEDELAYEDEXPANSION	

for /D %%i in (*.) do (
	
	set OUTPUT_FILES_LIST=!OUTPUT_FILES_LIST! %OUTPUT_PATH%\audio_%%i.pack
)

setlocal DISABLEDELAYEDEXPANSION  

if not defined PYTHON_BIN set "PYTHON_BIN=python"
"%PYTHON_BIN%" "%PROJECT_PATH%\tools\sound_config_exporter.py" "%SOUND_CONFIGURATION_FILE%" "%SOUND_CONFIGURATION_OUT%"
if errorlevel 1 goto FAILED
if not exist "%OUTPUT_PATH%\config" mkdir "%OUTPUT_PATH%\config"
move /Y "%SOUND_CONFIGURATION_OUT%" "%OUTPUT_PATH%\config\"
if errorlevel 1 goto FAILED

rem Build bar file or not
if not "%1"=="no_bar" (
	echo.
	echo **************************************************************
	echo    Achiving %PACK_NAME%
	echo **************************************************************
	
	
	for /D %%d in (*.) do (
		pushd %%d
	
		if not exist bin (
			mkdir bin
		)
		
		if exist %OUTPUT_PATH%\"audio_%%d.pack" (
			del /Q %OUTPUT_PATH%\"audio_%%d.pack"
		)
		
		for /R %%f in (*.wav) do (
			copy /Y %%f bin
		)
		
		cd bin
		
		"%PYTHON_BIN%" "%PROJECT_TOOLS_PATH%\zip_directory.py" "%OUTPUT_PATH%\audio_%%d.pack" "." --stored
		if errorlevel 1 exit /b 1

		cd ..
		
		rmdir /S /Q bin
			
		popd			
	)

	rem Generate sounds ids header
	%LIST_SOUND_FILES% %GENERATED_HEADER% %OUTPUT_FILES_LIST%
	
	if ERRORLEVEL 1 ( 
		echo.
		echo ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR
		echo.
		pause
		goto 
	)

	rem Copy header file if it is different or missing
	for %%f in (*.h) do (
		if not exist %HEADERS_OUT_PATH%\%%~nf.h (
			move /Y %%f %HEADERS_OUT_PATH%
		) else (		
			FC %%f %HEADERS_OUT_PATH%\%%~nf.h | FIND "FC: no dif" > nul 
			if ERRORLEVEL 1 ( 
				move /Y %%f %HEADERS_OUT_PATH%
			) else (
				del /Q /S %%f > nul 
			)
		)
	)	
)

:END

echo.

rem Pause at end
if "%1"=="" (
	pause
)

popd
exit /b 0

:FAILED
popd
exit /b 1

