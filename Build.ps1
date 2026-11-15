param([string]$Destination = (Join-Path $PSScriptRoot 'build'))
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$compiler = Join-Path $framework 'csc.exe'
$binary = Join-Path (New-Item -ItemType Directory -Path $Destination -Force).FullName 'Rocksmith CDLC Manager.exe'
$assets = Join-Path $Destination 'icons'
& (Join-Path $PSScriptRoot 'scripts\New-Icons.ps1') -Destination $assets
$ico = (Resolve-Path (Join-Path $assets App.ico)).Path
$desktop = Join-Path $PSScriptRoot 'desktop'
$shell = Join-Path $desktop 'Shell.xaml'
$sources = @(Get-ChildItem -LiteralPath $desktop -Filter '*.cs' | ForEach-Object FullName)
$references = @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll','System.Xaml.dll','UIAutomationTypes.dll') | ForEach-Object {
    $reference = Join-Path $wpf $_
    if (!(Test-Path $reference)) { $reference = Join-Path $framework $_ }
    "/r:$reference"
}
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$binary" "/win32icon:$ico" "/resource:$ico,App.ico" "/resource:$shell,Shell.xaml" /r:System.Windows.Forms.dll /r:System.Net.Http.dll /r:System.Web.Extensions.dll @references $sources
if ($LASTEXITCODE) { throw 'Windows build failed.' }
