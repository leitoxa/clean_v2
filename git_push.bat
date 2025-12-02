@echo off
chcp 65001 >nul
echo ========================================
echo Git Auto Push
echo ========================================
echo.

REM Проверяем статус
echo [1/4] Проверка статуса...
git status
echo.

REM Добавляем все изменения
echo [2/4] Добавление файлов...
git add .
echo.

REM Запрашиваем сообщение коммита
set /p commit_msg="Введите сообщение коммита (или Enter для автосообщения): "
if "%commit_msg%"=="" (
    for /f "tokens=2-4 delims=/ " %%a in ('date /t') do (set mydate=%%c-%%b-%%a)
    for /f "tokens=1-2 delims=/:" %%a in ('time /t') do (set mytime=%%a:%%b)
    set commit_msg=Auto commit %mydate% %mytime%
)

REM Делаем коммит
echo [3/4] Создание коммита...
git commit -m "%commit_msg%"
echo.

REM Пушим в репозиторий
echo [4/4] Отправка в репозиторий...
git push
echo.

echo ========================================
echo Готово!
echo ========================================
pause
