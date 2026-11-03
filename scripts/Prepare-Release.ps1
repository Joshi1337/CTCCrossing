[CmdletBinding()]
param(
    [string]$Version,
    [string]$GameManaged = (Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\Railroader\Railroader_Data\Managed')
)
. "$PSScriptRoot/Release-Common.ps1"
$infoPath = Join-Path $RepoRoot 'Info.json'
$originalInfo = [IO.File]::ReadAllText($infoPath)
$info = $originalInfo | ConvertFrom-Json
if (!$Version) {
    $old = [Version]$info.version
    $Version = '{0}.{1}.{2}' -f $old.Major, $old.Minor, ($old.Build + 1)
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a version such as 0.3.2.' }
if (!(Test-Path -LiteralPath (Join-Path $GameManaged 'Railroader.dll'))) { throw 'Railroader.dll not found. Set -GameManaged to your local Railroader_Data\Managed directory.' }
$GameManaged = (Resolve-Path -LiteralPath $GameManaged).Path
$buildOutput = Join-Path $RepoRoot '.build/mod'
Push-Location $RepoRoot
try {
    dotnet run --project Source/Tests/Tests.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed; no release prepared.' }
    dotnet build Source/BrysonCrossingLamp.csproj --configuration Release --nologo "-p:GameManaged=$GameManaged" "-p:Version=$Version" "-p:OutputPath=$buildOutput/"
    if ($LASTEXITCODE -ne 0) { throw 'Build failed; no release prepared.' }
    $dll = Join-Path $buildOutput 'BrysonCrossingLamp.dll'
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString(3)
    if ($assemblyVersion -ne $Version) { throw 'Built DLL version mismatch.' }
    New-Item -ItemType Directory -Force -Path release | Out-Null
    Copy-Item -LiteralPath $dll -Destination release/BrysonCrossingLamp.dll -Force
    $info.version = $Version
    Write-Utf8File $infoPath (($info | ConvertTo-Json -Depth 8) + "`n")
    Write-Utf8File (Join-Path $RepoRoot 'release/build-manifest.json') (((Get-ReleaseManifest) | ConvertTo-Json -Depth 8) + "`n")
    & "$PSScriptRoot/Verify-Release.ps1"
    Write-Host "Prepared CTCCrossing $Version. Commit the source, Info.json and release/ together, then push main."
} finally { Pop-Location }

