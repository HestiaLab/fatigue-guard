[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$cliProject = Join-Path $repoRoot 'src/FatigueGuard.Cli/FatigueGuard.Cli.csproj'
$wixProject = Join-Path $repoRoot 'installer/FatigueGuard.Installer.wixproj'
$staging = Join-Path $repoRoot 'artifacts/installer/staging'
$releaseDir = Join-Path $repoRoot 'artifacts/release'

function Assert-Success([string]$Step) {
    if ($LASTEXITCODE -ne 0) {
        throw "$Step 失败（退出码 $LASTEXITCODE）"
    }
}

$version = ([xml](Get-Content -LiteralPath $cliProject -Raw)).Project.PropertyGroup.Version |
    Where-Object { $_ } |
    Select-Object -First 1
if (-not $version) {
    throw "无法从 $cliProject 读取 <Version>"
}

if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}

New-Item -ItemType Directory -Path $staging -Force | Out-Null

Write-Host "发布 FatigueGuard $version（win-x64 自包含单文件）…"
dotnet publish $cliProject -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $staging
Assert-Success 'dotnet publish'

# 安装包不携带调试符号。
Get-ChildItem -LiteralPath $staging -Filter '*.pdb' | Remove-Item -Force

foreach ($doc in @('LICENSE', 'NOTICE', 'README.md', 'THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $doc) -Destination $staging
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'licenses') -Destination $staging -Recurse

Write-Host '构建 MSI…'
dotnet build $wixProject -c $Configuration "-p:ProductVersion=$version" "-p:StagingDir=$staging"
Assert-Success 'MSI 构建'

$msi = Get-ChildItem -Path (Join-Path $repoRoot 'installer/bin') -Recurse -Filter '*.msi' -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $msi) {
    throw '没有找到 MSI 输出文件'
}

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
$target = Join-Path $releaseDir "FatigueGuard-$version-win-x64.msi"
Copy-Item -LiteralPath $msi.FullName -Destination $target -Force

Write-Host "MSI: $target"
