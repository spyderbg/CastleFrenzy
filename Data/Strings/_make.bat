@echo off
pushd "%~dp0"

rem ////////////////////////////////////////
rem 		Init pack
rem ///////////////////////////////////////

set PACK_NAME=Strings

call %~dp0\..\..\config.bat
call %~dp0\..\..\setenv.bat

rem ///////////////////////////////////////

set STRING_FILE_NAME=fortress_defender_strings.xls

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
echo     Make %PACK_NAME%
echo **************************************************************

rem Export text for all languages from .XLS file to outpu directory
if not exist bin (
	mkdir bin
)

if not defined PYTHON_BIN set "PYTHON_BIN=python"
"%PYTHON_BIN%" "%PROJECT_PATH%\tools\string_parser.py" "%STRING_FILE_NAME%" bin 7

if ERRORLEVEL 1 ( 
	echo.
	echo ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR
	echo.
	popd
	exit /b 1
)

move /Y bin\*.hpp %HEADERS_OUT_PATH%
move /Y bin\*.gen %CD%
rem move /Y bin\FontMapping_SC.txt %PROJECT_DATA_PATH%\Fonts
rem move /Y bin\FontMapping_JP.txt %PROJECT_DATA_PATH%\Fonts


rem Copy data to temp dir
rem if not exist %TEMP_PATH%\%PACK_NAME% (
rem 	mkdir %TEMP_PATH%\%PACK_NAME%
rem )
rem move /Y bin\*.* %TEMP_PATH%\%PACK_NAME%
rem rmdir bin


rem Build bar file or not
if not "%1"=="no_bar" (
	echo.
	echo **************************************************************
	echo    Achiving %PACK_NAME%
	echo **************************************************************	

	if not exist %OUTPUT_PATH%\%PACK_NAME% (
		mkdir %OUTPUT_PATH%\%PACK_NAME%
	)
	move /Y bin\*.* %OUTPUT_PATH%\%PACK_NAME%
	rmdir bin

	rem del /Q %OUTPUT_PATH%\%PACK_NAME%.bar
	
	rem copy /Y %OUTPUT_PATH%\%PACK_NAME%.bar %OUTPUT_PATH_WII%\%PACK_NAME%.bar
	
	if ERRORLEVEL 1 ( 
		echo.
		echo ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR
		echo.
		pause
		goto END
	)		
	
	for %%f in (%OUTPUT_PATH%\*.h) do (
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
popd
exit /b 0

