. "$PSScriptRoot/Release-Common.ps1"
& "$PSScriptRoot/Verify-Release.ps1"
$info = Get-Content -LiteralPath (Join-Path $RepoRoot 'Info.json') -Raw | ConvertFrom-Json
$staging = Join-Path $RepoRoot '.release'
$payload = Join-Path $staging 'CTCCrossing'
New-Item -ItemType Directory -Force -Path $payload | Out-Null
foreach ($file in @('Info.json', 'settings.json', 'README.md', 'LICENSE')) { Copy-Item -LiteralPath (Join-Path $RepoRoot $file) -Destination $payload -Force }
Copy-Item -LiteralPath (Join-Path $RepoRoot 'release/BrysonCrossingLamp.dll') -Destination $payload -Force
Copy-Item -LiteralPath (Join-Path $RepoRoot 'lib/0Harmony.dll') -Destination $payload -Force
Copy-Item -LiteralPath (Join-Path $RepoRoot 'lib/Harmony-LICENSE.txt') -Destination $payload -Force
$expected = @('Info.json', 'settings.json', 'README.md', 'LICENSE', 'BrysonCrossingLamp.dll', '0Harmony.dll', 'Harmony-LICENSE.txt') | Sort-Object
$present = @(Get-ChildItem -LiteralPath $payload -File | Select-Object -ExpandProperty Name | Sort-Object)
if (@(Compare-Object $expected $present).Count -ne 0 -or @(Get-ChildItem -LiteralPath $payload -Directory).Count -gt 0) { throw 'Unexpected files in package staging directory. Use a fresh checkout.' }
$zip = Join-Path $staging "CTCCrossing-$($info.version).zip"
Compress-Archive -LiteralPath $payload -DestinationPath $zip -Force
$sum = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Utf8File "$zip.sha256" "$sum  $([IO.Path]::GetFileName($zip))`n"
Write-Host "Packaged $zip"

