# Spatial Audio Dataset Sources

Solarmix uses open spatial audio datasets as validation material. The goal is not simply to make the app sound good; it is to compare how different spatialization algorithms behave when source motion, timbre, envelope, and acoustic data are varied in a controlled orbit scene.

This document separates data **types**, **formats**, and **specific datasets**.

## Data Types

| Type | Meaning | Solarmix Use |
| --- | --- | --- |
| HRTF / HRIR | Head-related transfer function / impulse response for binaural rendering. | Test direction, front/back stability, elevation, and headphone localization. |
| BRIR | Binaural room impulse response. | Test spatialization with room coloration, distance, and externalization. |
| RIR | Room impulse response. | Test reverberation, distance, and room-energy mapping. |
| SRIR | Spatial room impulse response. | Test multichannel or spatial room capture/rendering. |
| FOA / HOA RIR | First-order or higher-order ambisonic room impulse response. | Test ambisonic decoding and spatial room rendering. |
| SOFA | Spatially Oriented Format for Acoustics. This is a format/standard, not one dataset. | Candidate interchange format for HRTF, BRIR, SRIR, and related acoustic data. |

## Current / Planned Sources

| Source | Data Type | Link | Status In Solarmix | Notes |
| --- | --- | --- | --- | --- |
| MeshRIR | RIR on finely meshed grids | https://github.com/sh01k/MeshRIR and https://zenodo.org/records/10852693 | Active target on `meshRIR` | Used to evaluate interpolation and sound-field behavior. The dataset description says it is intended for sound field analysis and synthesis evaluation. |
| HiFi-HARP | 7th-order Ambisonic Room Impulse Responses; extracted FOA runtime subset in Solarmix experiments | https://huggingface.co/datasets/whojavumusic/hifi_harp | Branch target on `hifi-harp` | Used for ambisonic room response experiments. Redistribution terms should be checked before committing extracted audio. |
| SOFA | Format for HRTF, BRIR, SRIR, directivity, and spatial acoustic data | https://www.sofaconventions.org/ | Planned adapter family | Useful as a common interchange format for multiple public HRTF/BRIR datasets. |
| CIPIC HRTF Database | HRTF / HRIR | https://github.com/amini-allight/cipic-hrtf-database | Candidate HRTF adapter | Classic public HRTF database. Confirm license and preferred citation before bundling derived data. |
| SADIE II Database | HRTF / HRIR / BRIR in WAV and SOFA formats | https://www.york.ac.uk/sadie-project/database.html and https://zenodo.org/records/10886409 | Candidate HRTF/BRIR adapter | Useful for binaural rendering and comparison with tools such as Binamix-style interpolation workflows. |
| SONICOM HRTF Dataset | HRTF plus subject-related measurement data | https://www.sonicom.eu/tools-and-resources/hrtf-dataset/ | Candidate personalized-HRTF adapter | Useful for testing differences between subjects and personalization workflows. Review access and redistribution terms. |
| Public RIR/SRIR collections | RIR / SRIR | Examples include Zenodo-hosted RIR/SRIR datasets and curated public RIR lists | Candidate room-acoustics adapters | Each dataset needs a separate license and coordinate-convention review. |

## Adapter Requirements

Every dataset adapter should include:

- source URL;
- dataset citation;
- license and redistribution summary;
- local file layout;
- channel format;
- sample rate;
- coordinate convention;
- distance units;
- interpolation method;
- fallback behavior;
- validation scene or listening checklist.

## Redistribution Rule

Do not commit downloaded datasets, extracted archives, or derived impulse response bundles until the license and redistribution terms are clear. Prefer:

- documented download steps;
- small fixtures where permitted;
- GitHub Releases for explicit sample packs;
- external storage for large datasets;
- Git LFS only when the project intentionally commits redistributable assets.

