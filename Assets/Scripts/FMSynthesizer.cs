using UnityEngine;
using System;

[RequireComponent(typeof(AudioSource))]
public class FMSynthesizer : MonoBehaviour
{
    public enum OscType { Sine, Triangle, Sawtooth, Square }
    public enum PhysicalModel
    {
        Bubble,
        SoftMetal,
        RunningWater,
        Fire,
        Stone,
        WoodStickSlip,
        IceMetal,
        DeepPour,
        IceRain
    }

    const int EventVoices = 40;
    const int ModalVoices = 36;
    const double TwoPi = Math.PI * 2.0;

    [Header("Master")]
    public int planetIndex = 1;
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 2.5f)] public float volumeScale = 1f;

    [Header("Physical Model")]
    public PhysicalModel model = PhysicalModel.RunningWater;
    [Range(40f, 500f)] public float carrierNote = 160f; // Depth / body size.
    [Range(0.5f, 8f)] public float modRatio = 2f;       // Cavity / material size.
    [Range(0f, 10f)] public float modIndex = 3f;         // Energy / turbulence.
    [Range(0f, 5f)] public float lfoRate = 1f;           // Event / flow rate.
    [Range(0f, 0.3f)] public float lfoDepth = 0.05f;     // Micro motion.

    [Header("Gesture")]
    public bool pulseEnabled = false;
    [Range(0.1f, 8f)] public float pulseRate = 1f;
    [Range(0.01f, 0.3f)] public float pulseDecay = 0.08f;
    public OscType oscType = OscType.Sine;

    AudioSource audioSource;
    IPlanetSpatializer spatializer;
    PlanetOrbit orbit;
    Transform audioListener;

    double sampleRate;
    uint rngState = 1u;
    float targetMasterVolume = 1f;

    float smoothedVolume;
    float smoothedDepth;
    float smoothedSize;
    float smoothedEnergy;
    float smoothedRate;
    float smoothedMotion;
    float orbitDistance01;
    float orbitSpeed01;

    // ── Doppler ─────────────────────────────────────────────────────────────
    float dopplerShift = 1f;   // smoothed pitch multiplier (>1 = approaching)

    float lowNoise;
    float slowNoise;
    float bandA;
    float bandB;
    float bandC;
    float hpLastInput;
    float hpLastOutput;
    float outputSmooth;
    float pressure;
    float pressureTarget;
    float lastPressure;
    float pourLevel;
    float pourTarget;
    double pourTargetTimer;
    float tubePhase;
    float flamePhase;
    float rollPhase;
    float stickForce;
    float lastSlipGap = 0.1f;

    double mainTimer;
    double secondaryTimer;
    double pulseTimer;
    double pulseEnv = 1.0;

    readonly double[] eventPhase = new double[EventVoices];
    readonly double[] eventFreq = new double[EventVoices];
    readonly double[] eventTargetFreq = new double[EventVoices];
    readonly double[] eventEnv = new double[EventVoices];
    readonly double[] eventDecay = new double[EventVoices];
    readonly double[] eventBend = new double[EventVoices];
    readonly float[] eventGain = new float[EventVoices];
    // Per-voice slow-varying pitch wobble — breaks sinusoidal pitch coherence
    readonly double[] eventWobble = new double[EventVoices];
    int eventVoice;

    readonly double[] modalPhase = new double[ModalVoices];
    readonly double[] modalFreq = new double[ModalVoices];
    readonly double[] modalEnv = new double[ModalVoices];
    readonly double[] modalDecay = new double[ModalVoices];
    readonly float[] modalGain = new float[ModalVoices];
    int modalVoice;

    static readonly PhysicalModel[] PlanetModels =
    {
        PhysicalModel.Bubble,
        PhysicalModel.SoftMetal,
        PhysicalModel.RunningWater,
        PhysicalModel.Fire,
        PhysicalModel.Stone,
        PhysicalModel.WoodStickSlip,
        PhysicalModel.IceMetal,
        PhysicalModel.DeepPour,
        PhysicalModel.IceRain
    };

    static readonly float[] PlanetDepth =
    {
        260f, 340f, 170f, 145f, 86f, 210f, 390f, 72f, 310f
    };

    static readonly float[] PlanetSize =
    {
        1.6f, 4.6f, 3.1f, 2.2f, 6.9f, 5.3f, 3.8f, 7.4f, 1.3f
    };

    static readonly float[] PlanetEnergy =
    {
        5.2f, 3.0f, 4.8f, 4.2f, 5.6f, 3.8f, 3.5f, 5.0f, 4.6f
    };

    static readonly float[] PlanetRate =
    {
        3.7f, 0.75f, 2.45f, 1.45f, 1.05f, 0.85f, 0.62f, 1.25f, 2.3f
    };

    static readonly float[] PlanetVolumes =
    {
        1.08f, 0.82f, 0.92f, 0.98f, 1.34f, 1.18f, 0.85f, 1.42f, 1.48f
    };

    public string ModelLabel
    {
        get
        {
            switch (model)
            {
                case PhysicalModel.Bubble: return "bubble cavities";
                case PhysicalModel.SoftMetal: return "soft metal";
                case PhysicalModel.RunningWater: return "running water";
                case PhysicalModel.Fire: return "ember fire";
                case PhysicalModel.Stone: return "stone body";
                case PhysicalModel.WoodStickSlip: return "wood slip";
                case PhysicalModel.IceMetal: return "ice metal";
                case PhysicalModel.DeepPour: return "deep pour";
                case PhysicalModel.IceRain: return "ice rain";
                default: return "physical";
            }
        }
    }

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.spatialize = false;
        audioSource.dopplerLevel = 0f;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 1f;
        audioSource.minDistance = 9999f;
        audioSource.maxDistance = 10000f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;

        sampleRate = AudioSettings.outputSampleRate;
        spatializer = GetComponent<IPlanetSpatializer>();
        orbit = GetComponent<PlanetOrbit>();
    }

    public void Init()
    {
        int idx = Mathf.Clamp(planetIndex - 1, 0, PlanetModels.Length - 1);
        model = PlanetModels[idx];
        carrierNote = PlanetDepth[idx];
        modRatio = PlanetSize[idx];
        modIndex = PlanetEnergy[idx];
        lfoRate = PlanetRate[idx];
        volumeScale = PlanetVolumes[idx];
        lfoDepth = 0.035f + idx * 0.004f;
        pulseEnabled = false;

        rngState = 0x9E3779B9u + (uint)planetIndex * 747796405u;
        pressureTarget = NextBipolar() * 0.35f;
        pourLevel = NextUnitFloat();
        pourTarget = NextUnitFloat();
        pourTargetTimer = 0.6 + NextUnit() * 1.8;
        smoothedVolume = masterVolume * volumeScale;
        smoothedDepth = carrierNote;
        smoothedSize = Mathf.InverseLerp(0.5f, 8f, modRatio);
        smoothedEnergy = Mathf.Clamp01(modIndex / 10f);
        smoothedRate = Mathf.Clamp01(lfoRate / 5f);
        UpdateOrbitBands();

        mainTimer = 0.04 + NextUnit() * 0.2;
        secondaryTimer = 0.2 + NextUnit() * 1.2;

        audioSource.Play();
    }

    void Update()
    {
        UpdateOrbitBands();
    }

    void OnEnable()
    {
        if (audioSource != null && !audioSource.isPlaying)
            audioSource.Play();
    }

    public void SetActive(bool active)
    {
        targetMasterVolume = active ? 1f : 0f;
        if (active && !audioSource.isPlaying)
            audioSource.Play();
    }

    public void SetDopplerLevel(float level)
    {
        audioSource.dopplerLevel = Mathf.Min(level, 0.03f);
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (sampleRate <= 0)
            sampleRate = AudioSettings.outputSampleRate;

        double dt = 1.0 / sampleRate;
        float smooth = 1f - Mathf.Exp(-1f / ((float)sampleRate * 0.045f));

        for (int i = 0; i < data.Length; i += channels)
        {
            masterVolume = Mathf.MoveTowards(masterVolume, targetMasterVolume, (float)(dt / 0.18));
            smoothedVolume = Mathf.Lerp(smoothedVolume, masterVolume * volumeScale, smooth);
            smoothedDepth = Mathf.Lerp(smoothedDepth, carrierNote, smooth);
            smoothedSize = Mathf.Lerp(smoothedSize, Mathf.InverseLerp(0.5f, 8f, modRatio), smooth);
            smoothedEnergy = Mathf.Lerp(smoothedEnergy, Mathf.Clamp01(modIndex / 10f), smooth);
            smoothedRate = Mathf.Lerp(smoothedRate, Mathf.Clamp01(lfoRate / 5f), smooth);
            smoothedMotion = Mathf.Lerp(smoothedMotion, orbitSpeed01 * 0.65f + orbitDistance01 * 0.35f, smooth * 0.5f);

            double sample = GeneratePhysicalSample(dt);

            if (pulseEnabled)
            {
                pulseTimer += dt;
                if (pulseTimer >= 1.0 / Math.Max(0.1f, pulseRate))
                {
                    pulseTimer = 0.0;
                    pulseEnv = 1.0;
                }
                pulseEnv *= Math.Exp(-dt / Math.Max(0.01f, pulseDecay));
                sample *= 0.55 + pulseEnv * 0.45;
            }

            float output = (float)(Math.Tanh(sample * 1.15) * smoothedVolume * 0.34);
            output = HighPassDc(output);
            // Coefficient raised from 0.62 → 0.88: only smooths sample-level glitches,
            // no longer attenuates mid frequencies which was adding a veiled, noisy quality
            outputSmooth += (output - outputSmooth) * 0.88f;
            output = Mathf.Clamp(outputSmooth, -0.96f, 0.96f);

            if (float.IsNaN(output) || float.IsInfinity(output))
                output = 0f;

            if (spatializer != null && channels >= 2)
            {
                spatializer.ProcessSample(output, out float left, out float right);
                // Safety soft-limiter: catches any burst the spatializer may produce
                // (convolution can accumulate energy beyond ±1 at peak impulse density)
                data[i]     = SoftLimit(left);
                data[i + 1] = SoftLimit(right);
                for (int c = 2; c < channels; c++)
                    data[i + c] = 0f;
            }
            else
            {
                for (int c = 0; c < channels; c++)
                    data[i + c] = output;
            }
        }
    }

    double GeneratePhysicalSample(double dt)
    {
        switch (model)
        {
            case PhysicalModel.Bubble:
                // Mercury: pure discrete bubble pops — no continuous waterbed underneath.
                // The character should be unmistakably "individual bubbles", not flow.
                return GenerateBubbleModel(dt, false) * 1.40;
            case PhysicalModel.SoftMetal:
                return GenerateModalMetal(dt, false) * 0.92;
            case PhysicalModel.RunningWater:
                // Earth: continuous flowing water — vortex turbulence + bed noise dominate.
                // The old GenerateBubbleModel(sparse) secondary was making it sound identical
                // to Mercury. Replaced entirely with a stream-noise layer.
                return GenerateRunningWater(dt) * 1.55;
            case PhysicalModel.Fire:
                return GenerateFire(dt);
            case PhysicalModel.Stone:
                return GenerateStone(dt);
            case PhysicalModel.WoodStickSlip:
                return GenerateWoodStickSlip(dt);
            case PhysicalModel.IceMetal:
                return GenerateModalMetal(dt, true) * 1.08 + GenerateAirShimmer(dt) * 0.08;
            case PhysicalModel.DeepPour:
                return GenerateDeepPour(dt);
            case PhysicalModel.IceRain:
                return GenerateIceRain(dt);
            default:
                return GenerateRunningWater(dt);
        }
    }

    double GenerateRunningWater(double dt)
    {
        // Lower event rate and lower frequency range than Bubble —
        // these are large-scale eddies/gurgles, not individual cavity pops.
        double rate = 8.0 + smoothedRate * 22.0 + smoothedEnergy * 10.0;
        mainTimer -= dt;
        if (mainTimer <= 0.0)
        {
            pressureTarget = BilinearRandom() * (0.35f + smoothedEnergy * 0.5f);
            // Single burst, lower freq (60–900 Hz) = deep water gurgle, not a bubble chirp
            TriggerWaterCavity(55f, Mathf.Lerp(400f, 900f, 1f - Depth01()), 0.032f, 0.18f);
            mainTimer += 1.0 / Math.Max(3.0, rate);
        }

        float slew = Mathf.Lerp(0.0048f, 0.022f, smoothedRate) * Mathf.Lerp(1.35f, 0.72f, smoothedSize);
        pressure += (pressureTarget - pressure) * slew;
        float diff = pressure - lastPressure;
        lastPressure = pressure;
        // Vortex is the dominant character: rolling, continuous, pressure-driven turbulence
        float vortex = Mathf.Sign(diff) * diff * diff * 82f;

        double cavities = RenderEventVoices(dt, 0.0032);
        double bed  = GenerateWaterBed(dt, 0.68f + smoothedEnergy * 0.30f);
        // Bed + vortex dominate (sounds like a river/stream); cavities are subtle undertones
        return bed * 0.92 + vortex * 0.62 + cavities * 0.40;
    }

    double GenerateBubbleModel(double dt, bool sparse)
    {
        double rate = sparse
            ? 4.0 + smoothedRate * 12.0 + smoothedEnergy * 5.0
            : 10.0 + smoothedRate * 22.0 + smoothedEnergy * 10.0;
        mainTimer -= dt;
        if (mainTimer <= 0.0)
        {
            // Mercury: high-frequency range (400–6000 Hz), very short duration (8–35 ms).
            // The fast bend (0.0042) makes the frequency chirp audibly downward —
            // the distinctive Minnaert resonance of a collapsing gas cavity.
            float low  = Mathf.Lerp(400f,  1200f, 1f - smoothedSize);
            float high = Mathf.Lerp(2800f, 6000f, 1f - Depth01());
            TriggerWaterCavityChirp(low, high, 0.008f, 0.035f);
            mainTimer += 1.0 / Math.Max(1.5, rate + NextUnit() * rate * 0.50);
        }

        return RenderEventVoices(dt, 0.0042) * 1.30;
    }

    // Like TriggerWaterCavity but with a faster, steeper frequency chirp and
    // shorter envelope — sounds like a single sharp bubble "tock" rather than a tone.
    void TriggerWaterCavityChirp(float low, float high, float minDur, float maxDur)
    {
        int v = eventVoice++ % EventVoices;
        float sizeSkew = Mathf.Pow(NextUnitFloat(), Mathf.Lerp(2.8f, 0.9f, smoothedEnergy));
        float start = low * Mathf.Pow(Mathf.Max(1.01f, high / low), sizeSkew);
        start *= (0.78f + NextUnitFloat() * 0.46f) * dopplerShift;
        // Target is LOWER than start — a sharp descending chirp (rising for real Minnaert but
        // downward sounds more natural for our bubble character)
        eventFreq[v]       = start;
        eventTargetFreq[v] = start * (0.38f + NextUnitFloat() * 0.22f);  // drops to 40–60% of start
        eventBend[v]       = 0.0055 + smoothedRate * 0.003 + smoothedEnergy * 0.002; // fast bend
        eventPhase[v]      = NextUnit();
        eventEnv[v]        = (0.18f + smoothedEnergy * 0.40f) * (0.5f + NextUnitFloat() * 0.8f);
        eventGain[v]       = 0.22f + smoothedEnergy * 0.40f;
        double dur = minDur + NextUnit() * Math.Max(0.002, maxDur);
        eventDecay[v]      = Math.Exp(-1.0 / (sampleRate * dur));
        eventWobble[v]     = NextUnit() * 2.0 - 1.0;
    }

    double GenerateDeepPour(double dt)
    {
        pourTargetTimer -= dt;
        if (pourTargetTimer <= 0.0)
        {
            pourTarget = Mathf.Clamp01(0.16f + NextUnitFloat() * 0.78f);
            pourTargetTimer = 0.8 + NextUnit() * (3.2 - smoothedRate * 1.5);
        }

        float liquidSlew = (0.0012f + smoothedRate * 0.0024f) * Mathf.Lerp(0.7f, 1.6f, smoothedEnergy);
        pourLevel += (pourTarget - pourLevel) * liquidSlew;

        float liquidDepth = Mathf.SmoothStep(0.1f, 1f, pourLevel);
        float cavity = 1f - liquidDepth;
        double rate = 9.0 + smoothedRate * 20.0 + smoothedEnergy * 10.0;
        mainTimer -= dt;
        if (mainTimer <= 0.0)
        {
            float low = Mathf.Lerp(70f, 360f, liquidDepth);
            float high = Mathf.Lerp(420f, 1800f, cavity);
            TriggerWaterCavity(low, high, 0.028f, 0.18f);
            mainTimer += 1.0 / Math.Max(2.0, rate);
        }

        // Tube sine oscillator removed — it created a clear periodic descending pitch
        // as pourLevel tracked through its random targets. Replaced with a wider
        // water bed whose amount breathes with the liquid depth, giving a "container
        // resonance" character without any perceptible fundamental frequency.
        float bedAmount = 0.74f + cavity * 0.48f + smoothedEnergy * 0.22f;
        double bed = GenerateWaterBed(dt, bedAmount);
        return bed * 1.25 + RenderEventVoices(dt, 0.0038) * 0.92;
    }

    double GenerateIceRain(double dt)
    {
        double rate = 10.0 + smoothedRate * 38.0 + smoothedEnergy * 16.0;
        mainTimer -= dt;
        if (mainTimer <= 0.0)
        {
            if (NextUnitFloat() < 0.7f)
                TriggerClickModal(900f + NextUnitFloat() * 2400f, 0.035f, 0.16f, 0.1f);
            TriggerWaterCavity(520f, 4200f, 0.008f, 0.052f);
            mainTimer += PoissonGap(rate);
        }

        return RenderModalVoices(dt) * 0.62 + RenderEventVoices(dt, 0.0028) * 0.72 + GenerateAirShimmer(dt) * 0.05;
    }

    double GenerateFire(double dt)
    {
        flamePhase = Wrap01(flamePhase + (1.2f + smoothedRate * 3.4f) * (float)dt);
        double lap = GenerateFilteredNoise(0.008f + smoothedEnergy * 0.01f, 0.0018f) *
            (0.34 + 0.18 * Math.Sin(flamePhase * TwoPi));

        double crackRate = 6.0 + smoothedEnergy * 28.0 + smoothedRate * 12.0;
        mainTimer -= dt;
        if (mainTimer <= 0.0)
        {
            float root = Mathf.Lerp(900f, 3600f, NextUnitFloat());
            TriggerClickModal(root, 0.022f, 0.08f, 0.16f + smoothedEnergy * 0.22f);
            mainTimer += PoissonGap(crackRate);
        }

        return lap * 0.72 + RenderModalVoices(dt) * 0.68;
    }

    double GenerateStone(double dt)
    {
        rollPhase = Wrap01(rollPhase + (0.24f + smoothedRate * 2.2f) * (float)dt);
        double bumpRate = 7.0 + smoothedRate * 18.0 + smoothedEnergy * 10.0;
        mainTimer -= dt;
        if (mainTimer <= 0.0)
        {
            float root = Mathf.Lerp(42f, 110f, Depth01()) * (0.8f + NextUnitFloat() * 0.45f);
            TriggerStoneModal(root, 0.12f, 0.55f);
            mainTimer += PoissonGap(bumpRate);
        }

        double rumble = GenerateFilteredNoise(0.003f + smoothedEnergy * 0.004f, 0.0009f) * 1.2;
        return rumble * 0.46 + RenderModalVoices(dt) * 0.96;
    }

    double GenerateWoodStickSlip(double dt)
    {
        float push = (0.08f + smoothedRate * 0.25f + smoothedEnergy * 0.14f) * (float)dt;
        stickForce += push;
        double threshold = 0.09 + smoothedSize * 0.17 + NextUnit() * 0.055;
        if (stickForce > threshold)
        {
            float amp = Mathf.Clamp(Mathf.Sqrt(lastSlipGap * 5.5f), 0.18f, 0.85f);
            TriggerWoodModal(Mathf.Lerp(110f, 340f, Depth01()), 0.16f, amp);
            stickForce *= 0.18f;
            lastSlipGap = 0f;
        }

        lastSlipGap += (float)dt;
        double rub = GenerateFilteredNoise(0.006f + smoothedEnergy * 0.006f, 0.002f) * (0.08 + stickForce * 1.7);
        return rub + RenderModalVoices(dt) * 0.96;
    }

    double GenerateModalMetal(double dt, bool ice)
    {
        double rate = (ice ? 0.7 : 1.1) + smoothedRate * (ice ? 2.6 : 4.2) + smoothedEnergy * 1.2;
        mainTimer -= dt;
        if (mainTimer <= 0.0)
        {
            float root = smoothedDepth * (ice ? 1.35f : 0.9f) * (0.78f + NextUnitFloat() * 0.42f);
            TriggerMetalModal(root, ice);
            mainTimer += PoissonGap(rate);
        }

        return RenderModalVoices(dt);
    }

    double GenerateWaterBed(double dt, float amount)
    {
        double n = NextBipolar();
        lowNoise += ((float)n - lowNoise) * (0.0025f + smoothedRate * 0.006f);
        slowNoise += (lowNoise - slowNoise) * (0.0007f + smoothedSize * 0.0018f);
        bandA += ((float)n - bandA) * (0.025f + smoothedEnergy * 0.025f);
        bandB += (bandA - bandB) * (0.004f + smoothedSize * 0.006f);
        bandC += ((bandA - bandB) - bandC) * 0.06f;
        return (lowNoise - slowNoise) * 0.16 * amount + bandC * 0.12 * amount;
    }

    double GenerateAirShimmer(double dt)
    {
        return GenerateFilteredNoise(0.018f + smoothedEnergy * 0.012f, 0.004f) * 0.34;
    }

    double GenerateFilteredNoise(float fast, float slow)
    {
        float n = NextBipolar();
        bandA += (n - bandA) * fast;
        bandB += (bandA - bandB) * slow;
        return bandA - bandB;
    }

    void TriggerWaterCavity(float low, float high, float minDur, float maxDur)
    {
        int v = eventVoice++ % EventVoices;
        float sizeSkew = Mathf.Pow(NextUnitFloat(), Mathf.Lerp(2.1f, 0.65f, smoothedEnergy));
        float start = low * Mathf.Pow(Mathf.Max(1.01f, high / low), sizeSkew);

        // Extra frequency scatter: each cavity uses a randomised inharmonic multiple
        // This is the key change that makes bubbles sound less like musical tones —
        // each event lands at a slightly different pitch, no stable harmonic series
        float scatterRatio = 0.82f + NextUnitFloat() * 0.42f;  // ×0.82–1.24 random offset
        start *= scatterRatio;

        eventFreq[v] = start;
        eventTargetFreq[v] = start * (1.12f + NextUnitFloat() * Mathf.Lerp(0.22f, 0.68f, smoothedEnergy));
        eventBend[v] = 0.0014 + smoothedRate * 0.0032 + smoothedEnergy * 0.002;
        eventPhase[v] = NextUnit();
        eventEnv[v] = (0.12f + smoothedEnergy * 0.34f) * (0.6f + NextUnitFloat() * 0.6f);
        eventGain[v] = 0.18f + smoothedEnergy * 0.35f;
        double dur = minDur + NextUnit() * Math.Max(0.002f, maxDur);
        eventDecay[v] = Math.Exp(-1.0 / (sampleRate * dur));

        // Seed the per-voice wobble so each voice starts at a random phase of its noise
        eventWobble[v] = NextUnit() * 2.0 - 1.0;
    }

    void TriggerClickModal(float root, float decay, float spread, float amp)
    {
        float[] ratios = { 1f, 1.71f, 2.42f, 3.18f };
        for (int i = 0; i < ratios.Length; i++)
        {
            int v = modalVoice++ % ModalVoices;
            modalFreq[v] = root * ratios[i] * (1f + (NextUnitFloat() - 0.5f) * spread);
            modalPhase[v] = NextUnit();
            modalEnv[v] = amp / (1f + i * 0.7f);
            modalGain[v] = 0.5f + NextUnitFloat() * 0.5f;
            modalDecay[v] = Math.Exp(-1.0 / (sampleRate * (decay + i * decay * 0.55f)));
        }
    }

    void TriggerMetalModal(float root, bool ice)
    {
        // Both ice and non-ice use 3 partials.
        // Ice had 5 long-decay partials (up to 3.5s each) causing ~18 concurrent
        // sinusoids at steady state — the source of Uranus's grainy, rough texture.
        // 3 partials with max 1.6s decay keeps concurrent voices around 7, which is clean.
        float[] ratios = ice
            ? new[] { 1f, 2.13f, 3.71f }
            : new[] { 1f, 2.01f, 2.89f };
        // Non-ice 0.26, ice 0.22: high Rate/Energy can stack 13+ concurrent voices.
        // Keeping individual amp low lets the tanh stay out of saturation.
        float amp = ice ? 0.22f : 0.26f;
        for (int i = 0; i < ratios.Length; i++)
        {
            int v = modalVoice++ % ModalVoices;
            float spread = ice ? 0.022f : 0.038f;
            modalFreq[v] = root * ratios[i] * (1f + (NextUnitFloat() - 0.5f) * spread);
            modalPhase[v] = NextUnit();
            modalEnv[v] = amp / (1f + i * 0.62f);
            modalGain[v] = Mathf.Lerp(0.5f, 1f, NextUnitFloat());
            double dur = ice
                ? Mathf.Min(1.6f, 1.1f + i * 0.22f + smoothedSize * 0.95f)   // was up to 3.5s
                : (0.38 + i * 0.14 + smoothedSize * 0.55);
            modalDecay[v] = Math.Exp(-1.0 / (sampleRate * dur));
        }
    }

    void TriggerStoneModal(float root, float decay, float amp)
    {
        float[] ratios = { 1f, 1.38f, 1.92f, 2.75f };
        for (int i = 0; i < ratios.Length; i++)
        {
            int v = modalVoice++ % ModalVoices;
            modalFreq[v] = root * ratios[i] * (0.94f + NextUnitFloat() * 0.12f);
            modalPhase[v] = NextUnit();
            modalEnv[v] = amp / (1f + i * 0.85f);
            modalGain[v] = 0.7f;
            modalDecay[v] = Math.Exp(-1.0 / (sampleRate * (decay + smoothedSize * 0.35f + i * 0.12f)));
        }
    }

    void TriggerWoodModal(float root, float decay, float amp)
    {
        float[] ratios = { 1f, 1.64f, 2.21f, 3.05f };
        for (int i = 0; i < ratios.Length; i++)
        {
            int v = modalVoice++ % ModalVoices;
            modalFreq[v] = root * ratios[i] * (0.96f + NextUnitFloat() * 0.08f);
            modalPhase[v] = NextUnit();
            modalEnv[v] = amp / (1f + i * 0.75f);
            modalGain[v] = 0.8f;
            modalDecay[v] = Math.Exp(-1.0 / (sampleRate * (decay + i * 0.11f)));
        }
    }

    double RenderEventVoices(double dt, double bendFloor)
    {
        double sum = 0.0;
        for (int i = 0; i < EventVoices; i++)
        {
            if (eventEnv[i] <= 0.00004)
                continue;

            eventFreq[i] += (eventTargetFreq[i] - eventFreq[i]) * Math.Max(bendFloor, eventBend[i]);

            // Per-voice slow pitch wobble: evolves toward random noise → breaks pure-tone pitch quality
            // Rate constant ~0.0022/sample ≈ 22ms time constant at 44kHz
            eventWobble[i] += ((NextUnit() * 2.0 - 1.0) - eventWobble[i]) * 0.0022;

            // Doppler + wobble applied to instantaneous frequency
            // Wobble depth scales with envelope (loudest moments are most physically energetic)
            double wobbleDepth = 0.045 * eventEnv[i];
            double instFreq = eventFreq[i] * dopplerShift * (1.0 + eventWobble[i] * wobbleDepth);

            eventPhase[i] = Wrap01(eventPhase[i] + instFreq * dt);
            eventEnv[i] *= eventDecay[i];
            double env = eventEnv[i];
            sum += Math.Sin(eventPhase[i] * TwoPi) * env * eventGain[i];
        }
        return sum;
    }

    double RenderModalVoices(double dt)
    {
        double sum = 0.0;
        for (int i = 0; i < ModalVoices; i++)
        {
            if (modalEnv[i] <= 0.00004)
                continue;

            modalPhase[i] = Wrap01(modalPhase[i] + modalFreq[i] * dt);
            modalEnv[i] *= modalDecay[i];
            sum += Math.Sin(modalPhase[i] * TwoPi) * modalEnv[i] * modalGain[i];
        }
        return sum;
    }

    void UpdateOrbitBands()
    {
        float dist = orbit != null ? orbit.dist : transform.localPosition.magnitude;
        float speed = orbit != null ? Mathf.Abs(orbit.baseSpeed) : Mathf.Max(0.1f, lfoRate);
        orbitDistance01 = Mathf.InverseLerp(48f, 284f, dist);
        orbitSpeed01 = Mathf.InverseLerp(0.7f, 8f, speed);
        ComputeDoppler(dist, speed);
    }

    void ComputeDoppler(float orbitDist, float orbitalSpeed)
    {
        if (orbit == null) return;
        if (audioListener == null)
            audioListener = FindAnyObjectByType<AudioListener>()?.transform;
        if (audioListener == null) return;

        // Planet's current world position and tangential velocity direction
        // (perpendicular to radial in XZ plane, in the direction of orbit)
        Vector3 pos   = transform.position;
        Vector3 toSun = pos.magnitude > 0.001f ? -pos.normalized : Vector3.forward;
        // Tangent is 90° CCW of (toward sun), signed by orbit direction
        Vector3 tangent = new Vector3(-toSun.z, 0f, toSun.x) * Mathf.Sign(orbitalSpeed);

        // Tangential speed in world units/sec
        // deg/sec → rad/sec → arc length/sec
        float tangentSpeed = (orbitalSpeed * Mathf.Deg2Rad) * orbitDist;

        // Direction from planet toward listener
        Vector3 toListener = (audioListener.position - pos).normalized;

        // Radial velocity: positive = moving toward listener
        float radialVel = Vector3.Dot(tangent * tangentSpeed, toListener);

        // Wider range than before — flyby passes should feel like a real pitch
        // sweep, not just a subtle wobble.  ±5.5 % is still musical enough to
        // avoid the "repeating pitch ramp" artefact on circular orbits.
        const float c = 260f;
        float targetShift = c / Mathf.Max(0.01f, c - radialVel);
        targetShift = Mathf.Clamp(targetShift, 0.955f, 1.055f);

        // Smooth over ~1 second to avoid sudden pitch jumps
        dopplerShift += (targetShift - dopplerShift) * 0.003f;
    }

    float Depth01()
    {
        return Mathf.InverseLerp(40f, 500f, smoothedDepth);
    }

    double PoissonGap(double rate)
    {
        return -Math.Log(Math.Max(0.000001, 1.0 - NextUnit())) / Math.Max(0.01, rate);
    }

    float BilinearRandom()
    {
        return (NextUnitFloat() - NextUnitFloat()) * 0.5f;
    }

    float HighPassDc(float input)
    {
        float output = input - hpLastInput + 0.9992f * hpLastOutput;
        hpLastInput = input;
        hpLastOutput = output;
        return output;
    }

    float NextUnitFloat()
    {
        return (float)NextUnit();
    }

    float NextBipolar()
    {
        return (float)(NextUnit() * 2.0 - 1.0);
    }

    double NextUnit()
    {
        rngState ^= rngState << 13;
        rngState ^= rngState >> 17;
        rngState ^= rngState << 5;
        return (rngState & 0x00FFFFFF) / 16777216.0;
    }

    static float Wrap01(float value)
    {
        value -= Mathf.Floor(value);
        return value;
    }

    static double Wrap01(double value)
    {
        value -= Math.Floor(value);
        return value;
    }

    // Smooth soft-limiter used as a safety net after spatial processing.
    // Passes signals below 0.88 unaffected; gently rounds anything above.
    static float SoftLimit(float x)
    {
        const float knee = 0.88f;
        if (x >  knee) return  knee + (x - knee)  * 0.10f;
        if (x < -knee) return -knee + (x + knee)  * 0.10f;
        return x;
    }
}
