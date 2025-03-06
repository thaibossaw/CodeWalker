@echo off
echo Starting CodeWalker...
"C:\Users\thoma\Documents\Code\CodeWalker\CodeWalker\bin\Debug\net48\CodeWalker.exe" > codewalker.log 2>&1
echo CodeWalker exited with code %ERRORLEVEL%
echo Log file created: codewalker.log
pause 