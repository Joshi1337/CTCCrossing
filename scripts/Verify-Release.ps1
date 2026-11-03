. "$PSScriptRoot/Release-Common.ps1"
$manifest = Get-Content -LiteralPath (Join-Path $RepoRoot 'release/build-manifest.json') -Raw | ConvertFrom-Json
$actual = Get-ReleaseManifest
if ($actual.version -notmatch '^\d+\.\d+\.\d+$' -or $manifest.version -ne $actual.version) { throw 'Invalid or mismatched release version.' }
if ($manifest.files.Count -ne $actual.files.Count) { throw 'Release file list changed. Run scripts/Prepare-Release.ps1.' }
for ($i = 0; $i -lt $actual.files.Count; $i++) {
    if ($manifest.files[$i].path -cne $actual.files[$i].path -or $manifest.files[$i].sha256 -cne $actual.files[$i].sha256) {
        throw "Stale release build: $($actual.files[$i].path). Run scripts/Prepare-Release.ps1 before pushing."
    }
}
$dllVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $RepoRoot 'release/BrysonCrossingLamp.dll')).Version.ToString(3)
if ($dllVersion -ne $manifest.version) { throw 'DLL and manifest versions do not match.' }
Write-Host "Verified CTCCrossing $($manifest.version): source fingerprint, runtime files and DLL version match."

