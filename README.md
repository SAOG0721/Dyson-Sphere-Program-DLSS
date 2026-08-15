# Dyson Sphere Program DLSS

An experimental DLSS Super Resolution / DLAA mod for **Dyson Sphere Program**.

Tested with:

- Dyson Sphere Program `0.10.34.28529` (Steam build `23109513`)
- Unity `2022.3.62f3c1`, Mono, Built-in Render Pipeline
- Direct3D 12
- NVIDIA GeForce RTX 5070 Ti at 2560×1440
- BepInEx 5.4.17

> This is an independent community mod. It is not affiliated with or endorsed by Youthcat Studio, Gamera Games, Unity, or NVIDIA.

## Features

- DLSS Ultra Performance, Performance, Balanced, Quality, and DLAA modes.
- F8 mouse-operated control panel.
- Keeps the game's post-processing order and native-resolution UI composition.
- Uses the game's real depth and motion-vector textures.
- Converts the legacy TAA Halton sequence to zero-centered DLSS input-pixel jitter.
- Audits the applied projection, managed DLSS submission, and NVIDIA native readback.
- Safe fallback to the game's anti-aliasing when required inputs are unavailable.

This release intentionally contains **no DLSS Frame Generation, Streamline, Reflex, `dxgi.dll`, or `d3d12.dll` integration**.

## Download and installation

Download `DSP-DLSS-0.5.2-BepInEx5.zip` from the [v0.5.2 release](https://github.com/SAOG0721/Dyson-Sphere-Program-DLSS/releases/tag/v0.5.2).

See [the full installation and recovery guide](docs/INSTALLATION.md). In short:

1. Install BepInEx 5 x64 into the game directory and run the game once.
2. Back up any existing `NVUnityPlugin.dll` and `nvngx_dlss.dll`.
3. Extract the release ZIP directly beside `DSPGAME.exe`.
4. Add `-force-d3d12` to the Steam launch options.
5. Launch through Steam, enter a save, and press F8.

## Important limitation

The current low-resolution DLSS inputs are prepared after the scene has already rendered at full resolution. The quality modes exercise real low-input/high-output DLSS reconstruction, but they do **not** yet reduce the preceding 3D rendering cost proportionally.

The game also uses many indirect/procedural GPU instances. Some conveyors, cargo, logistics units, enemies, rockets, Dyson structures, particles, or transparent objects may lack complete object motion vectors and can still show temporal artifacts.

## Jitter investigation

The initial integration could pass a parameter-chain audit while still producing scale-periodic grid artifacts. The game's legacy TAA jitter was positive-only, scaled for ordinary TAA, and normalized in output-pixel space. Version 0.5.2 recovers the Halton phase, skips the zero phase, centers it around zero, restores full one-input-pixel coverage, and writes the same value to both the actual camera projection and DLSS.

The full technical retrospective is in [Implementation lessons](docs/IMPLEMENTATION_LESSONS.md).

## Build

Requirements:

- .NET SDK capable of targeting `netstandard2.1`
- A matching Dyson Sphere Program installation
- BepInEx 5 installed in that game directory

Set `DSP_GAME_DIR` or pass `DSPGameDir` explicitly:

```powershell
$env:DSP_GAME_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program'
dotnet build .\src\DSPDLSSZeroMV\DSPDLSSZeroMV.csproj -c Release
```

The output file remains named `DSPDLSSZeroMV.dll` for compatibility with the tested deployment; the name is historical and does not mean the current mod uses zero motion vectors.

## Evidence standard

`DLSS jitter audit: PASS` proves that the actual projection, managed submission, and native NVIDIA readback agree. It does not replace fixed-scene screenshot/video A/B testing, motion-vector coverage checks, final-output verification, or GPU performance measurement.

## Third-party components

The release package contains NVIDIA DLSS and the matching Unity NVIDIA native runtime required by this specific Unity player. These components remain the property of their respective owners and are distributed subject to their own terms. See [Third-party notices](THIRD_PARTY_NOTICES.md) and the license files included in the release archive.

No open-source license has been selected for the mod source at this time.
