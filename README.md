# CTCCrossing

[![Test and release](https://github.com/Joshi1337/CTCCrossing/actions/workflows/release.yml/badge.svg)](https://github.com/Joshi1337/CTCCrossing/actions/workflows/release.yml)
[![Latest release](https://img.shields.io/github/v/release/Joshi1337/CTCCrossing)](https://github.com/Joshi1337/CTCCrossing/releases/latest)

Crossing indicators and **AUTO / CLOSE** switches for Railroader's original **3D CTC panel in Bryson**.

[Download the latest release](https://github.com/Joshi1337/CTCCrossing/releases/latest) · [Deutsche Anleitung](docs/README.de.md) · [Report a problem](https://github.com/Joshi1337/CTCCrossing/issues)

## Features

- Separate indicators for **Bryson Everett, Governors Island, Sylva Allen and Andrews**.
- **Flashing red:** gates are closing. **Steady red:** closed position reported or calculated. **Off:** opening/open, or feedback unavailable.
- An **AUTO / CLOSE** switch beside each indicator: click its plate or handle to toggle.
- **AUTO** leaves the game's train detection in charge. **CLOSE** closes and holds the gates, with the existing crossing lights and bell.
- Returning to AUTO does **not** force the gates open if a train still requires them closed.
- Remote indication continues while distant gate animations are disabled by the game.
- **F8** opens diagnostics for all four crossings.

## Requirements and installation

- Railroader **Experimental**, built and inspected against **0.6.2**. The manifest accepts 0.6.1+, but other versions are not guaranteed.
- The base map and the original Bryson CTC panel.
- DLL mods enabled in Railroader. Harmony is included; no separate mod loader is needed.

1. Close Railroader completely.
2. Download **`CTCCrossing-<version>.zip`** from Releases. GitHub's automatic “Source code” archives are for developers, not installation.
3. Extract the `CTCCrossing` folder into `%USERPROFILE%\Documents\Railroader\Mods`.
4. Check that `Info.json`, `BrysonCrossingLamp.dll`, `0Harmony.dll` and `settings.json` are directly inside that folder.
5. Start Railroader, enable **CTCCrossing** in Manage Mods and click **Apply**. Load your save and visit the Bryson panel.

### Upgrading an earlier prototype

Earlier versions used a folder named `BrysonCrossingLamp`. **Update the files inside that existing folder instead of installing a second copy.** The folder name is not the mod's identity. Keep your `settings.json` if you adjusted the layout, then restart the game.

The internal ID `joshi/bryson-crossing-lamp` and DLL name are deliberately retained for compatibility. Do not enable two copies of the same mod.

## Controls and limitations

| Control / indicator | Meaning |
| --- | --- |
| AUTO | Original train detection controls the crossing |
| CLOSE | Operator requests and holds closure |
| Flashing red | Closing, approximately 0.5 seconds on / 0.5 seconds off |
| Steady red | Closed animation endpoint, or elapsed calculated travel time at a distant crossing |
| F8 | Diagnostics, crossing mapping, control mode and feedback status |

**Distant steady-red indication is calculated, not an independent end-position sensor.** When the animation is disabled, the mod tracks the game's gate command using clip length and animation speed. Visible animation feedback corrects the estimate when available. F8 identifies calculated remote indication. Missing controllers or invalid travel metadata still leave the lamp off; **off is not proof that a crossing is open**.

- Operator switches work in single player or on the **multiplayer host**. Clients cannot issue commands. Manual switch positions are not synchronized to clients.
- Manual CLOSE selections are **not saved**. Loading a world starts the switches on AUTO; unloading the mod returns control to the game.
- Indicators respond to the gate-closing command, not the earlier roadside warning-light phase.
- Mapping is specific to four known objects in the base map. Map replacements or game updates can break it.
- This remains a prototype. Multiplayer and every combination of train movement, streaming and other mods have not been tested in game.

The Bryson indicator, flashing and clearing were tested in game during development. Further visual, control and remote-state refinements have automated tests, but that does not replace in-game validation on your installation.

## Configuration and troubleshooting

`settings.json` is read on world load. It controls label visibility, rotation and placement offsets. `crossingHierarchy` overrides the Bryson object path only. The old `maxCrossingDistanceMetres` field is retained but no longer used.

If a switch shows N/A or an indicator seems wrong, press F8. The game log is:

```text
%USERPROFILE%\AppData\LocalLow\Giraffe Lab LLC\Railroader\Player.log
```

Look for `joshi/bryson-crossing-lamp`, the crossing name, `AUTO/CLOSE circuit override installed`, or `operator selected CLOSE/AUTO`. When reporting a bug, include the mod/game versions, crossing, control mode, whether you stayed in Bryson, and relevant log excerpts. Review logs for personal details before posting them.

## Development and automatic releases

The production DLL references proprietary Railroader/Unity libraries from your local installation. **Those libraries are not uploaded or downloaded by CI.** GitHub runs the game-independent tests, verifies a prepared DLL against its recorded source/file hashes, packages it and publishes a release.

On Windows, install the **.NET 8 SDK**, PowerShell 7 and Git. The tests target .NET Framework 4.7.2; .NET Framework 4.8 can run them. Restore obtains the reference assemblies if needed.

```powershell
git clone https://github.com/Joshi1337/CTCCrossing.git
cd CTCCrossing
dotnet run --project Source/Tests/Tests.csproj --configuration Release
```

### Publish your next update

Edit the source and changelog on `main`, then run:

```powershell
pwsh ./scripts/Publish-Update.ps1 -Message "Describe your update"
```

This increments the patch version, tests, builds against local Railroader, prepares the source/binary fingerprint, commits and pushes. It stages all non-ignored changes in this dedicated repository, so check your changes first. GitHub then automatically creates **`v<version>`** with an installable ZIP and SHA-256 file after its checks pass.

For a specific version or non-default game installation:

```powershell
pwsh ./scripts/Publish-Update.ps1 -Version 0.4.0 -Message "Add a feature" -GameManaged "D:\SteamLibrary\steamapps\common\Railroader\Railroader_Data\Managed"
```

To prepare without committing/pushing:

```powershell
pwsh ./scripts/Prepare-Release.ps1 -Version 0.3.2
git add --all
git commit -m "Prepare 0.3.2"
git push origin main
```

Source-only pushes without a rebuilt matching DLL **fail verification rather than releasing stale code**. Documentation-only pushes do not create duplicate releases. An already-published version is never overwritten; prepare a new version for changed code. Pull requests run checks but never publish. The workflow can also be rerun from Actions.

The source fingerprint detects accidental stale builds; it is not a reproducible-build attestation. Tests cover indication, remote travel/reversal/pause, crossing mapping and a real Harmony patch on a stand-in circuit method. They do not execute the full game.

## License

CTCCrossing source: [MIT](LICENSE). Harmony: [its included MIT license](lib/Harmony-LICENSE.txt). Railroader and Unity belong to their respective owners; their libraries and assets are not included. This is an unofficial mod, not affiliated with the game's developers.

