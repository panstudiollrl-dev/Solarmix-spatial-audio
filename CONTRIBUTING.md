# Contributing

Solarmix is an experimental audio project, so useful contributions include code, listening notes, reproducible bugs, dataset integration improvements, sound-design patches, mobile UI fixes, and documentation.

## Good First Contributions

- Improve README setup notes for your Unity version or platform.
- Add a small reproducible issue for a spatialization bug.
- Document how a specific planet voice is synthesized.
- Improve fallback behavior when external impulse response data is missing.
- Add screenshots, recordings, or test notes for iOS builds.

## Branches

Use the branch that matches the spatial backend you are working on:

- `main` / `steam-audio` for the original Steam Audio baseline.
- `hifi-harp` for HiFi-HARP-style FOA RIR work.
- `meshRIR` for current MeshRIR-inspired work.

## Development Notes

- Keep Unity-generated folders out of git.
- Keep large datasets out of git unless they are intentionally small fixtures.
- Document dataset source, license, and extraction steps.
- Prefer focused pull requests with one clear audio, UI, or build concern.
- Include listening notes when a change affects sound quality.

## Issue Reports

Helpful issue reports include:

- branch name;
- Unity version;
- platform and device;
- headphones/speaker setup;
- expected behavior;
- actual behavior;
- screenshots or short recordings when possible.

