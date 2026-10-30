@echo off

rem ////////////////////////////////////////
rem 		Init pack
rem ///////////////////////////////////////

set PACK_NAME=Scenes

call %~dp0\..\..\config.bat
call %~dp0\..\..\setenv.bat

rem ///////////////////////////////////////

set LOG=%CD%\make%PACK_NAME%.log

rem bin holds the committed BDAE models. They are source data (engine animation,
rem material and event records plus the unrefined meshes), so they are staged as
rem they are and never removed. Refined meshes come from the .blend files:
rem see rebuild_Linux.sh and models.json.

rem Clean data
if "%1"=="clean" (
	call _clean.bat
	goto END
)

if not exist "bin" (
	echo.
	echo Data\Scenes\bin is missing; restore it from version control.
	pause
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

rem Build bar file or not
if not "%1"=="no_bar" (
	echo.
	echo **************************************************************
	echo    Achiving %PACK_NAME%
	echo **************************************************************

	if not exist %OUTPUT_PATH%\%PACK_NAME% (
		mkdir %OUTPUT_PATH%\%PACK_NAME%
	)
	xcopy /S /Y bin %OUTPUT_PATH%\%PACK_NAME%

	if ERRORLEVEL 1 (
		echo.
		echo ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR ERROR
		echo.
		pause
		goto END
	)

	rem Scene textures sit beside the .blend files; they override bin.
	for /D %%d in (*.) do (
		if not "%%d"=="bin" (
			if not exist %OUTPUT_PATH%\%PACK_NAME%\%%d (
				mkdir %OUTPUT_PATH%\%PACK_NAME%\%%d
			)
			for %%f in ( %%d\*.png ) do (
				copy /Y %%f %OUTPUT_PATH%\%PACK_NAME%\%%d > NUL
			)
		)
	)
)

:END
