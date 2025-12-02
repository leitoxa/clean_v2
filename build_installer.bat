@echo off
chcp 65001 >nul
echo ========================================
echo File Cleanup Manager - Build Installer
echo ========================================
echo.

REM Поиск Inno Setup Compiler
set ISCC=""

if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    goto :found
)

if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set ISCC="C:\Program Files\Inno Setup 6\ISCC.exe"
    goto :found
)

if exist "C:\Program Files (x86)\Inno Setup 5\ISCC.exe" (
    set ISCC="C:\Program Files (x86)\Inno Setup 5\ISCC.exe"
    goto :found
)

if exist "C:\Program Files\Inno Setup 5\ISCC.exe" (
    set ISCC="C:\Program Files\Inno Setup 5\ISCC.exe"
    goto :found
)

echo [ERROR] Inno Setup Compiler не найден!
echo.
echo Установите Inno Setup с https://jrsoftware.org/isdl.php
echo.
pause
exit /b 1

:found
echo [OK] Найден Inno Setup: %ISCC%
echo.

REM Проверка существования setup.iss
if not exist "setup.iss" (
    echo [ERROR] Файл setup.iss не найден!
    pause
    exit /b 1
)

REM Компиляция инсталлятора
echo [INFO] Компиляция инсталлятора...
echo.
%ISCC% setup.iss

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo [SUCCESS] Инсталлятор успешно создан!
    echo ========================================
    echo.
    echo Файл находится в папке: output\
    echo.
    
    REM Показываем список файлов в output
    if exist "output\" (
        echo Созданные файлы:
        dir /B output\*.exe
    )
    echo.
) else (
    echo.
    echo ========================================
    echo [ERROR] Ошибка при компиляции!
    echo ========================================
    echo.
)

pause
