@echo off
chcp 65001 >nul
echo Быстрый Git Push...

REM Добавляем все изменения
git add .

REM Автоматическое сообщение с датой и временем
for /f "tokens=2-4 delims=/ " %%a in ('date /t') do (set mydate=%%c-%%b-%%a)
for /f "tokens=1-2 delims=/:" %%a in ('time /t') do (set mytime=%%a:%%b)

REM Коммит и пуш
git commit -m "Update %mydate% %mytime%"
git push

echo Готово!
pause
