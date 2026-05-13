using UnityEngine;
using System;

[RequireComponent(typeof(AudioSource))]
public class FMSynthesizer : MonoBehaviour
{
    public enum OscType { Sine, Triangle, Sawtooth, Square }
    enum SoundscapeLayer
    {
        PebbleRun,
        WarmBrook,
        EarthCreek,
        CaveDrops,
        WideRiver,
        RingSpring,
        GlassRill,
        DeepCurrent,
        MistRain
    }

    const int CavityVoices = 24;
    const double TwoPi = Math.PI * 2.0;

    [Header("Master")]
    public int planetIndex = 1;
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 2.5f)] public float volumeScale = 1f;

    [Header("Oscillator")]
    public OscType oscType = OscType.Sine;
    [Range(40f, 500f)] public float carrierNote = 100f;

    [Header("FM")]
    [Range(0.5f, 8f)] public float modRatio = 2f;
    [Range(0f, 10f)] public float modIndex = 0.3f;

    [Header("LFO")]
    [Range(0f, 5f)] public float lfoRate = 0f;
    [Range(0f, 0.3f)] public float lfoDepth = 0.05f;

    [Header("Pulse")]
    public bool pulseEnabled = false;
    [Range(0.1f, 8f)] public float pulseRate = 1f;
    [Range(0.01f, 0.3f)] public float pulseDecay = 0.08f;

    AudioSource audioSource;
    IPlanetSpatializer spatializer;
    PlanetOrbit orbit;
    SoundscapeLayer layer;

    double sampleRate;
    double carrierPhase;
    double modPhase;
    double subPhase;
    double lfoPhase;
    double pulseTimer;
    double pulseEnv = 1.0;
    double whalePhase;
    double whaleTimer;
    double whaleEnv;
    double whaleFreq;
    double whaleTargetFreq;
    double dripPhase;
    double dripTimer;
    double dripEnv;
    double dripFreq;
    double chimePhase;
    double chimeTimer;
    double chimeEnv;
    double chimeFreq;
    double birdPhase;
    double birdTimer;
    double birdEnv;
    double birdFreq;
    double padPhaseA;
    double padPhaseB;
    double padPhaseC;
    double crackleTimer;
    double hydroCrackleEnv;
    double hydroCrackleColor;

    readonly double[] cavityPhase = new double[CavityVoices];
    readonly double[] cavityFreq = new double[CavityVoices];
    readonly double[] cavityTargetFreq = new double[CavityVoices];
    readonly double[] cavityEnv = new double[CavityVoices];
    readonly double[] cavityDecay = new double[CavityVoices];
    int cavityVoice;

    uint rngState = 1u;
    float targetMasterVolume = 1f;
    float smoothedVolume;
    float smoothedCarrier;
    float smoothedModRatio;
    float smoothedModIndex;
    float smoothedLfoRate;
    float smoothedLfoDepth;
    float brown;
    float waterLow;
    float waterBand;
    float waterBandSlow;
    float waterLfoPhase;
    float waterLfoValue;
    float pinkB0;
    float pinkB1;
    float pinkB2;
    float deepBand;
    float deepBandSlow;
    float fizzBand;
    float fizzSlow;
    float windBand;
    float windSlow;
    float outputSmooth;
    float hpLastInput;
    float hpLastOutput;
    float streamDensity;
    float streamBrightness;
    float streamGain;
    float cavityGain;
    float deepGain;
    float dripGain;
    float bodyGain;
    float chimeGain;
    float birdGain;
    float resonatorSoftness;
    float cavityLow;
    float cavityHigh;
    float cavityDecayBase;
    float targetDelta = 0.5f;
    float targetTheta = 0.5f;
    float targetAlpha = 0.35f;
    float targetBeta = 0.45f;
    float deltaBand = 0.5f;
    float thetaBand = 0.5f;
    float alphaBand = 0.35f;
    float betaBand = 0.45f;

    static readonly SoundscapeLayer[] PlanetLayers =
    {
        SoundscapeLayer.PebbleRun,
        SoundscapeLayer.WarmBrook,
        SoundscapeLayer.EarthCreek,
        SoundscapeLayer.CaveDrops,
        SoundscapeLayer.WideRiver,
        SoundscapeLayer.RingSpring,
        SoundscapeLayer.GlassRill,
        SoundscapeLayer.DeepCurrent,
        SoundscapeLayer.MistRain
    };

    static readonly float[] PlanetScale =
    {
        176.00f, 98.00f, 132.00f, 220.00f, 61.74f,
        146.83f, 246.94f, 49.00f, 196.00f
    };

    static readonly float[] PlanetModRatios =
    {
        2.6f, 1.1f, 1.7f, 3.2f, 0.72f,
        2.1f, 3.35f, 0.62f, 2.8f
    };

    static readonly float[] PlanetModIndexes =
    {
        1.45f, 0.46f, 0.86f, 1.25f, 0.38f,
        0.72f, 0.58f, 0.28f, 0.96f
    };

    static readonly float[] PlanetLfoRates =
    {
        0.9f, 0.12f, 0.38f, 0.21f, 0.055f,
        0.28f, 0.16f, 0.024f, 0.52f
    };

    static readonly float[] PlanetLfoDepths =
    {
        0.08f, 0.025f, 0.055f, 0.035f, 0.02f,
        0.04f, 0.03f, 0.016f, 0.045f
    };

    static readonly bool[] PlanetPulse =
    {
        false, false, false, false, false, false, false, false, false
    };

    static readonly float[] PlanetPulseRates =
    {
        3.5f, 0.8f, 0.9f, 1.6f, 0.4f, 0.9f, 0.5f, 0.35f, 2.5f
    };

    static readonly float[] PlanetPulseDecays =
    {
        0.08f, 0.18f, 0.16f, 0.08f, 0.25f, 0.14f, 0.2f, 0.26f, 0.06f
    };

    static readonly float[] PlanetVolumes =
    {
        0.88f, 0.95f, 0.82f, 0.78f, 1.08f,
        0.86f, 0.8f, 1.12f, 1.18f
    };

    static readonly OscType[] PlanetOscTypes =
    {
        OscType.Sine, OscType.Sine, OscType.Triangle, OscType.Sine, OscType.Sine,
        OscType.Triangle, OscType.Sine, OscType.Sine, OscType.Sine
    };

    static readonly float[] PlanetStreamDensity =
    {
        0.92f, 0.22f, 0.86f, 0.18f, 0.16f,
        0.52f, 0.34f, 0.2f, 0.42f
    };

    static readonly float[] PlanetStreamBrightness =
    {
        0.92f, 0.2f, 0.56f, 0.32f, 0.12f,
        0.62f, 0.78f, 0.16f, 0.7f
    };

    static readonly float[] PlanetStreamGain =
    {
        0.9f, 0.22f, 1.08f, 0.18f, 0.18f,
        0.48f, 0.26f, 0.22f, 0.42f
    };

    static readonly float[] PlanetCavityGain =
    {
        1.28f, 0.28f, 1.34f, 0.62f, 0.22f,
        0.84f, 0.98f, 0.38f, 0.72f
    };

    static readonly float[] PlanetDeepGain =
    {
        0.04f, 0.44f, 0.18f, 0.08f, 1.08f,
        0.18f, 0.06f, 1.16f, 0.08f
    };

    static readonly float[] PlanetDripGain =
    {
        0.12f, 0.02f, 0.16f, 1.12f, 0.03f,
        0.62f, 0.08f, 0.04f, 0.68f
    };

    static readonly float[] PlanetBodyGain =
    {
        0.0f, 0.08f, 0.018f, 0.0f, 0.12f,
        0.02f, 0.0f, 0.08f, 0.0f
    };

    static readonly float[] PlanetChimeGain =
    {
        0.04f, 0.18f, 0.04f, 0.02f, 0.0f,
        0.42f, 0.64f, 0.0f, 0.24f
    };

    static readonly float[] PlanetBirdGain =
    {
        0.0f, 0.05f, 0.1f, 0.0f, 0.0f,
        0.035f, 0.0f, 0.0f, 0.02f
    };

    static readonly float[] PlanetWhaleGain =
    {
        0.0f, 0.22f, 0.03f, 0.0f, 0.56f,
        0.06f, 0.0f, 0.62f, 0.0f
    };

    static readonly float[] PlanetResonatorSoftness =
    {
        0.72f, 0.98f, 0.82f, 0.62f, 0.99f,
        0.76f, 0.48f, 0.99f, 0.7f
    };

    static readonly float[] PlanetCavityLow =
    {
        720f, 180f, 260f, 900f, 120f,
        420f, 1020f, 140f, 760f
    };

    static readonly float[] PlanetCavityHigh =
    {
        5600f, 900f, 3200f, 2900f, 700f,
        4300f, 6800f, 850f, 5000f
    };

    static readonly float[] PlanetCavityDecay =
    {
        0.035f, 0.11f, 0.075f, 0.16f, 0.18f,
        0.095f, 0.05f, 0.22f, 0.045f
    };

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
        int idx = Mathf.Clamp(planetIndex - 1, 0, PlanetScale.Length - 1);
        layer = PlanetLayers[idx];
        carrierNote = PlanetScale[idx];
        modRatio = PlanetModRatios[idx];
        modIndex = PlanetModIndexes[idx];
        lfoRate = PlanetLfoRates[idx];
        lfoDepth = PlanetLfoDepths[idx];
        pulseEnabled = PlanetPulse[idx];
        pulseRate = PlanetPulseRates[idx];
        pulseDecay = PlanetPulseDecays[idx];
        volumeScale = PlanetVolumes[idx];
        oscType = PlanetOscTypes[idx];
        streamDensity = PlanetStreamDensity[idx];
        streamBrightness = PlanetStreamBrightness[idx];
        streamGain = PlanetStreamGain[idx];
        cavityGain = PlanetCavityGain[idx];
        deepGain = PlanetDeepGain[idx];
        dripGain = PlanetDripGain[idx];
        bodyGain = PlanetBodyGain[idx];
        chimeGain = PlanetChimeGain[idx];
        birdGain = PlanetBirdGain[idx];
        resonatorSoftness = PlanetResonatorSoftness[idx];
        cavityLow = PlanetCavityLow[idx];
        cavityHigh = PlanetCavityHigh[idx];
        cavityDecayBase = PlanetCavityDecay[idx];

        rngState = 0x9E3779B9u + (uint)planetIndex * 747796405u;
        carrierPhase = planetIndex * 0.071;
        modPhase = planetIndex * 0.173;
        subPhase = planetIndex * 0.113;
        lfoPhase = planetIndex * 0.137;
        padPhaseA = planetIndex * 0.061;
        padPhaseB = planetIndex * 0.149;
        padPhaseC = planetIndex * 0.233;
        waterLfoPhase = planetIndex * 0.097f;
        waterLfoValue = 0.5f;
        smoothedVolume = masterVolume;
        smoothedCarrier = carrierNote;
        smoothedModRatio = modRatio;
        smoothedModIndex = modIndex;
        smoothedLfoRate = lfoRate;
        smoothedLfoDepth = lfoDepth;
        UpdateNeuralBands();
        deltaBand = targetDelta;
        thetaBand = targetTheta;
        alphaBand = targetAlpha;
        betaBand = targetBeta;

        whaleTimer = 2.0 + NextUnit() * 8.0;
        dripTimer = 0.5 + NextUnit() * 2.5;
        chimeTimer = 2.0 + NextUnit() * 4.0;
        birdTimer = 2.0 + NextUnit() * 5.0;
        crackleTimer = 0.05 + NextUnit() * 0.2;

        audioSource.Play();
    }

    void Update()
    {
        UpdateNeuralBands();
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
        float smooth = 1f - Mathf.Exp(-1f / ((float)sampleRate * 0.04f));

        for (int i = 0; i < data.Length; i += channels)
        {
            masterVolume = Mathf.MoveTowards(masterVolume, targetMasterVolume, (float)(dt / 0.2));
            smoothedVolume = Mathf.Lerp(smoothedVolume, masterVolume * volumeScale, smooth);
            smoothedCarrier = Mathf.Lerp(smoothedCarrier, carrierNote, smooth);
            smoothedModRatio = Mathf.Lerp(smoothedModRatio, modRatio, smooth);
            smoothedModIndex = Mathf.Lerp(smoothedModIndex, modIndex, smooth);
            smoothedLfoRate = Mathf.Lerp(smoothedLfoRate, lfoRate, smooth);
            smoothedLfoDepth = Mathf.Lerp(smoothedLfoDepth, lfoDepth, smooth);
            deltaBand = Mathf.Lerp(deltaBand, targetDelta, smooth * 0.45f);
            thetaBand = Mathf.Lerp(thetaBand, targetTheta, smooth * 0.45f);
            alphaBand = Mathf.Lerp(alphaBand, targetAlpha, smooth * 0.45f);
            betaBand = Mathf.Lerp(betaBand, targetBeta, smooth * 0.45f);

            double lfo = Math.Sin(lfoPhase * TwoPi);
            lfoPhase = Wrap01(lfoPhase + smoothedLfoRate * dt);
            float agitation = Mathf.Clamp01(betaBand * 0.68f + (1f - alphaBand) * 0.22f + thetaBand * 0.1f);

            double sample = GenerateLayerSample(dt, lfo, agitation);

            if (pulseEnabled)
            {
                pulseTimer += dt;
                if (pulseTimer >= 1.0 / Math.Max(0.1f, pulseRate))
                {
                    pulseTimer = 0.0;
                    pulseEnv = 1.0;
                }
                pulseEnv *= Math.Exp(-dt / Math.Max(0.01f, pulseDecay));
                sample *= 0.45 + pulseEnv * 0.55;
            }

            float output = (float)(Math.Tanh(sample * 1.55) * smoothedVolume * 0.28);
            output = HighPassDc(output);
            outputSmooth += (output - outputSmooth) * 0.72f;
            output = outputSmooth;

            if (float.IsNaN(output) || float.IsInfinity(output))
                output = 0f;

            if (spatializer != null && channels >= 2)
            {
                spatializer.ProcessSample(output, out float left, out float right);
                data[i] = left;
                data[i + 1] = right;
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

    double GenerateLayerSample(double dt, double lfo, float agitation)
    {
        int idx = Mathf.Clamp(planetIndex - 1, 0, PlanetWhaleGain.Length - 1);
        double stream = GenerateWaterStream(dt, betaBand, thetaBand, agitation) * streamGain;
        double deep = GenerateDeepWater(deltaBand, agitation) * deepGain;
        double drips = GenerateDrips(dt, thetaBand, agitation) * dripGain;
        double body = GenerateBodyResonance(dt, lfo, thetaBand) * bodyGain;
        double chimes = GenerateChimes(dt, alphaBand) * chimeGain;
        double birds = GenerateBirds(dt, betaBand) * birdGain;
        double whale = GenerateWhale(dt, deltaBand) * PlanetWhaleGain[idx];
        double wind = GenerateSoftWind(deltaBand, agitation);

        switch (layer)
        {
            case SoundscapeLayer.PebbleRun:
                return stream * 1.05 + drips * 0.18 + deep * 0.02 + chimes * 0.08;
            case SoundscapeLayer.WarmBrook:
                return body * 1.15 + deep * 0.42 + whale * 0.3 + chimes * 0.22 + stream * 0.18;
            case SoundscapeLayer.EarthCreek:
                return stream * 1.18 + drips * 0.3 + deep * 0.16 + body * 0.28 + birds * 0.1;
            case SoundscapeLayer.CaveDrops:
                return drips * 1.42 + stream * 0.16 + deep * 0.06 + chimes * 0.08;
            case SoundscapeLayer.WideRiver:
                return deep * 1.02 + body * 1.18 + whale * 0.72 + stream * 0.16;
            case SoundscapeLayer.RingSpring:
                return chimes * 0.98 + drips * 0.66 + stream * 0.34 + deep * 0.08;
            case SoundscapeLayer.GlassRill:
                return chimes * 1.24 + stream * 0.18 + drips * 0.12 + deep * 0.03;
            case SoundscapeLayer.DeepCurrent:
                return deep * 1.12 + body * 0.74 + whale * 0.82 + stream * 0.18 + wind * 0.03;
            case SoundscapeLayer.MistRain:
                return drips * 1.02 + chimes * 0.34 + stream * 0.42 + deep * 0.04;
            default:
                return stream + deep + drips;
        }
    }

    double GenerateBodyResonance(double dt, double lfo, float theta)
    {
        double root = Mathf.Clamp(smoothedCarrier * 0.5f, 38f, 180f);
        double breath = 0.48 + theta * 0.42 + 0.1 * Math.Sin((lfoPhase * 0.27 + planetIndex * 0.041) * TwoPi);
        double vibrato = Math.Pow(2.0, (lfo * smoothedLfoDepth * 5.0) / 1200.0);

        padPhaseA = Wrap01(padPhaseA + root * vibrato * dt);
        padPhaseB = Wrap01(padPhaseB + root * 1.498 * dt);
        padPhaseC = Wrap01(padPhaseC + root * 2.247 * dt);

        double tone = Math.Sin(padPhaseA * TwoPi) * 0.54;
        tone += Math.Sin(padPhaseB * TwoPi) * 0.28;
        tone += Math.Sin(padPhaseC * TwoPi) * 0.18;
        return tone * breath * 0.34;
    }

    double GenerateDeepWater(float delta, float agitation)
    {
        float white = NextBipolar();
        brown += (white - brown) * (0.0018f + delta * 0.0042f);
        waterLow += (brown - waterLow) * (0.0009f + agitation * 0.0015f + delta * 0.0018f);

        float rippleWhite = NextBipolar();
        deepBand += (rippleWhite - deepBand) * (0.008f + delta * 0.018f + agitation * 0.01f);
        deepBandSlow += (deepBand - deepBandSlow) * (0.002f + agitation * 0.003f);
        float slowPressure = waterLow * (0.18f + delta * 0.16f + agitation * 0.05f);
        float audibleCurrent = (deepBand - deepBandSlow) * (0.03f + delta * 0.07f + deepGain * 0.04f);
        return slowPressure + audibleCurrent;
    }

    double GenerateSoftWind(float delta, float agitation)
    {
        float white = NextBipolar();
        windBand += (white - windBand) * (0.006f + delta * 0.022f);
        windSlow += (windBand - windSlow) * (0.0015f + agitation * 0.002f);
        return (windBand - windSlow) * (0.016f + delta * 0.038f);
    }

    double GenerateWaterStream(double dt, float beta, float theta, float agitation)
    {
        float white = NextBipolar();
        pinkB0 = 0.99765f * pinkB0 + white * 0.099046f;
        pinkB1 = 0.96300f * pinkB1 + white * 0.296516f;
        pinkB2 = 0.57000f * pinkB2 + white * 1.052691f;
        float pink = (pinkB0 + pinkB1 + pinkB2 + white * 0.1848f) * 0.026f;

        waterLfoPhase += (0.35f + beta * 3.2f + theta * 0.8f) * (float)dt;
        if (waterLfoPhase >= 1f)
            waterLfoPhase -= Mathf.Floor(waterLfoPhase);
        waterLfoValue = 0.5f + 0.5f * Mathf.Sin(waterLfoPhase * Mathf.PI * 2f);

        float fast = 0.026f + beta * 0.095f + theta * 0.025f + waterLfoValue * 0.035f;
        float slow = 0.0014f + beta * 0.0055f + theta * 0.003f + (1f - waterLfoValue) * 0.002f;
        waterBand += (pink - waterBand) * fast;
        waterBandSlow += (waterBand - waterBandSlow) * slow;
        fizzBand += (white - fizzBand) * (0.045f + beta * 0.075f);
        fizzSlow += (fizzBand - fizzSlow) * (0.012f + beta * 0.018f);

        double bandpassBody = (waterBand - waterBandSlow) * (0.03 + beta * 0.065 + theta * 0.018);
        double surfaceFizz = (fizzBand - fizzSlow) * (0.0008 + beta * 0.002) * Mathf.Lerp(0.24f, 0.54f, streamDensity);
        hydroCrackleColor += (white - hydroCrackleColor) * 0.48;
        hydroCrackleEnv *= Math.Exp(-dt / 0.018);
        double crackle = hydroCrackleColor * hydroCrackleEnv * (0.004 + beta * 0.013 + theta * 0.006);
        double bed = bandpassBody + surfaceFizz + crackle;

        crackleTimer -= dt;
        if (crackleTimer <= 0.0)
        {
            TriggerCavity(beta, theta);
            int cavityCount = Math.Max(1, Mathf.FloorToInt(1f + beta * 4f + theta * 2f));
            for (int i = 1; i < cavityCount; i++)
                TriggerCavity(beta, theta);
            if (NextUnit() > 0.52 + resonatorSoftness * 0.18f)
                hydroCrackleEnv = Math.Max(hydroCrackleEnv, (0.01 + beta * 0.04 + theta * 0.018) * 3.2);

            double minGap = Math.Max(0.006, 0.22 - beta * 0.12);
            double maxGap = minGap + 0.14 + (1.0 - beta) * 0.09;
            crackleTimer = minGap + NextUnit() * Math.Max(0.02, maxGap);
        }

        double cavities = 0.0;
        for (int i = 0; i < CavityVoices; i++)
        {
            if (cavityEnv[i] <= 0.0001)
                continue;

            cavityFreq[i] += (cavityTargetFreq[i] - cavityFreq[i]) * (0.0015 + streamBrightness * 0.0032);
            cavityPhase[i] = Wrap01(cavityPhase[i] + cavityFreq[i] * dt);
            cavityEnv[i] *= cavityDecay[i];
            cavities += Math.Sin(cavityPhase[i] * TwoPi) * cavityEnv[i];
        }

        double cavityTone = Mathf.Lerp(0.56f, 1.08f, 1f - resonatorSoftness);
        return bed + cavities * cavityGain * cavityTone;
    }

    void TriggerCavity(float beta, float theta)
    {
        int voice = cavityVoice++ % CavityVoices;
        double low = Math.Max(120.0, cavityLow * 0.45);
        double high = Math.Max(low + 100.0, cavityHigh * 0.58 + beta * 1200.0);
        double skew = Math.Pow(NextUnit(), 1.75);
        double start = low * Math.Pow(high / low, skew);

        cavityFreq[voice] = start;
        cavityTargetFreq[voice] = start * (1.08 + NextUnit() * 0.28);
        cavityEnv[voice] = (0.018 + beta * 0.052 + theta * 0.03) * Mathf.Lerp(0.78f, 1.16f, 1f - resonatorSoftness);
        double duration = 0.055 + NextUnit() * 0.16 + theta * 0.055;
        cavityDecay[voice] = Math.Exp(-1.0 / (sampleRate * duration));
    }

    double GenerateWhale(double dt, float delta)
    {
        whaleTimer -= dt;
        if (whaleTimer <= 0.0)
        {
            double[] notes = { 130.81, 146.83, 110.0, 164.81 };
            whaleFreq = notes[(int)(NextUnit() * notes.Length) % notes.Length] * 0.5;
            whaleTargetFreq = whaleFreq * (0.78 + NextUnit() * 0.08);
            whaleEnv = 1.0;
            whaleTimer = 12.0 - delta * 6.0 + NextUnit() * 6.0;
        }

        if (whaleEnv <= 0.0001)
            return 0.0;

        whaleFreq += (whaleTargetFreq - whaleFreq) * 0.00016;
        whalePhase = Wrap01(whalePhase + whaleFreq * dt);
        whaleEnv *= Math.Exp(-dt / 2.8);
        double tone = Math.Sin(whalePhase * TwoPi);
        tone += Math.Sin(whalePhase * TwoPi * 0.5 + 0.6) * 0.28;
        return tone * whaleEnv * (0.28 + delta * 0.52);
    }

    double GenerateDrips(double dt, float theta, float agitation)
    {
        dripTimer -= dt;
        if (dripTimer <= 0.0)
        {
            dripFreq = 700.0 + theta * 900.0 + NextUnit() * 500.0;
            dripEnv = 1.0;
            dripTimer = Math.Max(0.45, 3.2 - theta * 1.8) + NextUnit();
        }

        if (dripEnv <= 0.0001)
            return 0.0;

        dripPhase = Wrap01(dripPhase + dripFreq * dt);
        dripFreq *= 0.99972 - streamBrightness * 0.00006;
        dripEnv *= Math.Exp(-dt / (0.055 + agitation * 0.025));
        double membrane = Math.Sin(dripPhase * TwoPi);
        membrane += Math.Sin(dripPhase * TwoPi * 1.64) * 0.18;
        return membrane * dripEnv * (0.16 + theta * 0.5);
    }

    double GenerateChimes(double dt, float alpha)
    {
        chimeTimer -= dt;
        if (alpha > 0.24f && chimeTimer <= 0.0)
        {
            double[] scale = { 523.25, 587.33, 659.25, 783.99, 880.0, 987.77 };
            chimeFreq = scale[(int)(NextUnit() * scale.Length) % scale.Length];
            chimeEnv = 1.0;
            chimeTimer = 2.6 - alpha * 1.2 + NextUnit() * 0.5;
        }

        if (chimeEnv <= 0.0001)
            return 0.0;

        chimePhase = Wrap01(chimePhase + chimeFreq * dt);
        chimeFreq *= 0.99993;
        chimeEnv *= Math.Exp(-dt / (0.42 + cavityDecayBase * 0.6));
        double mod = Math.Sin(chimePhase * TwoPi * 3.01) * (0.24 + alpha * 0.22);
        double shimmer = Math.Sin(chimePhase * TwoPi + mod) * 0.74
            + Math.Sin(chimePhase * TwoPi * 2.01 + mod * 0.35) * 0.18
            + Math.Sin(chimePhase * TwoPi * 3.02) * 0.08;
        return shimmer * chimeEnv * (0.13 + alpha * 0.46);
    }

    double GenerateBirds(double dt, float beta)
    {
        birdTimer -= dt;
        if (beta > 0.32f && birdTimer <= 0.0)
        {
            birdFreq = 2400.0 + beta * 1800.0 + NextUnit() * 1000.0;
            birdEnv = 1.0;
            birdTimer = 4.0 - beta * 2.4 + NextUnit() * 2.0;
        }

        if (birdEnv <= 0.0001)
            return 0.0;

        birdPhase = Wrap01(birdPhase + birdFreq * dt);
        birdFreq *= 1.00022;
        birdEnv *= Math.Exp(-dt / 0.06);
        return Math.Sin(birdPhase * TwoPi) * birdEnv * (0.08 + beta * 0.28);
    }

    double ShapeByOscType(double tone, double phase)
    {
        switch (oscType)
        {
            case OscType.Triangle:
                return tone * 0.88 + (1.0 - 4.0 * Math.Abs(phase - 0.5)) * 0.12;
            case OscType.Sawtooth:
                return tone * 0.92 + (2.0 * phase - 1.0) * 0.08;
            case OscType.Square:
                return tone * 0.94 + Math.Tanh(Math.Sin(phase * TwoPi) * 3.0) * 0.06;
            default:
                return tone;
        }
    }

    float HighPassDc(float input)
    {
        float output = input - hpLastInput + 0.9992f * hpLastOutput;
        hpLastInput = input;
        hpLastOutput = output;
        return output;
    }

    float NextBipolar()
    {
        return (float)(NextUnit() * 2.0 - 1.0);
    }

    void UpdateNeuralBands()
    {
        int idx = Mathf.Clamp(planetIndex - 1, 0, PlanetStreamDensity.Length - 1);
        float dist = orbit != null ? orbit.dist : transform.localPosition.magnitude;
        float speed = orbit != null ? Mathf.Abs(orbit.baseSpeed) : Mathf.Max(0.1f, lfoRate);
        float radiusNow = orbit != null
            ? Mathf.Max(1f, transform.localPosition.magnitude)
            : Mathf.Max(1f, dist);
        float dist01 = Mathf.InverseLerp(48f, 284f, dist);
        float speed01 = Mathf.InverseLerp(0.7f, 8f, speed);
        float perihelionLift = Mathf.Clamp01((dist / radiusNow - 0.68f) * 0.8f);
        float orbitBreath = 0.5f + 0.5f * Mathf.Sin((float)(lfoPhase * TwoPi) + idx * 0.73f);

        float delta = 0.18f + dist01 * 0.52f + PlanetDeepGain[idx] * 0.18f;
        float theta = 0.22f + (1f - speed01) * 0.24f + PlanetDripGain[idx] * 0.22f + PlanetBodyGain[idx] * 2.2f;
        float alpha = 0.16f + PlanetChimeGain[idx] * 1.7f + orbitBreath * 0.16f + (1f - dist01) * 0.08f;
        float beta = 0.2f + speed01 * 0.3f + PlanetStreamDensity[idx] * 0.28f
            + PlanetStreamBrightness[idx] * 0.12f + perihelionLift * 0.12f;

        targetDelta = ShapeBand(delta);
        targetTheta = ShapeBand(theta);
        targetAlpha = ShapeBand(alpha);
        targetBeta = ShapeBand(beta);
    }

    static float ShapeBand(float value)
    {
        return Mathf.Pow(Mathf.Clamp01(value), 0.75f);
    }

    double NextUnit()
    {
        rngState ^= rngState << 13;
        rngState ^= rngState >> 17;
        rngState ^= rngState << 5;
        return (rngState & 0x00FFFFFF) / 16777216.0;
    }

    static double Wrap01(double value)
    {
        value -= Math.Floor(value);
        return value;
    }
}
