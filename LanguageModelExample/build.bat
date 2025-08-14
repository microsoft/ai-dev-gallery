@echo off
echo 正在构建 LanguageModelExample 项目...

REM 清理之前的构建
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"

REM 还原包
dotnet restore

REM 构建项目
dotnet build --configuration Release --platform x64

if %ERRORLEVEL% EQU 0 (
    echo 构建成功！
    echo 输出目录: bin\Release\net8.0-windows10.0.26100.0\win-x64\
) else (
    echo 构建失败！
    pause
)

pause 