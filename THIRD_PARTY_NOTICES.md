# Third-Party Notices

Solarmix is a Unity project that experiments with several spatial audio backends and audio datasets. This file tracks known third-party components so contributors can keep licensing and attribution clear.

## Unity

The project is built with Unity. Unity Editor, Unity packages, generated project files, and runtime components are governed by Unity's own terms.

## Steam Audio

The baseline branch includes Steam Audio integration files and plugin binaries. Steam Audio is a third-party SDK and remains governed by its own license and distribution terms.

Project branches:

- `main`
- `steam-audio`

## Dataset And Format Categories

Solarmix distinguishes between:

- **HRTF/HRIR**: head-related transfer functions or impulse responses for binaural rendering.
- **BRIR**: binaural room impulse responses, including room and listener/head response.
- **SRIR/RIR**: spatial or room impulse responses, often used for room acoustics and sound-field work.
- **HOA/FOA RIR**: higher-order or first-order ambisonic room impulse responses.
- **SOFA**: a file format/standard for storing spatially oriented acoustic data; it is not a dataset by itself.

Known dataset sources and planned adapters are tracked in [docs/DATASETS.md](docs/DATASETS.md).

## HiFi-HARP

The `hifi-harp` branch is designed to load extracted First-Order Ambisonics room impulse response `.wav` files from the `whojavumusic/hifi_harp` dataset.

Dataset page:

```text
https://huggingface.co/datasets/whojavumusic/hifi_harp
```

Contributors should review the dataset license and usage terms before redistributing any extracted audio files.

## MeshRIR

The `meshRIR` branch is designed to load MeshRIR-style room impulse response files such as `pos_src.npy` and `ir_*.npy`.

Reference implementation:

```text
https://github.com/sh01k/MeshRIR
```

Contributors should review the upstream repository and dataset licenses before redistributing MeshRIR data or derived extracts.

## HRTF / BRIR Candidates

Potential future HRTF and BRIR adapters include SOFA-format public datasets such as CIPIC, SADIE II, SONICOM, and other documented research datasets. These should not be bundled until their license, citation requirements, and redistribution terms are reviewed.

## Local User Assets

Do not commit personal exports, device builds, Xcode derived data, Unity `Library/`, large downloaded datasets, or private audio samples unless their source and license are documented.
