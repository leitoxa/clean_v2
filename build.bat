@echo off
echo Building File Cleanup Manager (Multi-Tab)...

set FRAMEWORK_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319

"%FRAMEWORK_PATH%\csc.exe" /target:winexe /out:CleanupManager.exe /reference:System.Windows.Forms.dll,System.Drawing.dll,System.ServiceProcess.dll,System.Configuration.Install.dll,System.Web.Extensions.dll,Microsoft.VisualBasic.dll CleanupManager.cs FolderConfig.cs

if %ERRORLEVEL% EQU 0 (
    echo Build successful!
    echo Run CleanupManager.exe to start.
) else (
    echo Build failed!
)
pause
