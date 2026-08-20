# DSP DLSS 0.6.2 release validation

- Managed source: Release build completed with 0 warnings and 0 errors.
- Native source: clean CMake/Ninja Release build completed successfully with MSVC.
- Package: expected file allowlist confirmed; every internal SHA-256 entry rechecked after archive creation.
- Third-party runtimes: Authenticode signatures checked before packaging; license and notice files included.
- Public source boundary: no game assemblies, generated game-code output, build outputs, logs, saves, or redistributable runtime DLLs are tracked.

This is static build and packaging evidence. It does not replace final in-game image-quality and stability testing.
