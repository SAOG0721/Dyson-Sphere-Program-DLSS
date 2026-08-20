# DSP DLSS 0.6.2 release notes

This release provides the Direct3D 11 DLSS SR/DLAA path and optional native Gaussian USM sharpening. It uses `F8` for enable/disable and `F9` for the control panel, starts disabled, and retains the game's anti-aliasing fallback.

The lower-resolution inputs are currently prepared after full-resolution scene rendering, so DLSS mode changes do not proportionally reduce preceding 3D cost. Motion-vector coverage is incomplete for some procedural and indirect instances.

The source builds completed with zero warnings and zero errors. The release archive and its internal file manifest were independently verified. Final in-game visual and long-session validation remains recommended.
