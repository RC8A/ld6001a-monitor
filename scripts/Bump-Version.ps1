param([ValidateSet('Patch','Minor','Major')][string]$Part='Patch')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$path=Join-Path $root 'version.txt'
$version=[version]([IO.File]::ReadAllText($path).Trim())
$next=switch($Part){'Major'{"$($version.Major+1).0.0"};'Minor'{"$($version.Major).$($version.Minor+1).0"};'Patch'{"$($version.Major).$($version.Minor).$($version.Build+1)"}}
[IO.File]::WriteAllText($path,$next+"`n",(New-Object Text.UTF8Encoding($false)))
Write-Output "Release version: $next"
