using UnityEngine;
using System;

[RequireComponent(typeof(AudioSource))]
public class FMSynthesizer : MonoBehaviour
{
    public enum OscType { Sine, Triangle, Sawtooth, Square }

    [Header("Master")]
    public int planetIndex = 1;
    [Range(0f, 1f)]
    public float masterVolume = 1f;
    [Range(0f, 1f)]
    public float volumeScale = 1f;

    [Header("Oscillator")]
    public OscType oscType = OscType.Sine;
    [Range(40f, 500f)]
    public float carrierNote = 100f;

    [Header("FM")]
    [Range(0.5f, 8f)]
    public float modRatio = 2f;
    [Range(0f, 10f)]
    public float modIndex = 0.3f;

    [Header("LFO")]
    [Range(0f, 5f)]
    public float lfoRate = 0f;
    [Range(0f, 0.3f)]
    public float lfoDepth = 0.05f;

    [Header("Pulse")]
    public bool pulseEnabled = false;
    [Range(0.1f, 8f)]
    public float pulseRate = 1f;
    [Range(0.01f, 0.3f)]
    public float pulseDecay = 0.08f;

    private AudioSource audioSource;
    private double cachedSampleRate;
    private double carrierPhase, modPhase, lfoPhase;
    private double pulseTimer = 0;
    private double pulseEnv = 0;
    private float targetMasterVolume = 1f;
    private const float fadeDuration = 0.05f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.spatialize = true;
        
        // 保留我們剛剛設定好的微距都卜勒，帶來自然的空氣推擠感
        audioSource.dopplerLevel = 0.02f; 
        
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 1f;
        audioSource.minDistance = 9999f;
        audioSource.maxDistance = 10000f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        cachedSampleRate = AudioSettings.outputSampleRate;
    }

    public void Init()
    {
        double t = (planetIndex - 1) / 8.0;
        carrierNote = (float)(60.0 + t * 420.0);
        modRatio    = (float)(1.5 + t * 2.5);
        modIndex    = (float)(0.2 + t * 0.6);
        lfoRate     = 0f;
        lfoDepth    = 0.05f;
        oscType     = OscType.Sine;

        lfoPhase     = planetIndex * 0.13;
        carrierPhase = planetIndex * 0.07;
        modPhase     = planetIndex * 0.23;

        audioSource.Play();
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
        audioSource.dopplerLevel = level;
    }

    double GetCarrierSample(double phase)
    {
        switch (oscType)
        {
            case OscType.Triangle: return 1.0 - 4.0 * Math.Abs(phase - 0.5);
            case OscType.Sawtooth: return 2.0 * phase - 1.0;
            case OscType.Square:   return phase < 0.5 ? 1.0 : -1.0;
            default:               return Math.Sin(phase * 2.0 * Math.PI);
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (cachedSampleRate <= 0) return;
        double dt     = 1.0 / cachedSampleRate;
        double twoPi  = 2.0 * Math.PI;

        float fadeStep = (float)(dt / fadeDuration);
        for (int i = 0; i < data.Length; i += channels)
        {
            masterVolume = Mathf.MoveTowards(masterVolume, targetMasterVolume, fadeStep);

            // 1. 產生 LFO 訊號 (-1.0 ~ 1.0)
            double lfo = Math.Sin(lfoPhase * twoPi);
            lfoPhase += lfoRate * dt;
            if (lfoPhase >= 1.0) lfoPhase -= 1.0;

            // 🌟 修正 A：LFO 映射到 Modulation Index (音色掃描)
            // 將 UI 傳來的 0~0.3 Depth，放大 15 倍。
            // 當 Depth 拉滿時，會產生 0~4.5 的巨大 Modulation Index 波動！
            double currentModIndex = modIndex + (lfo * lfoDepth * 15.0);
            if (currentModIndex < 0) currentModIndex = 0;

            double mFreq  = carrierNote * modRatio;
            double mDepth = currentModIndex * carrierNote;

            double mod = Math.Sin(modPhase * twoPi) * mDepth;
            modPhase += mFreq * dt;
            if (modPhase >= 1.0) modPhase -= 1.0;

            // 🌟 修正 B：LFO 映射到 Carrier Pitch (音高顫音)
            // 賦予聲音微小的 Pitch Wobble，打破死板的電子感
            double pitchWobble = 1.0 + (lfo * lfoDepth * 0.05);
            double instFreq = (carrierNote * pitchWobble) + mod;
            if (instFreq < 0) instFreq = 0;

            double carrier = GetCarrierSample(carrierPhase);
            carrierPhase += instFreq * dt;
            if (carrierPhase >= 1.0) carrierPhase -= 1.0;

            // 拔除原本沒用的 LFO 振幅控制，只保留 Pulse Envelope 功能
            double ampMod = 1.0; 
            if (pulseEnabled)
            {
                pulseTimer += dt;
                if (pulseTimer >= 1.0 / pulseRate)
                {
                    pulseTimer = 0;
                    pulseEnv = 1.0;
                }
                pulseEnv *= System.Math.Exp(-dt / pulseDecay);
                ampMod *= pulseEnv;
            }

            float output = (float)(carrier * ampMod * masterVolume * volumeScale * 0.15);

            if (float.IsNaN(output) || float.IsInfinity(output))
                output = 0f;

            for (int c = 0; c < channels; c++)
                data[i + c] = output;
        }
    }
}