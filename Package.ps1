param([string]$Destination = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$version = (Get-Content (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must contain a three-part version number.' }
$manifest = Get-Content (Join-Path $PSScriptRoot 'extension\manifest.json') -Raw | ConvertFrom-Json
if ($manifest.version -ne $version) { throw 'Set the extension manifest version to match VERSION.' }
$build = Join-Path $PSScriptRoot 'build'
& (Join-Path $PSScriptRoot 'Build.ps1') -Destination $build
$windows = New-Item -ItemType Directory -Path (Join-Path $build 'windows-package') -Force
$chrome = New-Item -ItemType Directory -Path (Join-Path $build 'chrome-package') -Force
Copy-Item -LiteralPath (Join-Path $build 'Rocksmith CDLC Manager.exe'),(Join-Path $PSScriptRoot 'README.md') -Destination $windows.FullName -Force
Copy-Item -Path (Join-Path $PSScriptRoot 'extension\*') -Destination $chrome.FullName -Recurse -Force
$iconFolder = New-Item -ItemType Directory -Path (Join-Path $chrome.FullName 'icons') -Force
foreach ($size in 16,32,48,128) {
    Copy-Item -LiteralPath (Join-Path $build "icons\icon-$size.png") -Destination $iconFolder.FullName -Force
}
$out = (New-Item -ItemType Directory -Path $Destination -Force).FullName
$desktopZip = Join-Path $out "Rocksmith-CDLC-Manager-$version-windows-x64.zip"
$chromeZip = Join-Path $out "Rocksmith-CDLC-Manager-$version-chrome.zip"
Compress-Archive -Path (Join-Path $windows.FullName '*') -DestinationPath $desktopZip -Force
Compress-Archive -Path (Join-Path $chrome.FullName '*') -DestinationPath $chromeZip -Force
$checksums = Get-FileHash -Algorithm SHA256 -LiteralPath $desktopZip,$chromeZip | ForEach-Object {
    "$($_.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_.Path))"
}
$checksums | Set-Content -LiteralPath (Join-Path $out 'SHA256SUMS.txt') -Encoding ascii
