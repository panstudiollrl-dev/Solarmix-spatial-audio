# Solarmix Spatial Audio

Solarmix is an open-source spatial audio validation instrument built in Unity. It uses an orbital solar-system interface to test how different spatial audio algorithms respond to controlled source motion, timbre, envelope shape, distance, and room impulse response data.

The project began as a Steam Audio baseline and is evolving into a comparative research instrument for multiple open spatial audio datasets and rendering backends, including HiFi-HARP and MeshRIR-inspired interpolation.

## Project Status

Solarmix is an active prototype. The repository is public so the audio engine, spatialization experiments, validation method, and mobile interaction work can be shared, reviewed, and extended.

Current branches:

| Branch | Purpose |
| --- | --- |
| `meshRIR` | Active/default Unity/iOS branch for MeshRIR-inspired validation, smoothed interpolation, and procedural physical-model sound probes |
| `main` | Historical Steam Audio baseline and repository documentation root |
| `steam-audio` | Explicit checkpoint for the original Steam Audio implementation |
| `hifi-harp` | Runtime spatialization experiments using HiFi-HARP-style FOA room impulse responses |
| `mesh-rir` | Earlier MeshRIR implementation checkpoint |

## Why This Exists

Most spatial audio tools are either engineering utilities or fixed media players. Solarmix is meant to sit somewhere more playful and inspectable: a repeatable listening scene for testing spatialization behavior with musically meaningful motion and sound material.

- a testbed for comparing spatial audio backends in a consistent orbit scene;
- a controlled way to hear how timbre and envelope affect spatial perception;
- a real-time instrument for designing circular, elliptical, flyby, and figure-eight motion tests;
- a mobile-first listening prototype for headphones and iOS builds;
- an open codebase where artists and audio developers can study the relationship between visual motion, listener orientation, and perceived sound position.

## Development Model

Solarmix is led by PAN STUDIO LLRL's artistic direction, listening goals, spatial-audio questions, interface ideas, and sound-design judgment. Much of the implementation has been developed through iterative collaboration with AI coding agents, including Codex and other agentic coding tools.

This is part of the project, not a hidden detail: Solarmix is also a case study in how an artist/developer can guide AI agents toward a specialized open-source audio tool through listening feedback, branch experiments, refactoring, dataset integration, and mobile build validation.

The project welcomes contributions that preserve this listening-led workflow: code changes should be evaluated not only by whether they compile, but by whether they improve spatial perception, continuity, sound quality, and controllability.

## Core Ideas

- **Planetary validation scene**: each planet is a sound source with orbit, motion, and tuneable synthesis parameters.
- **Listener at the sun**: the center of the solar system acts as the listening position, making orbital motion map directly to spatial motion.
- **Timbre and envelope probes**: planet voices are generated in code so transient, sustained, noisy, tonal, percussive, and resonant materials can be tested separately.
- **Spatial backend comparison**: Steam Audio, HiFi-HARP-style FOA, and MeshRIR-style interpolation live in separate branches.
- **Mobile deployment**: the current work targets Unity Editor and iOS builds.

## Validation Method

Solarmix treats spatialization as something to be listened to under controlled variation:

1. Choose a spatial audio backend or dataset.
2. Choose an orbit type: circle, ellipse, flyby, figure-eight, slow drift, or center crossing.
3. Assign each planet a distinct source model with known timbre and envelope behavior.
4. Listen for localization, front/back stability, distance impression, interpolation smoothness, clipping, timbral coloration, and discontinuities.
5. Compare the result across branches or datasets using the same visual motion.

See [docs/VALIDATION.md](docs/VALIDATION.md) for the working validation plan.

## Runtime Data

Large datasets are intentionally not committed directly unless they are small extracted runtime examples. Solarmix should always document where a spatial audio dataset comes from, what type of data it contains, and whether redistribution is allowed.

See [docs/DATASETS.md](docs/DATASETS.md) for the working dataset source list.

### HiFi-HARP

The `hifi-harp` branch can load extracted FOA `.wav` files from the `whojavumusic/hifi_harp` dataset.

Expected location:

```text
Assets/StreamingAssets/HiFiHARP/
```

The loader expects 4-channel First-Order Ambisonics `.wav` files. If none are present, the branch uses a small built-in room fallback.

### MeshRIR

The `meshRIR` branch can load MeshRIR-style source positions and impulse responses.

Expected location:

```text
Assets/StreamingAssets/MeshRIR/
```

Expected files include:

```text
pos_src.npy
ir_*.npy
```

If dataset files are missing, the branch uses a compact built-in mesh-style fallback so the project remains runnable.

### Other Candidate Dataset Families

Future dataset adapters may target open HRTF, BRIR, SRIR, and RIR resources such as SOFA-format HRTF collections, CIPIC, SADIE II, SONICOM, and other public room impulse response datasets. Each adapter should document licensing and redistribution rules before any derived assets are committed.

## Getting Started

1. Install Unity 6 or newer. The current local development version has used Unity `6000.4.x`.
2. Clone the repository.
3. Open the project folder in Unity.
4. Select the branch you want to test.
5. Open the main scene and press Play.

For iOS development, open the Unity-generated Xcode project after building from Unity. The active mobile work is currently on `meshRIR`.

## Repository Hygiene

Unity-generated folders such as `Library/`, `Temp/`, `Logs/`, local build folders, generated IDE projects, and app build products are ignored.

If you add large audio datasets, prefer external download instructions, Git LFS, or release assets rather than committing large raw archives directly.

## Roadmap

See [ROADMAP.md](ROADMAP.md).

Current priorities:

- stabilize MeshRIR interpolation and true dataset loading;
- document HRTF, BRIR, SRIR, RIR, HiFi-HARP, and MeshRIR sources clearly;
- define repeatable orbit/timbre/envelope validation scenes;
- improve mobile UI and orientation feedback;
- document sound-model parameters per planet;
- add repeatable spatial-audio validation scenes;
- publish demo media and tagged alpha releases.

## License

Original Solarmix code and documentation are available under the MIT license. Third-party SDKs, datasets, models, plugins, and audio assets remain under their respective licenses. See [LICENSE.md](LICENSE.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
