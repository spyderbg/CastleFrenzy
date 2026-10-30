@echo off

rem ////////////////////////////////////////
rem 		Init pack
rem ///////////////////////////////////////

set PACK_NAME=Scenes

call %~dp0\..\..\config.bat
call %~dp0\..\..\setenv.bat

rem ///////////////////////////////////////

set LOG=%CD%\make%PACK_NAME%.log
set TEXTURE_FORMATS=tga;png;bmp;
set DAE_CONVERTER=%PROJECT_PATH%\lib\irrlicht\tools\ColladaConverter\bin\Release\ColladaConverter_2008.exe

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

rem  Go through all scene directories
for /D %%d in (*.) do (
  if not "%%d"=="bin" (
    
		if not exist bin\%%d (
			mkdir bin\%%d
		)
		
    echo %%d
		for %%f in (%%d\*.dae) do (
		  echo %%~fnxf  
		  %DAE_CONVERTER% -d -i %%~fnxf
		)
		
		rem Copy texture to 'bin' dir
		for %%f in ( %%d\*.bdae ) do (			
			move /Y %%f bin\%%d > NUL
		)
		
		rem Copy texture to 'bin' dir
		for %%f in ( %%d\*.png ) do (
			copy /Y %%f bin\%%d > NUL
		)
  )
)

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

