@echo off

rem ////////////////////////////////////////
rem 		Init pack
rem ///////////////////////////////////////

set PACK_NAME=Effects

call %~dp0\..\..\config.bat
call %~dp0\..\..\setenv.bat

rem ///////////////////////////////////////

set LOG=%CD%\make%PACK_NAME%.log
set TEXTURE_FORMATS=png;

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

mkdir %OUTPUT_PATH%\%PACK_NAME%

copy *.png %OUTPUT_PATH%\%PACK_NAME%
copy effects.xml %OUTPUT_PATH%\%PACK_NAME%

:END

