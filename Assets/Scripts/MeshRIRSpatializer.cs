using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// MeshRIR Spatializer v3 — implements the actual MeshRIR interpolation algorithm.
///
/// Algorithm (from sh01k/MeshRIR):
///   Given N source positions p_1…p_N each with a measured room IR h_1…h_N,
///   and a target source direction d_target:
///
///     IR_target = Σ wᵢ × hᵢ
///     wᵢ = (1/dist(d_target, dᵢ)²) / Σⱼ(1/dist(d_target, dⱼ)²)
///
///   This inverse-distance-weighted blend gives smooth spatial IR interpolation
///   across the full 360° without discontinuities.
///
/// Data loading (StreamingAssets/MeshRIR/):
///   pos_src.npy  — source positions  (numSrc × 3)
///   pos_mic.npy  — mic positions     (numMic × 3)
///   ir_0.npy     — IRs to mic 0      (numSrc × irLen)
///   Sampling rate assumed 48 kHz (resampled at load time if device differs)
///
/// Tune parameters → Rate / Depth / Energy / Material / Density
/// Built-in compressor + hard limiter on output.
/// </summary>
[DisallowMultipleComponent]
public class MeshRIRSpatializer : MonoBehaviour, IPlanetSpatializer
{
    // ── Tune parameters ────────────────────────────────────────────────────
    [Range(0f, 1f)] public float rate          = 0.50f; // direction-tracking speed
    [Range(0f, 1f)] public float depth         = 0.55f; // reverb tail length / kernel depth
    [Range(0f, 1f)] public float energy        = 0.60f; // wet spatial energy
    [Range(0f, 1f)] public float material      = 0.35f; // absorption (0=reflective,1=absorptive)
    [Range(0f, 1f)] public float density       = 0.50f; // tap density in diffuse tail
    // Flyby parameters — two axes of control, replacing the single opaque flybyIntensity:
    //   flySense  : how easily the whoosh triggers (low = only very fast/close passes,
    //               high = even slow orbits produce a noticeable sweep effect)
    //   flyStrength: how dramatic the effect is once triggered (far-ear occlusion depth,
    //               amplitude pulse height, near-ear brightness)
    [Range(0f, 1f)] public float flySense    = 0.65f;
    [Range(0f, 2f)] public float flyStrength = 1.00f;

    // ── Constants ──────────────────────────────────────────────────────────
    const int   RingSize     = 8192;    // power-of-2 delay line
    const int   MaxTapsPerSrc = 40;     // sparse taps extracted per source IR
    const int   MaxSources    = 128;
    const float MaxITD_s     = 0.00075f;
    const float CompThresh   = 0.42f;
    const float CompRatio    = 4.0f;
    const float LimiterCeil  = 0.90f;
    const int   MeshRIR_SR   = 48000;  // MeshRIR dataset sample rate

    // ── One sparse tap ─────────────────────────────────────────────────────
    struct Tap
    {
        public int delay;
        public float left;
        public float right;
    }

    // ── Per-source entry ───────────────────────────────────────────────────
    struct SrcEntry
    {
        public Vector2 dir;    // normalised 2D direction from listener (azimuth on unit circle)
        public Tap[]   taps;   // sparse taps extracted from this source's IR
    }

    SrcEntry[] sources;         // loaded/synthesised source entries

    // ── Delay line ─────────────────────────────────────────────────────────
    readonly float[] ring = new float[RingSize];
    int ringIdx;

    // ── Per-sample smooth-tracking state ───────────────────────────────────
    Vector2 targetDir = Vector2.up;
    Vector2 cachedDir = Vector2.up;
    float targetElev;
    float targetDistGain = 1.2f;
    float cachedElev;
    float cachedDistGain = 1.2f;

    // ── Compressor state ───────────────────────────────────────────────────
    float compEnvL, compEnvR;

    // ── Pre-allocated weight scratch buffer (avoids GC on audio thread) ────
    readonly float[] _targetWeights = new float[MaxSources];
    readonly float[] _smoothWeights = new float[MaxSources];

    // ── Smoothed ITD state (avoids binary switch at centre crossing) ────────
    float _cachedItdL;  // smoothed delayed-sample value for left ear
    float _cachedItdR;  //                                    right ear

    // ── Near-field flyby / Wwise-style 3D audio state ───────────────────────
    // Angular velocity is computed from orbital tangential speed / distance.
    // This spikes to large values as the source approaches dist → 0, which is
    // exactly the physical condition that produces the "whoosh past the ear".
    Vector3 _prevWorldPos  = Vector3.zero;   // for velocity estimation (position diff)
    Vector2 _prevTargetDir = Vector2.up;     // last valid approach direction
    float   _angVelSmooth  = 0f;    // smoothed ω = tanSpeed / clampedDist (rad/s)
    float   _distSmooth    = 300f;  // smoothed clamped source distance
    // HP filter state for near-ear spectral brightening (per audio-thread sample)
    float   _hpPrevInL    = 0f;
    float   _hpPrevOutL   = 0f;
    float   _hpPrevInR    = 0f;
    float   _hpPrevOutR   = 0f;

    // ── Misc ───────────────────────────────────────────────────────────────
    Transform listener;
    bool ready; // true after Start() finishes kernel build

    // ═══════════════════════════════════════════════════════════════════════
    void Awake()
    {
        listener = FindAnyObjectByType<AudioListener>()?.transform;
        // Kernel build deferred to Start() — AudioSettings requires main thread
    }

    void Start()
    {
        BuildSourceTable();
        ready = true;
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
        if (!ready || sources == null || sources.Length == 0)
        {
            left = mono; right = mono; return;
        }

        ring[ringIdx] = mono;

        // ── Per-sample smooth tracking ─────────────────────────────────────
        float k = 0.0024f + rate * 0.0100f;
        cachedDir += (targetDir - cachedDir) * k;
        if (cachedDir.sqrMagnitude < 0.0001f)
            cachedDir = Vector2.up;
        else
            cachedDir.Normalize();
        cachedElev     += (targetElev     - cachedElev)     * k;
        cachedDistGain += (targetDistGain - cachedDistGain) * 0.0010f;

        float dx = cachedDir.x;   // +1 = right
        float dz = cachedDir.y;   // +1 = front

        // ── IDW weights — smoothed per-sample to avoid sudden tap swaps ─────
        float wTotal = 0f;
        int   nSrc   = Mathf.Min(sources.Length, _targetWeights.Length);

        for (int i = 0; i < nSrc; i++)
        {
            float dot   = Mathf.Clamp(Vector2.Dot(cachedDir, sources[i].dir), -1f, 1f);
            float chord = Mathf.Max(0.0001f, 1f - dot);
            _targetWeights[i] = 1f / (chord * chord + 0.018f);
            wTotal += _targetWeights[i];
        }

        float invW    = wTotal > 0.00001f ? 1f / wTotal : 0f;
        float weightK = 0.0045f + rate * 0.018f;
        float smoothTotal = 0f;
        for (int i = 0; i < nSrc; i++)
        {
            _smoothWeights[i] += (_targetWeights[i] * invW - _smoothWeights[i]) * weightK;
            smoothTotal += _smoothWeights[i];
        }
        float invSmoothW = smoothTotal > 0.00001f ? 1f / smoothTotal : 0f;

        // ── Binaural direct path — smooth ITD crossfade, no binary switch ───
        // Instead of "if dx>0, delay left ear else delay right", we smoothly
        // blend: at dx=+1 left ear gets full delay, right ear gets direct.
        // At dx=0 both get the same signal. No discrete jump at centre crossing.
        int itdSamples = Mathf.RoundToInt(Mathf.Abs(dx) * MaxITD_s * AudioSettings.outputSampleRate);
        float monoDelayed = ReadRing(itdSamples);
        float lateralT  = (dx + 1f) * 0.5f;              // 0=full-left, 1=full-right
        float rawL = Mathf.Lerp(mono,       monoDelayed, lateralT);   // left ear delayed when source right
        float rawR = Mathf.Lerp(monoDelayed, mono,       lateralT);   // right ear delayed when source left

        // Smooth the per-channel direct signal an extra notch to kill any
        // residual glitch at the ITD rounding boundary (~0.02 ms at most)
        _cachedItdL += (rawL - _cachedItdL) * 0.55f;
        _cachedItdR += (rawR - _cachedItdR) * 0.55f;

        float halfPi   = Mathf.PI * 0.5f;
        float panAngle = Mathf.Clamp((dx + 1f) * 0.5f, 0f, 1f) * halfPi;
        float dlGain   = Mathf.Cos(panAngle);
        float drGain   = Mathf.Sin(panAngle);
        float absDx    = Mathf.Abs(dx);
        float shadow   = Mathf.Clamp01(absDx);
        // Front-back boost: 28% louder in front, 28% softer behind.
        // Critical for front/back azimuth perception on headphones.
        float frontBoost = 1f + dz * 0.28f;

        // ── Near-field flyby binaural physics ────────────────────────────────
        // avN: angular velocity normalised to [0,1].
        // flySense [0,1] maps to divisor [20,1]:
        //   flySense=0.0 → divisor=20 → only very fast/close passes trigger (subtle)
        //   flySense=0.65→ divisor≈7  → default: moderate orbits give avN≈0.3-0.5
        //   flySense=1.0 → divisor=1  → almost any movement produces an effect
        float avNDivisor = Mathf.Lerp(20f, 1f, flySense);
        float avN        = Mathf.Clamp01(_angVelSmooth / avNDivisor);

        // nearT: proximity boost for near-field ILD only.
        float nearT  = Mathf.Clamp01(1f - _distSmooth / 80f);

        // flybyT: raw factor — fast AND lateral.
        // effectiveT: scaled by flyStrength (how dramatic once triggered).
        float flybyT     = avN * shadow;
        float effectiveT = Mathf.Clamp01(flybyT * flyStrength);

        // Dynamic head shadow: deepens 0.36 → 0.02 at full effectiveT.
        // Stronger rest value (0.36 vs 0.52) gives more ILD even at slow orbits.
        // Far ear nearly silent during a fast lateral sweep = the Wwise sensation.
        float dynShadow = Mathf.Lerp(0.36f, 0.02f, effectiveT);

        // Near-field ILD: near ear boosted when source is physically close AND lateral.
        float nfBoost = 1f + nearT * absDx * 1.35f;

        if (dx > 0f)
        {
            dlGain *= Mathf.Lerp(1f, dynShadow, shadow);  // left = far ear → shadowed
            drGain *= nfBoost;                             // right = near ear → boosted
        }
        else
        {
            drGain *= Mathf.Lerp(1f, dynShadow, shadow);  // right = far ear → shadowed
            dlGain *= nfBoost;                             // left = near ear → boosted
        }

        // Flyby presence pulse (uses effectiveT — respects per-planet intensity).
        float flybyPulse = 1f + effectiveT * 0.80f;
        // Elevation gain: 22% louder overhead, 22% softer below — was 8% (inaudible).
        float elevGain   = 1f + cachedElev * 0.22f;

        // Direct gain decreases as Reverb (energy) rises — wider contrast so
        // wet/dry crossfade is clearly audible across the slider's full range.
        float directGain = Mathf.Lerp(1.15f, 0.58f, energy) * cachedDistGain * frontBoost * elevGain * flybyPulse;
        float l = _cachedItdL * directGain * dlGain;
        float r = _cachedItdR * directGain * drGain;

        // ── Wet: MeshRIR-interpolated room reflections ──────────────────────
        // Process ALL sources whose smoothed weight clears the threshold.
        // This replaces the old top-K selection which caused audible pops
        // whenever the set of "active" sources changed from one sample to the next.
        // Reverb (energy): 0 = fully dry, 1 = fully wet. Boosted range so the
        // slider has clearly audible effect across its travel.
        float wetGain  = energy * Mathf.Lerp(0.65f, 2.10f, depth) * cachedDistGain;

        float tapKeep  = Mathf.Lerp(0.28f, 1f, density);

        // Damp (material): 0 = hard/reflective (long tail), 1 = absorptive (short tail).
        // Fixed direction: was Lerp(1.85,0.62,material) which was backwards — higher
        // material gave LESS tailDecay (longer reverb), opposite of "more damping".
        float tailDecay = Mathf.Lerp(8.5f, 1.8f, depth) * Mathf.Lerp(0.55f, 2.20f, material);
        float srInv    = 1f / Mathf.Max(1, AudioSettings.outputSampleRate);
        const float MinWetWeight = 0.022f; // sources below this contribute negligibly

        for (int i = 0; i < nSrc; i++)
        {
            float w = _smoothWeights[i] * invSmoothW;
            if (w < MinWetWeight) continue;

            Tap[] taps = sources[i].taps;
            if (taps == null || taps.Length == 0) continue;

            int activeTaps = Mathf.Clamp(Mathf.CeilToInt(taps.Length * tapKeep), 1, taps.Length);
            float ws = w * wetGain;

            for (int t = 0; t < activeTaps; t++)
            {
                float s    = ReadRing(taps[t].delay) * ws;
                float damp = Mathf.Exp(-taps[t].delay * srInv * tailDecay);
                l += s * taps[t].left  * damp;
                r += s * taps[t].right * damp;
            }
        }

        // ── Near-ear spectral brightening (pinna / ear-canal proximity effect) ─
        // One-pole HP filter, α ≈ 0.82 → Fc ≈ 1.5 kHz at 48 kHz.
        // During a fast flyby the near ear picks up a crisp high-frequency edge —
        // the characteristic "whoosh" timbre absent in far-field spatializers.
        const float hpA = 0.82f;
        float hpL = hpA * (_hpPrevOutL + l - _hpPrevInL);
        _hpPrevInL = l;  _hpPrevOutL = hpL;
        float hpR = hpA * (_hpPrevOutR + r - _hpPrevInR);
        _hpPrevInR = r;  _hpPrevOutR = hpR;

        // Near-ear spectral brightening — uses effectiveT so flyStrength scales it too.
        // At effectiveT=1: +60 % HP blend → crisp "whoosh" edge on the near ear.
        float brightAmt = effectiveT * 0.60f;
        if (dx > 0f) r += hpR * brightAmt;   // source right → right = near ear
        else         l += hpL * brightAmt;   // source left  → left  = near ear

        // ── Elevation spectral tilt (pinna cue) ─────────────────────────────
        // Above the listener: pinna reflects high frequencies into the ear canal
        //   → add HP content (brighter).
        // Below the listener: ear canal shadowed → subtract HP (darker, LP-like).
        //   signal - HP ≈ LP, so this naturally darkens the high shelf.
        float elevAbove = Mathf.Clamp01( cachedElev);   // 0..1 overhead
        float elevBelow = Mathf.Clamp01(-cachedElev);   // 0..1 below
        l += hpL * elevAbove * 0.55f;
        r += hpR * elevAbove * 0.55f;
        l -= hpL * elevBelow * 0.28f;
        r -= hpR * elevBelow * 0.28f;

        ringIdx = (ringIdx + 1) & (RingSize - 1);

        // ── Compressor + limiter ────────────────────────────────────────────
        left  = ApplyDynamics(l, ref compEnvL);
        right = ApplyDynamics(r, ref compEnvR);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Direction tracking
    // ═══════════════════════════════════════════════════════════════════════
    void UpdateDirection()
    {
        if (listener == null)
            listener = FindAnyObjectByType<AudioListener>()?.transform;
        if (listener == null) return;

        Vector3 world = transform.position - listener.position;
        float   dist  = world.magnitude;

        // ── Minimum clamp distance (≈ head radius) ─────────────────────────
        // Fig8 / Rose orbits pass through the listener's position — we never
        // let dist reach zero.  At MinDist the source is treated as being right
        // at the ear, which produces maximum ILD and the loudest flyby pulse.
        const float MinDist = 2f;
        float clampedDist = Mathf.Max(MinDist, dist);

        // ── Direction: freeze inside the "head zone" ────────────────────────
        // When the planet is closer than MinDist*0.5f we keep the last valid
        // approach direction rather than resetting to Vector2.up.  The moment
        // it re-emerges on the other side the direction flips, driving a massive
        // angular-velocity spike exactly as desired.
        if (dist >= MinDist * 0.5f)
        {
            Vector3 local = listener.InverseTransformDirection(world / clampedDist);
            var dir = new Vector2(local.x, local.z);
            if (dir.sqrMagnitude > 0.0001f)
            {
                targetDir  = dir.normalized;
                targetElev = Mathf.Clamp(local.y, -1f, 1f);
            }
        }
        // else: keep last targetDir & targetElev — source is "inside the head"

        // ── Distance gain: inverse-distance loudness boost at close range ───
        // As dist → MinDist the gain rises to 2.8× so a flyby is viscerally
        // louder, consistent with real acoustic inverse-square law.
        float dist01   = Mathf.Clamp01((clampedDist - MinDist) / (680f - MinDist));
        targetDistGain = Mathf.Lerp(2.8f, 1.05f, dist01);

        // ── Angular velocity: ω = tangential speed / distance ───────────────
        // This is the physically correct formulation.  Unlike the cross-product
        // approach (which gives 0 for a 180° flip), this spikes correctly as
        // clampedDist shrinks — exactly when a fig8 / fast planet flies past.
        Vector3 vel      = (transform.position - _prevWorldPos)
                           / Mathf.Max(Time.deltaTime, 0.001f);
        _prevWorldPos    = transform.position;
        Vector3 los      = dist > 0.001f ? world / dist : new Vector3(_prevTargetDir.x, 0f, _prevTargetDir.y);
        float   tanSpeed = (vel - Vector3.Dot(vel, los) * los).magnitude;
        float   angVel   = tanSpeed / clampedDist;

        // ── Peak-hold envelope: fast attack / slow decay ──────────────────────
        // Symmetric smoothing (0.18) caused ~90 ms lag: the whoosh peaked well
        // after the closest-approach moment.  With fast attack (0.72) the value
        // rises within ~1 frame, so the effect lands right when the eye sees it.
        // Slow decay (0.04) lets the sensation linger naturally after the pass.
        float attackK = angVel > _angVelSmooth ? 0.72f : 0.04f;
        _angVelSmooth  += (angVel      - _angVelSmooth) * attackK;
        _distSmooth    += (clampedDist - _distSmooth)   * 0.04f;
        _prevTargetDir  = targetDir;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Dynamics
    // ═══════════════════════════════════════════════════════════════════════
    float ApplyDynamics(float x, ref float env)
    {
        float absX = Mathf.Abs(x);
        float spd  = absX > env ? 0.28f : 0.0007f;
        env += (absX - env) * spd;

        float gain = 1f;
        if (env > CompThresh)
        {
            float over = env - CompThresh;
            gain = (CompThresh + over / CompRatio) / Mathf.Max(env, 0.00001f);
        }
        x *= gain;

        return (float)Math.Tanh(x / LimiterCeil) * LimiterCeil;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Ring buffer
    // ═══════════════════════════════════════════════════════════════════════
    float ReadRing(int delay)
    {
        if (delay >= RingSize) delay = RingSize - 1;
        int idx = ringIdx - delay;
        if (idx < 0) idx += RingSize;
        return ring[idx & (RingSize - 1)];
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Source table construction
    // ═══════════════════════════════════════════════════════════════════════
    void BuildSourceTable()
    {
        int sr = Mathf.Max(1, AudioSettings.outputSampleRate);

        // ── Try loading actual MeshRIR dataset ──────────────────────────────
        if (TryLoadMeshRIRDataset(sr))
        {
            Debug.Log($"[MeshRIR] Loaded {sources.Length} sources from dataset (sr={sr})");
            return;
        }

        // ── Synthetic fallback: 32 sources on a circle ──────────────────────
        BuildSyntheticSources(sr, 32);
        Debug.Log($"[MeshRIR] Using synthetic source table ({sources.Length} positions). " +
                   "Place MeshRIR .npy files under StreamingAssets/MeshRIR/ for real data.");
    }

    // ── Real dataset loading ────────────────────────────────────────────────
    bool TryLoadMeshRIRDataset(int sr)
    {
        string root = Path.Combine(Application.streamingAssetsPath, "MeshRIR");
        if (!Directory.Exists(root)) return false;

        // Require pos_src.npy and at least one ir_N.npy
        string posSrcPath = Path.Combine(root, "pos_src.npy");
        if (!File.Exists(posSrcPath)) return false;

        // Find mic IR files: ir_0.npy, ir_1.npy …
        var irFiles = new List<string>(Directory.GetFiles(root, "ir_*.npy", SearchOption.TopDirectoryOnly));
        irFiles.Sort(StringComparer.OrdinalIgnoreCase);
        if (irFiles.Count == 0) return false;

        if (!SpatialAudioNpy.TryRead(posSrcPath, out var srcPosArray)) return false;

        // pos_src shape: (numSrc, 3) → flatten: [x0,y0,z0, x1,y1,z1, …]
        int numSrc = srcPosArray.values.Length / 3;
        if (numSrc < 2) return false;

        // Use a stereo mic pair when available. A mono file still works, but
        // true left/right IRs are what make the MeshRIR branch worth having.
        if (!SpatialAudioNpy.TryRead(irFiles[0], out var leftIrArray)) return false;
        var rightIrArray = leftIrArray;
        if (irFiles.Count > 1)
        {
            if (SpatialAudioNpy.TryRead(irFiles[1], out var candidateRight))
                rightIrArray = candidateRight;
        }

        // ir_N.npy shape: (numSrc, irLen)
        int irLen   = leftIrArray.shape[leftIrArray.shape.Length - 1];
        int srcCheck = leftIrArray.values.Length / Mathf.Max(1, irLen);
        int rightSrcCheck = rightIrArray.values.Length / Mathf.Max(1, irLen);
        if (rightSrcCheck < srcCheck) srcCheck = rightSrcCheck;
        if (srcCheck < numSrc) numSrc = srcCheck;

        // Find listener reference position (centroid of mic grid, or first mic)
        Vector3 listenerPos = Vector3.zero;
        string posMicPath = Path.Combine(root, "pos_mic.npy");
        if (File.Exists(posMicPath) && SpatialAudioNpy.TryRead(posMicPath, out var micPosArray))
        {
            int numMic = micPosArray.values.Length / 3;
            for (int i = 0; i < numMic; i++)
            {
                listenerPos.x += micPosArray.values[i * 3 + 0];
                listenerPos.y += micPosArray.values[i * 3 + 1];
                listenerPos.z += micPosArray.values[i * 3 + 2];
            }
            if (numMic > 0) listenerPos /= numMic;
        }

        // Build SrcEntry for each source position
        float resampleRatio = MeshRIR_SR / (float)Mathf.Max(1, sr);
        int   tapLen = Mathf.Min(RingSize - 1, Mathf.CeilToInt(irLen / resampleRatio));

        sources = new SrcEntry[numSrc];
        for (int s = 0; s < numSrc; s++)
        {
            // Source position relative to listener
            float px = srcPosArray.values[s * 3 + 0] - listenerPos.x;
            float pz = srcPosArray.values[s * 3 + 2] - listenerPos.z;
            float mag = Mathf.Sqrt(px * px + pz * pz);
            Vector2 dir = mag > 0.001f ? new Vector2(px / mag, pz / mag) : Vector2.up;

            // Extract and resample this source's IR
            int offset = s * irLen;
            var taps   = ExtractSparseTaps(leftIrArray.values, rightIrArray.values, offset, irLen, tapLen, resampleRatio);

            sources[s] = new SrcEntry { dir = dir, taps = taps };
        }
        return true;
    }

    // ── Resample + extract sparse taps from a float[] slice ────────────────
    Tap[] ExtractSparseTaps(float[] leftData, float[] rightData, int offset, int srcLen, int dstLen, float ratio)
    {
        // Resample: srcLen @ MeshRIR_SR → dstLen @ device SR
        var left = new float[dstLen];
        var right = new float[dstLen];
        for (int i = 0; i < dstLen; i++)
        {
            float srcF = i * ratio;
            int   i0   = Mathf.Clamp((int)srcF, 0, srcLen - 1);
            int   i1   = Mathf.Min(i0 + 1, srcLen - 1);
            float t = srcF - i0;
            left[i] = Mathf.Lerp(leftData[offset + i0], leftData[offset + i1], t);
            right[i] = Mathf.Lerp(rightData[offset + i0], rightData[offset + i1], t);
        }

        // Normalise
        float peak = 0f;
        for (int i = 0; i < dstLen; i++)
        {
            peak = Mathf.Max(peak, Mathf.Abs(left[i]));
            peak = Mathf.Max(peak, Mathf.Abs(right[i]));
        }
        float normG = peak > 0.00001f ? 0.28f / peak : 1f;
        for (int i = 0; i < dstLen; i++)
        {
            left[i] *= normG;
            right[i] *= normG;
        }

        // Build sparse tap list. Runtime density decides how many are used, so
        // extraction keeps the full budget for continuous slider behaviour.
        int nTaps  = MaxTapsPerSrc;
        int stride = Mathf.Max(1, dstLen / nTaps);

        var result = new List<Tap>(nTaps);
        for (int i = stride / 2; i < dstLen && result.Count < nTaps; i += stride)
        {
            float l = left[i];
            float r = right[i];
            if (Mathf.Abs(l) + Mathf.Abs(r) < 0.0008f) continue;
            result.Add(new Tap { delay = Mathf.Max(1, i), left = l, right = r });
        }
        return result.ToArray();
    }

    // ── Synthetic sources ───────────────────────────────────────────────────
    void BuildSyntheticSources(int sr, int numSrc)
    {
        float tailMs  = 190f;
        int   maxTail = Mathf.Min(RingSize - 1, Mathf.RoundToInt(sr * tailMs / 1000f));
        int   nTaps   = MaxTapsPerSrc;

        sources = new SrcEntry[numSrc];
        for (int s = 0; s < numSrc; s++)
        {
            float aziRad = s / (float)numSrc * 2f * Mathf.PI;
            float sx     = Mathf.Sin(aziRad);
            float sz     = Mathf.Cos(aziRad);
            var   dir    = new Vector2(sx, sz);

            var taps = new List<Tap>(nTaps);

            // Early reflections — arrival time shifts with lateral position
            float[] earlyMs = { 4f, 8f, 14f, 22f, 34f, 50f };
            for (int e = 0; e < earlyMs.Length && taps.Count < nTaps / 2; e++)
            {
                float lateralShift = 1f + sx * 0.12f;
                int   delay = Mathf.RoundToInt(sr * earlyMs[e] * 0.001f * lateralShift);
                if (delay < 1 || delay >= maxTail) continue;
                float tSec  = delay / (float)sr;
                float decay = Mathf.Exp(-tSec * 5.2f);
                float frontBoost = 1f + sz * 0.16f;
                AddDirectionalTap(taps, delay, decay * frontBoost * 0.13f, sx);
            }

            // Diffuse tail
            int   tailStart = Mathf.RoundToInt(sr * 0.055f);
            int   tailSpan  = maxTail - tailStart;
            int   nDiffuse  = Mathf.Max(1, nTaps - taps.Count);
            float step      = tailSpan / (float)Mathf.Max(1, nDiffuse - 1);
            for (int t = 0; t < nDiffuse; t++)
            {
                int   delay = tailStart + Mathf.RoundToInt(t * step);
                if (delay >= maxTail) break;
                float tSec  = delay / (float)sr;
                float decay = Mathf.Exp(-tSec * 3.0f);
                AddDirectionalTap(taps, delay, decay * 0.055f, sx * 0.45f);
            }

            sources[s] = new SrcEntry { dir = dir, taps = taps.ToArray() };
        }
    }

    void AddDirectionalTap(List<Tap> taps, int delay, float gain, float sx)
    {
        float pan = Mathf.Clamp(sx, -1f, 1f);
        float angle = (pan + 1f) * Mathf.PI * 0.25f;
        float l = Mathf.Cos(angle);
        float r = Mathf.Sin(angle);
        float shadow = Mathf.Clamp01(Mathf.Abs(pan));
        if (pan > 0f) l *= Mathf.Lerp(1f, 0.62f, shadow);
        else if (pan < 0f) r *= Mathf.Lerp(1f, 0.62f, shadow);
        taps.Add(new Tap { delay = delay, left = gain * l, right = gain * r });
    }
}
