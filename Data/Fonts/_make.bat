@echo off

rem ////////////////////////////////////////
rem 		Init pack
rem ///////////////////////////////////////

set PACK_NAME=Fonts

call %~dp0\..\..\config.bat
call %~dp0\..\..\setenv.bat

rem ///////////////////////////////////////

set LOG=%CD%\make%PACK_NAME%.log

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

if not exist "bin" (
	mkdir bin
)

if not exist %PROJECT_DATA_PATH%\Sprites\%PACK_NAME% (
	mkdir %PROJECT_DATA_PATH%\Sprites\%PACK_NAME%
)

copy /Y *.yaml %PROJECT_DATA_PATH%\Sprites\%PACK_NAME%\ > NUL
copy /Y *.png %PROJECT_DATA_PATH%\Sprites\%PACK_NAME%\ > NUL
copy *.txt /Y bin > NUL


rem Build bar file or not
if not "%1"=="no_bar" (
	echo.
	echo **************************************************************
	echo    Achiving %PACK_NAME%
	echo **************************************************************
	
	rem del /Q %OUTPUT_PATH%\%PACK_NAME%.bar
	
	rem copy /Y %OUTPUT_PATH%\%PACK_NAME%.bar %OUTPUT_PATH_WII%\%PACK_NAME%.bar
	
	if not exist %OUTPUT_PATH%\%PACK_NAME% (
		mkdir %OUTPUT_PATH%\%PACK_NAME%
	)
	xcopy /S /Y bin %OUTPUT_PATH%\%PACK_NAME%
	rmdir bin	

	if ERRORLEVEL 1 ( 
		echo.
		echo ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR
		echo.
		pause
		goto END
	)

)

:END

