. "$PSScriptRoot/Release-Common.ps1"
& "$PSScriptRoot/Verify-Release.ps1"
$info = Get-Content -LiteralPath (Join-Path $RepoRoot 'Info.json') -Raw | ConvertFrom-Json
$tag = "v$($info.version)"
$zip = Join-Path $RepoRoot ".release/CTCCrossing-$($info.version).zip"
if (!$env:GITHUB_SHA -or !$env:GH_REPO) { throw 'Run in GitHub Actions with GITHUB_SHA and GH_REPO.' }
$existingJson = gh release view $tag --json isDraft,targetCommitish 2>$null
if ($LASTEXITCODE -eq 0) {
    $existing = $existingJson | ConvertFrom-Json
    if (!$existing.isDraft) {
        $remote = gh api "repos/$env:GH_REPO/contents/release/build-manifest.json?ref=$tag" --jq .content
        if ($LASTEXITCODE -ne 0) { throw 'Cannot verify already-published release; refusing to overwrite.' }
        $old = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(($remote -join ''))).Trim()
        $current = [IO.File]::ReadAllText((Join-Path $RepoRoot 'release/build-manifest.json')).Trim()
        if ($old -cne $current) { throw "Release $tag already exists with different content. Prepare a new version." }
        Write-Host "$tag is already published with the same verified content. Nothing to do."
        exit 0
    }
    if ($existing.targetCommitish -ne $env:GITHUB_SHA) { throw 'An existing draft points to a different commit; refusing to replace it.' }
} else {
    $notes = @"
CTCCrossing $($info.version) for Railroader Experimental.

Download **CTCCrossing-$($info.version).zip** below (the source-code archives are not installable mods).

See the README for installation, AUTO/CLOSE controls and remote-position limitations.

Built locally against Railroader; source fingerprints and automated tests checked by GitHub Actions.
Source commit: $env:GITHUB_SHA
"@
    $notesPath = Join-Path $RepoRoot '.release/notes.md'
    Write-Utf8File $notesPath $notes
    gh release create $tag --draft --target $env:GITHUB_SHA --title "CTCCrossing $($info.version)" --notes-file $notesPath
    if ($LASTEXITCODE -ne 0) { throw 'Draft release creation failed.' }
}
gh release upload $tag $zip "$zip.sha256" --clobber
if ($LASTEXITCODE -ne 0) { throw 'Asset upload failed; release remains a draft.' }
gh release edit $tag --draft=false --latest
if ($LASTEXITCODE -ne 0) { throw 'Release publication failed.' }
Write-Host "Published $tag."
