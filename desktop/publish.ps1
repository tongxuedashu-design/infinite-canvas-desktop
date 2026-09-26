$ErrorActionPreference = 'Stop'
$desktopDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $desktopDir 'publish'

dotnet publish (Join-Path $desktopDir 'InfiniteCanvasDesktop.csproj') `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDir

$executable = Join-Path $publishDir 'InfiniteCanvasDesktop.exe'
$desktopPath = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktopPath '无限画布.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $executable
$shortcut.WorkingDirectory = $publishDir
$shortcut.Description = '启动无限画布桌面端'
$shortcut.IconLocation = "$executable,0"
$shortcut.Save()

Write-Host "已生成：$executable"
Write-Host "已创建桌面快捷方式：$shortcutPath"
