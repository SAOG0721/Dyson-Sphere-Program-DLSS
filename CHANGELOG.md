# Changelog

## 0.5.2 - 2026-08-15

- Initial public release for Dyson Sphere Program `0.10.34.28529`.
- Added an F8 mouse-operated control panel.
- Added Ultra Performance, Performance, Balanced, Quality, and DLAA modes using NVIDIA-recommended input sizes.
- Replaced legacy output-space TAA jitter with zero-centered, full-range DLSS input-pixel jitter.
- Added stage-correct projection auditing at `TaaComponent.SetProjectionMatrix`.
- Added managed submission and NVIDIA native DebugView jitter verification.
- Kept real Unity depth and motion-vector inputs with safe TAA fallback.
- Restored the official matching Unity `NVUnityPlugin.dll` deployment.
- Documented the full-resolution scene-preparation and motion-vector coverage limitations.
