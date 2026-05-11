# HiFi-HARP Runtime RIRs

Put extracted HiFi-HARP FOA `.wav` files in this folder.

The runtime loader expects the dataset format described by `whojavumusic/hifi_harp`:

- 4 channels, First-Order Ambisonics
- 16 kHz sample rate
- `.wav` files from `FOA_train.zip` or `FOA_valid.zip`

If no file is present, `HiFiHarpSpatializer` uses a small built-in FOA room fallback so the branch still runs immediately.
