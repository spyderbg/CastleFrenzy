@echo off

rem ////////////////////////////////////////
rem 		Init pack
rem ///////////////////////////////////////

set PACK_NAME=Scenes

call %~dp0\..\..\config.bat
call %~dp0\..\..\setenv.bat

rem ///////////////////////////////////////

set LOG=%CD%\make%PACK_NAME%.log

echo.
echo **************************************************************
echo    Cleaning %PACK_NAME%
echo **************************************************************

if exist *.log (
	del /s /q *.log
)

rem bin holds the committed BDAE models (source data), so it is never removed.

if exist %OUTPUT_PATH%\%PACK_NAME% (
	rd /s /q %OUTPUT_PATH%\%PACK_NAME%
)
