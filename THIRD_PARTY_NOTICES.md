# Third-Party Notices

Solarmix is a Unity project that experiments with several spatial audio backends and audio datasets. This file tracks known third-party components so contributors can keep licensing and attribution clear.

## Unity

The project is built with Unity. Unity Editor, Unity packages, generated project files, and runtime components are governed by Unity's own terms.

## Steam Audio

The baseline branch includes Steam Audio integration files and plugin binaries. Steam Audio is a third-party SDK and remains governed by its own license and distribution terms.

Project branches:

- `main`
- `steam-audio`

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

## Local User Assets

Do not commit personal exports, device builds, Xcode derived data, Unity `Library/`, large downloaded datasets, or private audio samples unless their source and license are documented.

