# Roadmap

This roadmap is intentionally practical: Solarmix is both an artwork and a spatial-audio validation environment, so the project needs clear listening goals as much as code goals.

## Near Term

- Stabilize the `meshRIR` branch as the active mobile prototype.
- Add real MeshRIR dataset loading notes and sample verification steps.
- Define a repeatable validation scene matrix for orbit type, timbre, and envelope.
- Improve per-planet sound-model documentation.
- Add iOS UI screenshots and short demo recordings.
- Add a tagged `v0.1.0-alpha` release.

## Spatial Audio

- Compare Steam Audio, HiFi-HARP-style FOA, and MeshRIR-style interpolation using the same orbit scene.
- Add spatial continuity tests for front, side, rear, and center-crossing motion.
- Add open dataset adapters with documented coordinate conventions and interpolation assumptions.
- Add listener orientation reset and visual direction feedback.
- Reduce audible discontinuities from orbit wraparound and interpolation thresholds.

## Sound Design

- Keep planet voices distinct while preserving a calm listening profile.
- Expand physical-model synthesis inspired by water, bubbles, resonant objects, wind, granular textures, and low-frequency bodies.
- Make every exposed tune parameter audible and meaningful for its planet model.
- Add limiter/compressor settings that prevent clipping on iOS.
- Treat each planet as a probe source with a distinct timbre and envelope profile.

## Documentation

- Document build steps for Unity Editor and iOS.
- Track third-party dataset requirements and licenses.
- Add architecture notes for the audio graph and spatializer interface.
- Add contribution guidance for listening tests and sound patches.
- Document spatial validation protocols in `docs/VALIDATION.md`.

## Open Questions

- Which open spatial audio datasets should become first-class adapters?
- Which MeshRIR dataset subset should be used as the default documented fixture?
- Which orbit paths best reveal interpolation discontinuities?
- Should generated demo assets live in Git LFS, GitHub Releases, or external storage?
- Should WebGL return as a separate browser demo, or stay as a legacy prototype?
