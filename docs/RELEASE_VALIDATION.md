# Release validation

Version 0.5.2 was validated on Dyson Sphere Program `0.10.34.28529`, Unity `2022.3.62f3c1`, Direct3D 12, RTX 5070 Ti, and 2560×1440 output.

Observed runtime evidence:

- DLSS device capability and feature creation succeeded.
- All five modes used NVIDIA-recommended input dimensions.
- Projection-stage jitter, managed `DLSSCommandExecutionData`, and NVIDIA native DebugView readback matched.
- X and Y both covered positive and negative sub-pixel phases.
- DLAA, Quality, Balanced, Performance, and Ultra Performance all reached `DLSS jitter audit: PASS`.
- The session exceeded ten thousand DLSS Execute frames across mode changes.
- No native jitter mismatch or DLSS runtime exception was recorded in the additional stability window.

This evidence does not prove complete object motion-vector coverage, final image-quality acceptance in every scene, or a reduction in pre-DLSS 3D rendering cost.
