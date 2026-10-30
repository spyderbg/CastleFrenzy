@echo off

call %~dp0\..\config.bat
call %~dp0\..\setenv.bat

echo.
echo **************************************************************
echo    Archiving Data
echo **************************************************************

pushd %OUTPUT_PATH%

  set ARCHIVE_NAME="resources.pack"
  
  if exist %ARCHIVE_NAME% (
    del /Q %ARCHIVE_NAME%
  )

  python "%PROJECT_TOOLS_PATH%\zip_directory.py" %ARCHIVE_NAME% "." --exclude-pattern "*.sprite" --exclude-pattern "*.bsprite" --exclude-pattern "*.pvr"
  if errorlevel 1 exit /b 1
  
popd

:END
