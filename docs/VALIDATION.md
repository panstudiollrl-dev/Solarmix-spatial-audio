# Spatial Audio Validation Plan

Solarmix is designed to make spatial audio behavior audible. Instead of testing an algorithm only with static point sources, the project uses visible orbital motion and deliberately different sound-source materials.

## Goal

Evaluate how spatial audio datasets and rendering algorithms behave when source position, trajectory, timbre, and envelope are all varied in a controlled scene.

The central question is:

> When a listener can see a source orbiting around them, does the spatialized sound move continuously, believably, and distinctly enough to match that visual motion?

## Variables

### Spatial Backend

- Steam Audio baseline.
- HiFi-HARP-style FOA room impulse responses.
- MeshRIR-style meshed impulse response interpolation.
- Future SOFA/HRTF, BRIR, SRIR, and public RIR dataset adapters.

### Orbit Type

- Circular orbit for full 360-degree lateral motion.
- Elliptical orbit for distance and speed variation.
- Figure-eight orbit for repeated front-center and rear-center crossings.
- Flyby path for rapid lateral movement and Doppler-like cues.
- Slow drift for checking interpolation smoothness.
- Static source positions for A/B localization checks.

### Source Timbre

Each planet should act as a different probe signal:

- tonal sustained material;
- water-like broadband motion;
- bubble or cavity transients;
- resonant metallic or glassy partials;
- low-frequency bodies;
- noisy air or granular texture;
- short attacks with long decay;
- slow attack with stable sustain.

The purpose is not only musical variety. Different timbres reveal different artifacts: tonal sources expose pitch wobble, transients expose localization blur, broadband noise exposes comb filtering or coloration, and low-frequency bodies reveal distance/energy handling.

### Envelope

Envelope shapes should be intentionally different:

- short impulse;
- plucked decay;
- pulsed rhythm;
- continuous sustain;
- slow swell;
- irregular event stream.

## Listening Criteria

- **Continuity**: does the sound move smoothly, especially across front, rear, and center crossings?
- **Localization**: can the listener identify left, right, front, rear, near, and far changes?
- **Externalization**: does the source feel outside the head when heard on headphones?
- **Distance**: do near and far orbital paths produce a meaningful change without becoming inaudible?
- **Timbre preservation**: does the spatializer change the sound color too much?
- **Dynamic safety**: are there clips, clicks, bursts, or compressor pumping artifacts?
- **Parameter response**: do UI controls produce audible and explainable changes?
- **Mobile stability**: does the result survive iOS sample rate, buffer, CPU, and headphone constraints?

## Suggested Test Matrix

| Test | Orbit | Source | What It Reveals |
| --- | --- | --- | --- |
| Lateral sweep | Circle | sustained tone | left/right continuity and pan law |
| Center crossing | Figure-eight | broadband water | interpolation smoothness around front/rear |
| Transient localization | Static points | bubbles/clicks | localization precision and early reflection behavior |
| Distance scaling | Ellipse | low-frequency body | attenuation and room-energy mapping |
| Coloration check | Slow drift | resonant partials | comb filtering, phase artifacts, and pitch instability |
| Stress test | Fast flyby | mixed planets | CPU, limiter, and discontinuity resistance |

## Dataset Adapter Requirements

Every dataset adapter should document:

- source URL;
- license and redistribution terms;
- expected file layout;
- sample rate and channel format;
- coordinate convention;
- interpolation strategy;
- fallback behavior when files are missing;
- a minimal listening test scene.

## Notes

Subjective listening is part of the project, but the scene should still be repeatable. When reporting issues, include branch, backend, orbit type, planet/source model, parameter values, device, and headphone/speaker setup.

