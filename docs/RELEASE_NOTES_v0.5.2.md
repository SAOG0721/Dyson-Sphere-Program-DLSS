# Dyson Sphere Program DLSS v0.5.2

Initial public release of the DLSS Super Resolution / DLAA mod for Dyson Sphere Program.

## Highlights

- F8 opens a mouse-operated control panel.
- Ultra Performance, Performance, Balanced, Quality, and DLAA modes.
- Real Unity depth and motion-vector inputs.
- Corrected zero-centered, input-pixel DLSS jitter across every quality mode.
- Runtime audit of camera projection, managed submission, and NVIDIA native jitter readback.
- Safe fallback to the game's anti-aliasing when required inputs are unavailable.

## Tested configuration

- Dyson Sphere Program `0.10.34.28529` (Steam build `23109513`)
- Unity `2022.3.62f3c1`
- Direct3D 12
- BepInEx 5.4.17 x64
- NVIDIA GeForce RTX 5070 Ti, 2560×1440

## Installation

Install BepInEx 5 x64, back up any existing `NVUnityPlugin.dll` and `nvngx_dlss.dll`, then extract `DSP-DLSS-0.5.2-BepInEx5.zip` beside `DSPGAME.exe`. Add `-force-d3d12` to the Steam launch options and launch through Steam. Enter a save and press F8.

Read `README-DSP-DLSS.md` inside the archive before installation. The archive includes per-file hashes, the NVIDIA runtime license, and the Unity native-runtime notice.

## Important limitations

The low-resolution DLSS inputs are currently prepared after full-resolution scene rendering, so this release does not claim a proportional reduction in preceding 3D rendering cost. Procedural or indirect GPU instances may also lack complete object motion-vector coverage.

ZIP SHA-256: `9F0216E1443A7F817C76D8B26370150BA3DE261391434D4920F2AFBC8BFD195D`
