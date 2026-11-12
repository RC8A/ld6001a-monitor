param([switch]$NoLaunch,[int]$BuildNumber=0)
$ErrorActionPreference='Stop'
$projectDir=$PSScriptRoot
$outputDir=Split-Path -Parent $projectDir
$appName='LD6001A Monitor'
$exePath=Join-Path $outputDir ($appName+'.exe')
$version=([IO.File]::ReadAllText((Join-Path $outputDir 'version.txt'))).Trim()
if($version -notmatch '^\d+\.\d+\.\d+$') {throw 'version.txt must contain major.minor.patch'}
$counterPath=Join-Path $projectDir '.build-number'
if($BuildNumber -eq 0) {
    $previous=0
    if(Test-Path -LiteralPath $counterPath) {$previous=[int]([IO.File]::ReadAllText($counterPath)).Trim()}
    $BuildNumber=$previous+1
}
if($BuildNumber -lt 1 -or $BuildNumber -gt 65534) {throw 'Build number must be 1..65534'}
foreach($process in (Get-Process -Name 'LD6001A Monitor','LD Monitor' -ErrorAction SilentlyContinue)) {
    if($process.Path -eq $exePath -or $process.Path -eq (Join-Path $outputDir 'LD Monitor.exe')) {
        if(-not $process.CloseMainWindow()){Stop-Process -Id $process.Id -Force}
        elseif(-not $process.WaitForExit(5000)){Stop-Process -Id $process.Id -Force}
    }
}
$generatedDir=Join-Path $projectDir '.generated'
New-Item -ItemType Directory -Path $generatedDir -Force | Out-Null
$metadata=@"
using System.Reflection;
[assembly: AssemblyTitle("$appName")]
[assembly: AssemblyProduct("$appName")]
[assembly: AssemblyDescription("Independent HLK-LD6001A radar testing utility")]
[assembly: AssemblyCompany("Dmitry Popov")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Dmitry Popov")]
[assembly: AssemblyVersion("$version.$BuildNumber")]
[assembly: AssemblyFileVersion("$version.$BuildNumber")]
[assembly: AssemblyInformationalVersion("$version+build.$BuildNumber")]
"@
$metadataPath=Join-Path $generatedDir 'AssemblyInfo.cs'
[IO.File]::WriteAllText($metadataPath,$metadata,(New-Object Text.UTF8Encoding($true)))
$frameworkDir=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler=Join-Path $frameworkDir 'csc.exe'
$references=@('System.dll','System.Core.dll','System.Drawing.dll',(Join-Path $frameworkDir 'System.Xaml.dll'),(Join-Path $frameworkDir 'WPF\WindowsBase.dll'),(Join-Path $frameworkDir 'WPF\PresentationCore.dll'),(Join-Path $frameworkDir 'WPF\PresentationFramework.dll'))
$compilerArgs=@('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output',"/out:$exePath")
foreach($reference in $references){$compilerArgs+="/reference:$reference"}
$compilerArgs+="/resource:$(Join-Path $projectDir 'Main.xaml'),RadarTest.Main.xaml"
$compilerArgs+="/win32icon:$(Join-Path $projectDir 'Assets\ld-monitor.ico')"
$compilerArgs+="/resource:$(Join-Path $projectDir 'Assets\ld-monitor.ico'),RadarTest.AppIcon.ico"
$compilerArgs+="/resource:$(Join-Path $projectDir 'Assets\ld-monitor-logo.png'),RadarTest.Logo.png"
foreach($file in @('AppIdentity.cs','Language.cs','Protocol.cs','Settings.cs','Zones.cs','ParameterModel.cs','Trails.cs','CoreTests.cs','NumberField.cs','RadarCanvas.cs','WpfRows.cs','WpfMonitor.cs','WpfProgram.cs')){$compilerArgs+=(Join-Path $projectDir $file)}
$compilerArgs+=$metadataPath
& $compiler @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Build failed'}
foreach($testArgument in @('--self-test','--smoke-test')) {
    $testProcess=Start-Process -FilePath $exePath -ArgumentList $testArgument -WindowStyle Hidden -Wait -PassThru
    if($testProcess.ExitCode -ne 0){Get-Content -LiteralPath (Join-Path $outputDir 'error.txt') -ErrorAction SilentlyContinue;throw "Test failed: $testArgument"}
}
[IO.File]::WriteAllText($counterPath,[string]$BuildNumber)
Write-Output "Built and tested: $appName $version (build $BuildNumber)"
if(-not $NoLaunch){Start-Process -FilePath $exePath -WorkingDirectory $outputDir -WindowStyle Normal}
