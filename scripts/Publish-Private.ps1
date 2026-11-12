param([string]$Repository='RC8A/ld6001a-monitor')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$version=([IO.File]::ReadAllText((Join-Path $root 'version.txt'))).Trim()
& gh auth status
if($LASTEXITCODE -ne 0){throw 'Authenticate GitHub CLI first: gh auth login'}
$status=& git -C $root status --porcelain
if($LASTEXITCODE -ne 0 -or $status){throw 'Commit and review the local repository before publishing'}
$archive=Join-Path $root ('dist\LD6001A-Monitor-'+$version+'-win-x64.zip')
if(-not(Test-Path -LiteralPath $archive)){throw 'Prepare the release package first'}
$ErrorActionPreference='Continue'
& gh repo view $Repository --json nameWithOwner,isPrivate 2>$null | Out-Null
$lookupExit=$LASTEXITCODE
$ErrorActionPreference='Stop'
if($lookupExit -ne 0){
    & gh repo create $Repository --private --source $root --remote origin --push
    if($LASTEXITCODE -ne 0){throw 'Failed to create private repository'}
} else {
    $repo=& gh repo view $Repository --json isPrivate | ConvertFrom-Json
    if(-not $repo.isPrivate){throw 'Refusing to publish into a public repository'}
    $origin=& git -C $root remote get-url origin 2>$null
    if($LASTEXITCODE -ne 0){& git -C $root remote add origin ('https://github.com/'+$Repository+'.git')}
    elseif($origin -notmatch [regex]::Escape($Repository)+'(?:\.git)?$'){throw 'origin points to a different repository'}
    & git -C $root push origin main
    if($LASTEXITCODE -ne 0){throw 'Push failed'}
}
& gh release create ('v'+$version) $archive ($archive+'.sha256') --repo $Repository --draft --title ('LD6001A Monitor '+$version) --notes-file (Join-Path $root 'RELEASE-NOTES.md')
if($LASTEXITCODE -ne 0){throw 'Draft release failed'}
Write-Output 'Private repository and draft release are ready for review.'
