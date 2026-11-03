[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Message,
    [string]$Version,
    [string]$GameManaged = (Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\Railroader\Railroader_Data\Managed')
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repo
try {
    $branch = git branch --show-current
    if ($LASTEXITCODE -ne 0 -or $branch -ne 'main') { throw 'Run this from the main branch of your CTCCrossing clone.' }
    $conflicts = git diff --name-only --diff-filter=U
    if ($LASTEXITCODE -ne 0 -or $conflicts) { throw 'Resolve git conflicts first.' }
    & "$PSScriptRoot/Prepare-Release.ps1" -Version $Version -GameManaged $GameManaged
    git add --all
    if ($LASTEXITCODE -ne 0) { throw 'git add failed.' }
    git commit -m $Message
    if ($LASTEXITCODE -ne 0) { throw 'git commit failed.' }
    git push origin main
    if ($LASTEXITCODE -ne 0) { throw 'Push failed. Your local commit is preserved; resolve the GitHub error and retry git push origin main.' }
} finally { Pop-Location }

