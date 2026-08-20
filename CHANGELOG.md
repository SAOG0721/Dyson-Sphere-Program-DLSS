# Changelog

## 0.6.2 - 2026-08-21

- Standardized the release on Direct3D 11; Steam launch options should remain empty.
- Added an optimized native isotropic 3×3 Gaussian USM pass on the raw DLSS output.
- Added live sharpening control from 0 to 1 in 0.05 steps, with 0 as an exact bypass and 0.50 as the default/reset value.
- Changed controls to `F8` for DLSS enable/disable and `F9` for the control panel.
- Kept the safe disabled-at-start default and game anti-aliasing fallback.
- Removed NIS/NVSharpen, RCAS, Frame Generation, Streamline, D3D12, and proxy-DLL paths from the supported release scope.

## 0.5.2 - 2026-08-15

- Initial public release with DLSS SR/DLAA modes, a control panel, corrected DLSS input-pixel jitter, depth and motion-vector inputs, and safe anti-aliasing fallback.
