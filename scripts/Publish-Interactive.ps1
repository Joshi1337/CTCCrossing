[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repo
try {
    foreach ($command in @('git', 'dotnet')) {
        if (!(Get-Command $command -ErrorAction SilentlyContinue)) { throw "$command fehlt. Bitte installieren und erneut starten." }
    }
    $current = (Get-Content -LiteralPath 'Info.json' -Raw | ConvertFrom-Json).version
    Write-Host 'CTCCrossing - Update bauen und auf GitHub pushen'
    Write-Host "Projekt: $repo"
    $version = Read-Host "Version [Enter = $current]"
    if ([string]::IsNullOrWhiteSpace($version)) { $version = $current }
    $version = $version.Trim()
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Bitte eine Version wie 0.4.0 oder 0.4.1 eingeben.' }
    $message = Read-Host 'Kurze Beschreibung des Updates'
    if ([string]::IsNullOrWhiteSpace($message)) { throw 'Keine Beschreibung eingegeben. Abgebrochen.' }
    Write-Host "Version $version wird getestet, gebaut, committet und nach origin/main gepusht."
    Write-Host 'GitHub erstellt anschliessend das Release. Bereits veroeffentlichte Versionen nicht erneut verwenden.'
    if ((Read-Host 'Jetzt starten? JA eingeben') -ine 'JA') { Write-Host 'Abgebrochen.'; exit 0 }

    $branch = git branch --show-current
    if ($LASTEXITCODE -ne 0 -or $branch -ne 'main') { throw 'Bitte das lokale Projekt auf dem Branch main verwenden.' }
    $remote = git remote get-url origin
    if ($LASTEXITCODE -ne 0 -or $remote -notin @('https://github.com/Joshi1337/CTCCrossing.git', 'https://github.com/Joshi1337/CTCCrossing', 'git@github.com:Joshi1337/CTCCrossing.git')) {
        throw 'origin zeigt nicht auf Joshi1337/CTCCrossing. Bitte die Git-Konfiguration pruefen.'
    }
    git fetch origin main --tags
    if ($LASTEXITCODE -ne 0) { throw 'GitHub konnte nicht gelesen werden. Bitte Git-Anmeldung und Verbindung pruefen.' }
    git rev-parse --verify --quiet "refs/tags/v$version" *> $null
    if ($LASTEXITCODE -eq 0) { throw "v$version existiert bereits. Erneut starten und eine neue Version eingeben." }

    git rev-parse --verify --quiet HEAD *> $null
    if ($LASTEXITCODE -ne 0) {
        # First use of the prepared project: attach history without replacing working files.
        git reset --mixed origin/main
        if ($LASTEXITCODE -ne 0) { throw 'Die lokale Historie konnte nicht zugeordnet werden.' }
        git branch --set-upstream-to=origin/main main
        if ($LASTEXITCODE -ne 0) { throw 'Der Upstream konnte nicht gesetzt werden.' }
    }
    git merge-base --is-ancestor origin/main HEAD
    if ($LASTEXITCODE -ne 0) { throw 'GitHub enthaelt neuere Aenderungen. Bitte diese zuerst lokal zusammenfuehren; es wird nichts ueberschrieben.' }
    git var GIT_AUTHOR_IDENT *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Git-Name/E-Mail fehlen. Bitte git config user.name und git config user.email im Projekt setzen.' }

    & "$PSScriptRoot/Publish-Update.ps1" -Version $version -Message $message
    Write-Host "Update $version erfolgreich gepusht."
    Write-Host 'Release-Fortschritt: https://github.com/Joshi1337/CTCCrossing/actions'
} catch {
    Write-Host ("FEHLER: " + $_.Exception.Message) -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}
