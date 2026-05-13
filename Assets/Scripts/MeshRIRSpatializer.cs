using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// MeshRIR v2 – proper multi-azimuth spatial interpolation.
///
/// Key improvements over v1:
///  • 12-position azimuth kernel table; runtime linear-interpolation between
///    the two nearest positions → smooth spatial sound across full 360°
///  • Per-sample parameter smoothing (same technique as HiFiHarpSpatializer)
///    eliminates the choppy discontinuities at centre/intermediate angles
///  • Binaural ITD + ILD + head-shadow model, all interpolated continuously
///  • Elevation-aware energy tilt
///  • Built-in soft compressor + hard limiter → no clipping / burst pops
///
/// Tune parameters (Rate / Depth / Energy / Material / Density) map to
/// acoustically meaningful room properties and are exposed to SolarSystemUI.
/// </summary>
[DisallowMultipleComponent]
public class MeshRIRSpatializer : MonoBehaviour, IPlanetSpatializer
{
    // ── Tune parameters (exposed to UI) ────────────────────────────────────
    [Range(0f, 1f)] public float rate     = 0.50f; // tracking speed & room animation
    [Range(0f, 1f)] public float depth    = 0.55f; // reverb tail length
    [Range(0f, 1f)] public float energy   = 0.60f; // wet / spatial energy level
    [Range(0f, 1f)] public float material = 0.35f; // surface absorption (0=reflective, 1=absorptive)
    [Range(0f, 1f)] public float density  = 0.50f; // reflection density in diffuse tail

    // ── Internal constants ──────────────────────────────────────────────────
    const int   NumAzimuths  = 12;      // azimuth slices in kernel table (every 30°)
    const int   MaxTaps      = 56;      // max taps per azimuth entry
    const int   RingSize     = 8192;    // delay line length (must be power of 2)
    const float MaxITD_s     = 0.00075f;// max interaural time delay (seconds) ~0.75 ms
    const float CompThresh   = 0.40f;   // compressor threshold
    const float CompRatio    = 4.0f;    // compressor ratio above threshold
    const float LimiterCeil  = 0.90f;   // hard limiter ceiling

    // ── One room reflection tap ─────────────────────────────────────────────
    struct Tap { public int delay; public float left; public float right; }

    // ── Per-azimuth kernel tables (built at Awake) ──────────────────────────
    Tap[][] aziTable;  // aziTable[azIndex][tapIndex]

    // ── Delay ring ──────────────────────────────────────────────────────────
    readonly float[] ring = new float[RingSize];
    int ringIdx;

    // ── Per-sample smooth tracking state ───────────────────────────────────
    // (updated in Update(), consumed + smoothed every audio sample)
    float targetAzi;        // normalised azimuth 0..1  (0 = front, 0.25 = right, …)
    float targetElev;       // elevation  -1..1
    float targetDistGain;   // distance attenuation gain

    float cachedAzi      = 0f;
    float cachedElev     = 0f;
    float cachedDistGain = 1.2f;

    // ── Compressor state ────────────────────────────────────────────────────
    float compEnvL = 0f, compEnvR = 0f;

    // ── NpyKernelSet: optional per-azimuth binaural taps from .npy data ────
    struct NpyBinaural { public float[] left; public float[] right; }

    // ── References ──────────────────────────────────────────────────────────
    Transform listener;

    // ═══════════════════════════════════════════════════════════════════════
    //  Unity lifecycle
    // ═══════════════════════════════════════════════════════════════════════

    void Awake()
    {
        listener = FindAnyObjectByType<AudioListener>()?.transform;
        BuildKernelTable();
    }

    void Update()
    {
        UpdateDirection();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  IPlanetSpatializer
    // ═══════════════════════════════════════════════════════════════════════

    public void ProcessSample(float mono, out float left, out float right)
    {
        // Write input into the ring
        ring[ringIdx] = mono;

        // ── Per-sample smooth parameter tracking ───────────────────────────
        // rate [0..1] blends tracking speed 0.0018..0.0055 per sample
        // Higher rate = faster spatial updates = more responsive but slightly
        // harsher on fast movement. Lower rate = smoother panning.
        float k = 0.0018f + rate * 0.0037f;
        cachedAzi      += (targetAzi      - cachedAzi)      * k;
        cachedElev     += (targetElev     - cachedElev)     * k;
        cachedDistGain += (targetDistGain - cachedDistGain) * 0.0010f;

        // ── Azimuth interpolation: find two neighbouring table entries ─────
        float aziFrac = cachedAzi * NumAzimuths;      // 0 .. NumAzimuths
        int   aziLo   = (int)aziFrac % NumAzimuths;
        int   aziHi   = (aziLo + 1) % NumAzimuths;
        float aziT    = aziFrac - (int)aziFrac;       // fractional blend weight

        // ── Binaural direct path (ITD + ILD + head shadow) ─────────────────
        float aziAngle = cachedAzi * 2f * Mathf.PI;
        float sinAzi   = Mathf.Sin(aziAngle);         // lateral component
        float cosAzi   = Mathf.Cos(aziAngle);         // front/back component

        // ITD: interaural time delay — interpolated smoothly via cachedAzi
        int itdSamples = Mathf.RoundToInt(
            Mathf.Abs(sinAzi) * MaxITD_s * AudioSettings.outputSampleRate);

        // Ipsilateral ear gets the "early" sample, contralateral gets ITD delay
        float directL = sinAzi >= 0f ? mono : ReadRing(itdSamples);
        float directR = sinAzi <= 0f ? mono : ReadRing(itdSamples);

        // ILD: constant-power pan + head-shadow attenuation on far ear
        float halfPi   = Mathf.PI * 0.5f;
        float panAngle = Mathf.Clamp((sinAzi + 1f) * 0.5f, 0f, 1f) * halfPi;
        float dlGain   = Mathf.Cos(panAngle);   // left channel gain
        float drGain   = Mathf.Sin(panAngle);   // right channel gain

        // Head-shadow: far side loses high-frequency energy (simulated as gain dip)
        float shadow   = Mathf.Clamp01(Mathf.Abs(sinAzi));
        float frontBoost = 1f + cosAzi * 0.12f;           // front sounds brighter
        if (sinAzi > 0f)  dlGain *= Mathf.Lerp(1f, 0.52f, shadow);   // L is far
        else              drGain *= Mathf.Lerp(1f, 0.52f, shadow);    // R is far

        // Elevation: mild gain tilt based on cached elevation
        float elevScale = 1f + cachedElev * 0.10f;

        float directGain = (1f - energy * 0.28f) * cachedDistGain * frontBoost;
        float l = directL * directGain * dlGain * elevScale;
        float r = directR * directGain * drGain * elevScale;

        // ── Wet: interpolate between aziLo and aziHi tap arrays ───────────
        // Linear blend of gain values at each tap — this is the core fix for
        // the choppy middle: gains transition continuously, not in discrete steps
        Tap[] tapsLo = aziTable[aziLo];
        Tap[] tapsHi = aziTable[aziHi];
        int   nTaps  = Mathf.Min(tapsLo.Length, tapsHi.Length);

        float wetGain = energy * (0.55f + depth * 0.55f) * cachedDistGain;

        for (int i = 0; i < nTaps; i++)
        {
            // Blend delay time (small ITD shift in early reflections)
            int tapDelay = Mathf.RoundToInt(
                tapsLo[i].delay + (tapsHi[i].delay - tapsLo[i].delay) * aziT);
            tapDelay = Mathf.Max(1, tapDelay);

            // Blend L/R tap gains
            float tapL = tapsLo[i].left  + (tapsHi[i].left  - tapsLo[i].left)  * aziT;
            float tapR = tapsLo[i].right + (tapsHi[i].right - tapsLo[i].right) * aziT;

            float s = ReadRing(tapDelay);
            l += s * tapL * wetGain * elevScale;
            r += s * tapR * wetGain * elevScale;
        }

        // Advance ring pointer
        ringIdx = (ringIdx + 1) & (RingSize - 1);

        // ── Compressor + limiter ───────────────────────────────────────────
        l = ApplyDynamics(l, ref compEnvL);
        r = ApplyDynamics(r, ref compEnvR);

        left  = l;
        right = r;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Direction tracking (called from Update on main thread)
    // ═══════════════════════════════════════════════════════════════════════

    void UpdateDirection()
    {
        if (listener == null)
            listener = FindAnyObjectByType<AudioListener>()?.transform;
        if (listener == null) return;

        Vector3 world = transform.position - listener.position;
        float dist = world.magnitude;
        if (dist < 0.001f)
        {
            targetAzi = 0f; targetElev = 0f;
            return;
        }

        Vector3 local = listener.InverseTransformDirection(world / dist);

        // Azimuth: atan2(x, z) → 0..1 normalised (0=front CCW)
        float azi = Mathf.Atan2(local.x, local.z) / (2f * Mathf.PI);
        if (azi < 0f) azi += 1f;
        targetAzi  = azi;
        targetElev = Mathf.Clamp(local.y, -1f, 1f);

        // Distance gain: mild roll-off over long planetary distances
        float dist01      = Mathf.Clamp01((dist - 30f) / 650f);
        targetDistGain    = Mathf.Lerp(1.45f, 1.05f, dist01);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Dynamics processing: compressor + hard limiter
    // ═══════════════════════════════════════════════════════════════════════

    float ApplyDynamics(float x, ref float envState)
    {
        float absX = Mathf.Abs(x);

        // Peak envelope follower (attack fast, release slow)
        float envSpeed = absX > envState ? 0.30f : 0.0008f;
        envState += (absX - envState) * envSpeed;

        // Compressor: gain reduction when envelope exceeds threshold
        float gainReduction = 1f;
        if (envState > CompThresh)
        {
            float over = envState - CompThresh;
            float compressed = CompThresh + over / CompRatio;
            gainReduction = compressed / Mathf.Max(envState, 0.00001f);
        }
        x *= gainReduction;

        // Hard limiter with soft knee (avoids harsh clicks at ceiling)
        if      (x >  LimiterCeil) x =  LimiterCeil + (x - LimiterCeil)  * 0.08f;
        else if (x < -LimiterCeil) x = -LimiterCeil + (x + LimiterCeil)  * 0.08f;

        return x;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Ring buffer read
    // ═══════════════════════════════════════════════════════════════════════

    float ReadRing(int delay)
    {
        // Clamp delay to ring capacity
        if (delay >= RingSize) delay = RingSize - 1;
        int idx = ringIdx - delay;
        if (idx < 0) idx += RingSize;
        return ring[idx & (RingSize - 1)];
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Kernel table construction
    // ═══════════════════════════════════════════════════════════════════════

    void BuildKernelTable()
    {
        int sr = Mathf.Max(1, AudioSettings.outputSampleRate);

        // Try to load MeshRIR .npy dataset first (provides best quality)
        if (TryLoadNpyKernels(sr))
        {
            Debug.Log("[MeshRIR] Loaded kernel table from .npy dataset.");
            return;
        }

        // Try mono WAV fallback
        if (TryLoadWavKernel(sr))
        {
            Debug.Log("[MeshRIR] Loaded kernel from WAV. Using binaural synthesis model.");
            return;
        }

        // Fully synthetic fallback – still produces high-quality spatial audio
        BuildSyntheticKernels(sr);
        Debug.Log("[MeshRIR] Using built-in synthetic kernel table. " +
                  "Place ir_*.npy files under StreamingAssets/MeshRIR for dataset quality.");
    }

    // ── .npy path ──────────────────────────────────────────────────────────
    bool TryLoadNpyKernels(int sr)
    {
        string root = Path.Combine(Application.streamingAssetsPath, "MeshRIR");
        if (!Directory.Exists(root)) return false;

        var files = new List<string>(Directory.GetFiles(root, "ir_*.npy", SearchOption.AllDirectories));
        if (files.Count == 0) return false;
        files.Sort(StringComparer.OrdinalIgnoreCase);

        // Build an azimuth table from however many .npy files exist.
        // Each .npy is assumed to be a (N_pos × T) array of impulse responses.
        // We synthesise L/R from the mono IR using our binaural model.
        if (!SpatialAudioNpy.TryRead(files[0], out var array)) return false;

        int irLen  = array.shape[array.shape.Length - 1];
        int nRows  = array.values.Length / Mathf.Max(1, irLen);
        int tapLen = Mathf.Min(MaxTaps * 4, Mathf.Min(2048, irLen));

        aziTable = new Tap[NumAzimuths][];
        for (int az = 0; az < NumAzimuths; az++)
        {
            float aziAngle = az / (float)NumAzimuths * 2f * Mathf.PI;
            float sinAz    = Mathf.Sin(aziAngle);
            int   row      = (int)(((aziAngle / (2f * Mathf.PI)) * nRows) % nRows);
            int   offset   = row * irLen;

            var taps = new List<Tap>(MaxTaps);
            float peak = 0f;
            for (int i = 0; i < tapLen; i++)
            {
                float v = array.values[offset + i];
                if (Mathf.Abs(v) > peak) peak = Mathf.Abs(v);
            }
            float normGain = peak > 0.00001f ? 0.30f / peak : 1f;

            // Sample every few frames as sparse taps, applying binaural model
            int stride = Mathf.Max(1, tapLen / MaxTaps);
            for (int i = 0; i < tapLen && taps.Count < MaxTaps; i += stride)
            {
                float v = array.values[offset + i] * normGain;
                if (Mathf.Abs(v) < 0.001f) continue;
                float ild = Mathf.Abs(sinAz) * 0.42f;
                float tapL = sinAz >= 0f ? v : v * (1f - ild);
                float tapR = sinAz <= 0f ? v : v * (1f - ild);
                taps.Add(new Tap { delay = Mathf.Max(1, i), left = tapL, right = tapR });
            }
            aziTable[az] = taps.ToArray();
        }
        return true;
    }

    // ── WAV path ────────────────────────────────────────────────────────────
    bool TryLoadWavKernel(int sr)
    {
        string root = Path.Combine(Application.streamingAssetsPath, "MeshRIR");
        string wav  = SpatialAudioWav.FindFirstWav(root);
        if (string.IsNullOrEmpty(wav)) return false;
        if (!SpatialAudioWav.TryRead(wav, out var audio)) return false;

        // Down-mix to mono, resample, then apply binaural model per azimuth
        int   outputRate = Mathf.Max(1, AudioSettings.outputSampleRate);
        int   frames     = audio.samples.Length / audio.channels;
        int   tapLen     = Mathf.Min(2048, Mathf.CeilToInt(frames * outputRate / (float)audio.sampleRate));
        float step       = audio.sampleRate / (float)outputRate;
        var   monoKernel = new float[tapLen];
        for (int i = 0; i < tapLen; i++)
        {
            float src = i * step;
            int   i0  = Mathf.Clamp((int)src, 0, frames - 1);
            int   i1  = Mathf.Min(i0 + 1, frames - 1);
            float t   = src - i0;
            float a   = 0f, b = 0f;
            for (int c = 0; c < audio.channels; c++)
            {
                a += audio.samples[i0 * audio.channels + c];
                b += audio.samples[i1 * audio.channels + c];
            }
            monoKernel[i] = Mathf.Lerp(a, b, t) / audio.channels;
        }
        NormalizeArray(monoKernel, 0.32f);
        BuildBinauralTableFromMono(monoKernel, sr);
        return true;
    }

    // ── Fully synthetic kernel table ────────────────────────────────────────
    void BuildSyntheticKernels(int sr)
    {
        // Tail length in samples depends on depth [0..1]
        float tailMs  = 55f + depth * 110f;
        int   maxTail = Mathf.Min(RingSize - 1,
                            Mathf.RoundToInt(sr * tailMs / 1000f));
        int   nTaps   = Mathf.Max(6, Mathf.RoundToInt(MaxTaps * (0.35f + density * 0.65f)));

        // Absorption coefficient: high material → absorptive → fast decay
        float absorb = 0.06f + material * 0.80f;

        aziTable = new Tap[NumAzimuths][];

        for (int az = 0; az < NumAzimuths; az++)
        {
            float aziAngle = az / (float)NumAzimuths * 2f * Mathf.PI;
            float sinAz    = Mathf.Sin(aziAngle);  // +1=right, -1=left
            float cosAz    = Mathf.Cos(aziAngle);  // +1=front, -1=back
            float frontScale = 1f + cosAz * 0.18f; // front reflections are brighter

            var taps = new List<Tap>(nTaps);

            // ── Early reflections (room boundaries) ─────────────────────
            // Arrival times vary with azimuth (lateral surfaces closer on near side)
            float[] earlyMs = { 4f, 8f, 13f, 20f, 30f, 44f };
            for (int e = 0; e < earlyMs.Length && taps.Count < nTaps / 2; e++)
            {
                // Lateral shift: near-side wall → earlier arrival on ipsilateral ear
                float lateralShift = 1f + sinAz * 0.10f;
                int   delay = Mathf.RoundToInt(sr * earlyMs[e] * 0.001f * lateralShift);
                if (delay < 1 || delay >= maxTail) continue;

                float tSec  = delay / (float)sr;
                float decay = Mathf.Exp(-tSec * absorb * 22f);

                // ILD: ipsilateral ear gets more direct energy from lateral reflections
                float ild     = Mathf.Abs(sinAz) * 0.38f;
                float ipsi    = decay * (0.48f + ild) * frontScale;
                float contra  = decay * (0.48f - ild * 0.65f) * frontScale;
                float tapL    = sinAz >= 0f ? ipsi : contra;
                float tapR    = sinAz <= 0f ? ipsi : contra;

                taps.Add(new Tap { delay = delay,
                                   left  = tapL * 0.13f,
                                   right = tapR * 0.13f });
            }

            // ── Diffuse reverb tail ─────────────────────────────────────
            int   tailStart   = Mathf.RoundToInt(sr * 0.050f);
            int   tailSpan    = maxTail - tailStart;
            int   tailTaps    = Mathf.Max(1, nTaps - taps.Count);
            float tailStep    = tailSpan / (float)Mathf.Max(1, tailTaps - 1);

            for (int t = 0; t < tailTaps; t++)
            {
                int   delay = tailStart + Mathf.RoundToInt(t * tailStep);
                if (delay >= maxTail) break;

                float tSec  = delay / (float)sr;
                float decay = Mathf.Exp(-tSec * absorb * 10f);

                // Diffuse tail: mostly omni, with a gentle lateral spread
                // that depends on azimuth and density param
                float spread  = (0.30f + density * 0.38f) * sinAz;
                float omni    = decay * 0.055f;
                float tapL    = omni - spread * decay * 0.028f;
                float tapR    = omni + spread * decay * 0.028f;

                taps.Add(new Tap { delay = delay, left = tapL, right = tapR });
            }

            aziTable[az] = taps.ToArray();
        }
    }

    // ── Build binaural table from a mono kernel (WAV fallback) ─────────────
    void BuildBinauralTableFromMono(float[] monoKernel, int sr)
    {
        int tapLen = monoKernel.Length;
        int nTaps  = Mathf.Max(4, Mathf.Min(MaxTaps, tapLen));
        int stride = Mathf.Max(1, tapLen / nTaps);

        aziTable = new Tap[NumAzimuths][];
        for (int az = 0; az < NumAzimuths; az++)
        {
            float aziAngle = az / (float)NumAzimuths * 2f * Mathf.PI;
            float sinAz    = Mathf.Sin(aziAngle);
            var   taps     = new List<Tap>(nTaps);

            for (int i = 0; i < tapLen && taps.Count < nTaps; i += stride)
            {
                float v = monoKernel[i];
                if (Mathf.Abs(v) < 0.0008f) continue;
                float ild  = Mathf.Abs(sinAz) * 0.40f;
                float tapL = sinAz >= 0f ? v : v * (1f - ild);
                float tapR = sinAz <= 0f ? v : v * (1f - ild);
                taps.Add(new Tap { delay = Mathf.Max(1, i), left = tapL, right = tapR });
            }
            aziTable[az] = taps.ToArray();
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    static void NormalizeArray(float[] arr, float targetPeak)
    {
        float peak = 0f;
        for (int i = 0; i < arr.Length; i++)
            if (Mathf.Abs(arr[i]) > peak) peak = Mathf.Abs(arr[i]);
        if (peak < 0.00001f) return;
        float g = targetPeak / peak;
        for (int i = 0; i < arr.Length; i++) arr[i] *= g;
    }
}
