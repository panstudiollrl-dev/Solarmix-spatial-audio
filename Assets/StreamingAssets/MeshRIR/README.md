# MeshRIR Runtime RIRs

Put extracted MeshRIR session folders or exported RIR wav files here.

Supported at runtime:

- MeshRIR `ir_*.npy` files from the Zenodo dataset
- mono/stereo `.wav` RIR files exported from the MeshRIR Python/Matlab tools

The loader uses one RIR row as the source room kernel and combines it with live per-planet direction, interaural delay, and stereo spreading. If no dataset file is present, `MeshRIRSpatializer` uses a compact built-in mesh-style fallback so the branch can run immediately.
