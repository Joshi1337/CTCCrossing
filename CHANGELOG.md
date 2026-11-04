# Changelog

## 0.4.0 — 2026-11-03

- Move AUTO/CLOSE switches closer to their crossing indicators.
- Enable AUTO/CLOSE for multiplayer clients using the game's authorized property-change transport.
- Synchronize all four switch positions and restore the current host state for late joiners.
- Apply manual closure consistently to each peer's crossing circuit; host world reload resets AUTO.
- Keep unrelated properties host-only and respect the game's local message authorization.
- Add multiplayer lifecycle, command routing, permission and mapping regression tests.

Host and panel users need the updated mod. Live two-player Steam validation is still pending.

## 0.3.1 — 2026-11-03

First public CTCCrossing release, combining the development prototypes:

- Four red crossing indicators on the original Bryson 3D CTC panel.
- Flashing while closing and steady red after closure.
- Independent AUTO/CLOSE switches for single player and the multiplayer host.
- Remote progress tracking when distant gate animations are disabled.
- F8 diagnostics, configurable placement and English Crossing labels.
- Automated tests, local release preparation and GitHub Actions publishing.

Known limits: remote closure is calculated when visuals are unavailable; manual selections reset on world load; client switch synchronization and full multiplayer validation are not implemented.

