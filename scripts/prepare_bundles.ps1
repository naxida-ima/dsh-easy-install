# prepare_bundles.ps1 —— 在 Windows 构建机上组装全部离线资源
# 产出：_assets/node.zip（Node 便携版，根=node.exe）
#       _assets/dsh.zip（@deepseek-ai/dsh 完整依赖树，根=node_modules）
#       _assets/switch.zip（桌面开关 onedir，根=switch.exe）
#       _assets/bundle_info.json / _assets/checksums.json
param(
    [string]$NodeVersion = "24.19.0",
    [string]$DshPackage = "@deepseek-ai/dsh"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root "_assets"
$work = Join-Path $root "build\_bundle_work"
$makeZip = Join-Path $root "scripts\make_zip.py"
New-Item -ItemType Directory -Force -Path $assets, $work | Out-Null
Write-Host "==> work dir: $work"

# ---------- 1. Node 便携版 ----------
$nodeZipUrl = "https://nodejs.org/dist/v$NodeVersion/node-v$NodeVersion-win-x64.zip"
$nodeZip = Join-Path $work "node-src.zip"
Write-Host "==> downloading Node $NodeVersion ..."
Invoke-WebRequest -Uri $nodeZipUrl -OutFile $nodeZip -UseBasicParsing
$nodeExtract = Join-Path $work "node-src"
if (Test-Path $nodeExtract) { Remove-Item -Recurse -Force $nodeExtract }
Expand-Archive -Path $nodeZip -DestinationPath $nodeExtract -Force
$nodeDir = Get-ChildItem $nodeExtract -Directory | Select-Object -First 1
if (-not $nodeDir) { throw "node zip has no top-level dir" }
Write-Host "==> repacking node.zip (strip top dir)"
python $makeZip $nodeDir.FullName (Join-Path $assets "node.zip")
$npmCmd = Join-Path $nodeDir.FullName "npm.cmd"
$nodeExe = Join-Path $nodeDir.FullName "node.exe"
$nodeVer = & $nodeExe --version
Write-Host "==> node version: $nodeVer"

# ---------- 2. dsh 离线依赖 ----------
$dshApp = Join-Path $work "dsh-app"
if (Test-Path $dshApp) { Remove-Item -Recurse -Force $dshApp }
New-Item -ItemType Directory -Force -Path $dshApp | Out-Null
Write-Host "==> npm install $DshPackage (Windows platform) ..."
Push-Location $dshApp
& $npmCmd install $DshPackage --no-audit --no-fund --ignore-scripts=false --foreground-scripts 2>&1 | Write-Host
if ($LASTEXITCODE -ne 0) { throw "npm install failed: $LASTEXITCODE" }
Pop-Location

# dsh 版本（从装好的包读取，最准）
$dshPkgJson = Join-Path $dshApp "node_modules\@deepseek-ai\dsh\package.json"
$dshVer = (Get-Content $dshPkgJson -Raw | ConvertFrom-Json).version
Write-Host "==> dsh version: $dshVer"

# 去除 dev 痕迹 + 平台无关大文件（可选裁剪，保证体积可控）
$trim = Join-Path $root "scripts\trim_bundle.ps1"
if (Test-Path $trim) { & $trim -AppDir $dshApp }

Write-Host "==> packing dsh.zip"
python $makeZip $dshApp (Join-Path $assets "dsh.zip")

# ---------- 3. switch.zip（PyInstaller onedir 输出） ----------
$switchOut = Join-Path $root "dist\switch"
if (Test-Path $switchOut) {
    Write-Host "==> packing switch.zip"
    python $makeZip $switchOut (Join-Path $assets "switch.zip")
} else {
    throw "switch build output not found: $switchOut"
}

# ---------- 4. 元信息 + 校验 ----------
$info = @{
    dsh_version  = $dshVer
    node_version = $nodeVer
    built_at     = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
} | ConvertTo-Json
Set-Content -Path (Join-Path $assets "bundle_info.json") -Value $info -Encoding UTF8

$checks = @{}
Get-ChildItem (Join-Path $assets "*.zip") | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower()
    $checks[$_.Name] = $hash
    Write-Host ("==> {0}  {1:N1} MB  sha256={2}" -f $_.Name, ($_.Length / 1MB), $hash)
}
($checks | ConvertTo-Json) | Set-Content -Path (Join-Path $assets "checksums.json") -Encoding UTF8

Write-Host "==> bundles ready:"
Get-ChildItem $assets | ForEach-Object { Write-Host ("    {0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB)) }
