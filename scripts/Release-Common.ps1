Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:RepoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Write-Utf8File([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
}

function Get-ReleaseFiles {
    $paths = [Collections.Generic.List[string]]::new()
    Get-ChildItem -LiteralPath (Join-Path $script:RepoRoot 'Source') -File -Recurse |
        Where-Object { $_.Extension -in '.cs', '.csproj' -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        ForEach-Object { $paths.Add($_.FullName.Substring($script:RepoRoot.Length + 1).Replace('\', '/')) }
    foreach ($path in @('Info.json', 'settings.json', 'lib/0Harmony.dll', 'release/BrysonCrossingLamp.dll')) { $paths.Add($path) }
    $result = $paths.ToArray()
    [Array]::Sort($result, [StringComparer]::Ordinal)
    return $result
}

function Get-ReleaseManifest {
    $info = Get-Content -LiteralPath (Join-Path $script:RepoRoot 'Info.json') -Raw | ConvertFrom-Json
    $files = @(foreach ($path in Get-ReleaseFiles) {
        [ordered]@{ path = $path; sha256 = (Get-FileHash -LiteralPath (Join-Path $script:RepoRoot $path) -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    return [ordered]@{ version = $info.version; files = $files }
}

