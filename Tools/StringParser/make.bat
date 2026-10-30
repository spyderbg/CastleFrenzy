@echo off
rem Compatibility launcher; the Python exporter needs no compilation.
if not defined PYTHON_BIN set "PYTHON_BIN=python"
if "%~1"=="" (
    "%PYTHON_BIN%" "%~dp0..\..\tools\string_parser.py" --help
) else (
    "%PYTHON_BIN%" "%~dp0..\..\tools\string_parser.py" %*
)
exit /b %errorlevel%
