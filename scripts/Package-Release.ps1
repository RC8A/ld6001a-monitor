param()
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$version=([IO.File]::ReadAllText((Join-Path $root 'version.txt'))).Trim()
$exe=Join-Path $root 'LD6001A Monitor.exe'
if(-not(Test-Path -LiteralPath $exe)){throw 'Run Source/Build.ps1 first'}
$info=(Get-Item -LiteralPath $exe).VersionInfo
if($info.ProductVersion.Split('+')[0] -ne $version){throw 'Executable version does not match version.txt'}
$releaseDir=Join-Path $root 'dist'
$stage=Join-Path $releaseDir ('LD6001A-Monitor-'+$version+'-win-x64')
if(Test-Path -LiteralPath $stage){throw 'Release staging already exists; choose a new version or inspect dist first'}
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath $exe -Destination $stage
foreach($file in @('README.md','CHANGELOG.md','COPYRIGHT','LICENSE.txt')){Copy-Item -LiteralPath (Join-Path $root $file) -Destination $stage}
$manifest=[ordered]@{product='LD6001A Monitor';version=$version;fileVersion=$info.FileVersion;target='Windows 10 x64';framework='.NET Framework 4.7.2 or newer';license='PolyForm-Noncommercial-1.0.0';files=@('LD6001A Monitor.exe','README.md','CHANGELOG.md','COPYRIGHT','LICENSE.txt')}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $stage 'manifest.json') -Encoding UTF8
$zip=Join-Path $releaseDir ('LD6001A-Monitor-'+$version+'-win-x64.zip')
Compress-Archive -LiteralPath $stage -DestinationPath $zip -CompressionLevel Optimal
$hash=Get-FileHash -LiteralPath $zip -Algorithm SHA256
[IO.File]::WriteAllText($zip+'.sha256',$hash.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($zip)+"`n")
Write-Output $zip
