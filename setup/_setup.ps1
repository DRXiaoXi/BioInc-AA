# =============================================================
#  Bio Inc. Redemption MOD 安装程序 (BioIncAA v1.2.0 + BepInEx 5.4.23.2)
#  用法:
#    安装:  _setup.ps1                (或 setup.ps1 / install.ps1)
#    卸载:  _setup.ps1 uninstall
#    静默:  _setup.ps1 -Silent        (自动确认所有提示)
# =============================================================
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('install', 'uninstall', 'repair')]
    [string]$Action = 'install',

    [switch]$Silent
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$ModName    = 'BioIncAA (抗锯齿 + 帧率解锁)'
$ModVersion = '1.3.0'
$ModAuthor  = '哔哩哔哩: 逐梦之子-晓夕'
$StampFile  = 'BepInEx\BioIncAA.version'
# 本脚本与 payload 文件同目录 (payload\_setup.ps1); 打包成自解压 exe 后同样同目录
$PayloadDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Step($msg)  { Write-Host "  [*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "  [+] $msg" -ForegroundColor Green }
function Write-Warn2($msg) { Write-Host "  [!] $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "  [x] $msg" -ForegroundColor Red }

# ---------- 定位游戏 ----------
function Find-Game {
    $candidates = @()

    # 1) 当前目录就是游戏目录?
    if (Test-Path (Join-Path $PWD 'BioIncRedemption.exe')) { $candidates += $PWD.Path }

    # 2) Steam 注册表
    try {
        $steam = Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
        if ($steam.SteamPath) {
            $steamPath = $steam.SteamPath -replace '/', '\'
            $libFile = Join-Path $steamPath 'steamapps\libraryfolders.vdf'
            if (Test-Path $libFile) {
                $libs = Select-String -Path $libFile -Pattern '"path"\s+"([^"]+)"' -AllMatches |
                        ForEach-Object { $_.Matches[0].Groups[1].Value -replace '\\\\', '\' }
                $libs = @($steamPath) + @($libs)
                foreach ($lib in ($libs | Select-Object -Unique)) {
                    $p = Join-Path $lib 'steamapps\common\Bio Inc. Redemption'
                    if (Test-Path (Join-Path $p 'BioIncRedemption.exe')) { $candidates += $p }
                }
            }
        }
    } catch { }

    # 3) 常见盘符探测
    foreach ($drive in (Get-PSDrive -PSProvider FileSystem)) {
        foreach ($prefix in @('', 'Steam', 'SteamLibrary', 'Games\Steam', 'Program Files (x86)\Steam', 'Program Files\Steam')) {
            $p = Join-Path $drive.Root "$prefix\steamapps\common\Bio Inc. Redemption"
            if (Test-Path (Join-Path $p 'BioIncRedemption.exe')) { $candidates += $p }
        }
    }

    return $candidates | Select-Object -Unique
}

# ---------- MOD 文件清单 ----------
$ModFiles = @(
    'winhttp.dll',
    'doorstop_config.ini',
    '.doorstop_version',
    'BepInEx\BioIncAA.version'
)
$ModDirs = @('BepInEx\core', 'BepInEx\patchers', 'BepInEx\plugins')

function Test-ModInstalled($game) {
    return Test-Path (Join-Path $game $StampFile)
}

# ---------- 备份 ----------
function Backup-Existing($game) {
    $bak = Join-Path $game 'MOD_BACKUP'
    $first = -not (Test-Path $bak)
    if ($first) { New-Item $bak -ItemType Directory -Force | Out-Null }
    foreach ($f in @('winhttp.dll', 'doorstop_config.ini', '.doorstop_version')) {
        $src = Join-Path $game $f
        $dst = Join-Path $bak $f
        if ((Test-Path $src) -and -not (Test-Path $dst)) {
            Copy-Item $src $dst -Force
            Write-Step "备份 $f"
        }
    }
}

# ---------- 执行安装 ----------
function Install-Mod($game) {
    Write-Step "目标目录: $game"

    $proc = Get-Process BioIncRedemption -ErrorAction SilentlyContinue
    if ($proc) {
        if (-not $Silent) {
            $ans = Read-Host "  游戏正在运行, 必须关闭才能安装。现在关闭? (Y/n)"
            if ($ans -eq 'n') { Write-Fail '已取消'; exit 1 }
        }
        Stop-Process -Name BioIncRedemption -Force
        Start-Sleep -Seconds 3
        Write-Ok '已关闭游戏'
    }

    Backup-Existing $game

    Write-Step '释放文件...'
    Copy-Item (Join-Path $PayloadDir '*') $game -Recurse -Force
    # 保险: 重新拷贝最新插件(如果工作区有)
    $newest = Get-ChildItem "D:\我要干大活\生化公司修改版\BioIncAA\bin\Release\BioIncAA.dll" -ErrorAction SilentlyContinue
    if ($newest) { Copy-Item $newest.FullName (Join-Path $game 'BepInEx\plugins\BioIncAA.dll') -Force }

    Set-Content (Join-Path $game $StampFile) "BioIncAA v$ModVersion`nAuthor: $ModAuthor`nInstalled: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -Encoding UTF8
    Write-Ok "已安装 $ModName v$ModVersion"
}

# ---------- 卸载 ----------
function Uninstall-Mod($game) {
    Write-Step "目标目录: $game"
    $proc = Get-Process BioIncRedemption -ErrorAction SilentlyContinue
    if ($proc) { Stop-Process -Name BioIncRedemption -Force; Start-Sleep -Seconds 3; Write-Ok '已关闭游戏' }

    foreach ($f in $ModFiles) {
        $p = Join-Path $game $f
        if (Test-Path $p) { Remove-Item $p -Force; Write-Ok "删除 $f" }
    }
    foreach ($d in $ModDirs) {
        $p = Join-Path $game $d
        if (Test-Path $p) { Remove-Item $p -Recurse -Force; Write-Ok "删除 $d\" }
    }
    # cache/config/logs 也一并清理 (config 是 MOD 专属, 不含原版数据)
    foreach ($d in @('BepInEx\cache', 'BepInEx\config', 'BepInEx\LogOutput.log', 'BepInEx\update_tmp')) {
        $p = Join-Path $game $d
        if (Test-Path $p) { Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue }
    }
    $bepDir = Join-Path $game 'BepInEx'
    if ((Test-Path $bepDir) -and ((Get-ChildItem $bepDir -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0)) {
        Remove-Item $bepDir -Force
        Write-Ok '删除 BepInEx'
    }
    # 安装时自动创建的备份目录也一并清除
    $mb = Join-Path $game 'MOD_BACKUP'
    if (Test-Path $mb) { Remove-Item $mb -Recurse -Force; Write-Ok '删除 MOD_BACKUP\' }
    # 提示: 若用户曾把安装包解压进游戏目录, 这里会留下安装器自身的几个文件(非MOD文件)
    $installerCopies = @('安装MOD.bat', '卸载MOD.bat', '_setup.ps1', '使用说明.txt') | Where-Object { Test-Path (Join-Path $game $_) }
    if ($installerCopies.Count -gt 0 -and (Join-Path $game '_setup.ps1') -ne $PSCommandPath) {
        Write-Warn2 ('游戏目录下还有解压进去的安装器文件: ' + ($installerCopies -join ', '))
        Write-Warn2 '它们不是 MOD 文件; 如不再需要可手动删除, 或保留用于下次安装'
    }
    Write-Ok '原版文件未受任何影响'
}

# ---------- 主流程 ----------
Write-Host ''
Write-Host "===============================================" -ForegroundColor Magenta
Write-Host "  Bio Inc. Redemption MOD 安装程序" -ForegroundColor Magenta
Write-Host "  $ModName v$ModVersion + BepInEx 5.4.23.2" -ForegroundColor Magenta
Write-Host "  开发: $ModAuthor" -ForegroundColor Magenta
Write-Host "===============================================" -ForegroundColor Magenta
Write-Host ''

if (-not (Test-Path $PayloadDir)) { Write-Fail "找不到 payload 目录: $PayloadDir"; exit 1 }

$games = Find-Game
if ($games.Count -eq 0) {
    Write-Fail '未找到游戏 (BioIncRedemption.exe)'
    $manual = Read-Host '  请手动输入游戏目录 (直接回车退出)'
    if ([string]::IsNullOrWhiteSpace($manual)) { exit 1 }
    if (-not (Test-Path (Join-Path $manual 'BioIncRedemption.exe'))) { Write-Fail '该目录下没有 BioIncRedemption.exe'; exit 1 }
    $games = @($manual.Trim())
}

$game = $games[0]
if ($games.Count -gt 1 -and -not $Silent) {
    Write-Host '  找到多个安装位置:'
    for ($i = 0; $i -lt $games.Count; $i++) { Write-Host "    [$($i+1)] $($games[$i])" }
    $sel = Read-Host "  选择 (1, 默认)"
    if (-not [int]::TryParse($sel, [ref]$null) -and $sel) {
        $idx = [int]$sel - 1
        if ($idx -ge 0 -and $idx -lt $games.Count) { $game = $games[$idx] }
    }
}

$installed = Test-ModInstalled $game
if ($Action -eq 'install') { if ($installed) { $Action = 'repair' } }
elseif ($Action -eq 'repair' -and -not $installed) { $Action = 'install' }

switch ($Action) {
    'uninstall' {
        if (-not $Silent) {
            $ans = Read-Host "  确认从以下目录卸载 MOD?`n  $game`n  (Y/n)"
            if ($ans -eq 'n') { Write-Fail '已取消'; exit 0 }
        }
        Uninstall-Mod $game
        Write-Host ''
        Write-Ok '卸载完成。游戏已恢复原版。'
    }
    default {
        $verb = if ($Action -eq 'repair') { '修复(重装)' } else { '安装' }
        if (-not $Silent -and $installed) {
            Write-Warn2 "检测到已安装 $ModName, 将执行重装修复(保留你的设置)"
        }
        Install-Mod $game
        Write-Host ''
        Write-Ok "$verb 完成! 启动游戏即可使用:"
        Write-Host '    设置 -> 画质 -> 抗锯齿 (关闭/2x/4x/8x)' -ForegroundColor White
        Write-Host '    设置 -> 画质 -> 帧率    (默认60/90/120/144/165/240/不限)' -ForegroundColor White
        Write-Host '    另外修复了设置面板越开越宽的原生bug' -ForegroundColor White
        Write-Host ''
        Write-Warn2 '卸载方法: 再次运行本程序选择 [U] 卸载; 或直接删除游戏目录下 BepInEx 文件夹与 winhttp.dll'
    }
}
