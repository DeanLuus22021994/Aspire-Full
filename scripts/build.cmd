@echo off
cd /d c:\Users\Dean\source\Aspire-Full
dotnet build --verbosity minimal
echo Build exit code: %ERRORLEVEL%
pause
