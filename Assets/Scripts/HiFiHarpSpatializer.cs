using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class HiFiHarpSpatializer : MonoBehaviour, IPlanetSpatializer
{
    const int MaxKernelSamples = 2048;
    const int RingSize = 4096;

    [Range(0f, 1f)] public float roomAmount = 0.65f;
    [Range(0f, 1f)] public float directAmount = 0.9f;
    [Range(0f, 1f)] public float width = 0.85f;

    float[] wKernel;
    float[] xKernel;
    float[] yKernel;
    float[] zKernel;
    float[] leftKernel;
    float[] rightKernel;
    readonly float[] ring = new float[RingSize];
    int ringIndex;
    Transform listener;
    int lastDirectionBucket = int.MinValue;

    void Awake()
    {
        listener = FindAnyObjectByType<AudioListener>()?.transform;
        LoadOrBuildKernels();
        UpdateStereoKernels(true);
    }

    void Update()
    {
        UpdateStereoKernels(false);
    }

    public void ProcessSample(float mono, out float left, out float right)
    {
        ring[ringIndex] = mono;

        float l = mono * directAmount;
        float r = mono * directAmount;
        var lk = leftKernel;
        var rk = rightKernel;
        int n = lk != null ? lk.Length : 0;

        for (int i = 0; i < n; i++)
        {
            int idx = ringIndex - i;
            if (idx < 0) idx += RingSize;
            float s = ring[idx];
            l += s * lk[i] * roomAmount;
            r += s * rk[i] * roomAmount;
        }

        ringIndex++;
        if (ringIndex >= RingSize) ringIndex = 0;

        left = Mathf.Clamp(l, -1f, 1f);
        right = Mathf.Clamp(r, -1f, 1f);
    }

    void LoadOrBuildKernels()
    {
        string root = Path.Combine(Application.streamingAssetsPath, "HiFiHARP");
        string wav = SpatialAudioWav.FindFirstWav(root);
        if (!string.IsNullOrEmpty(wav) && SpatialAudioWav.TryRead(wav, out var audio) && audio.channels >= 4)
        {
            BuildFromFoaWav(audio);
            Debug.Log("HiFi-HARP spatializer loaded FOA RIR: " + wav);
            return;
        }

        BuildFallbackFoaRoom();
        Debug.Log("HiFi-HARP spatializer using built-in FOA fallback. Put FOA wav files under StreamingAssets/HiFiHARP to use the dataset.");
    }

    void BuildFromFoaWav(SpatialAudioWav.AudioData audio)
    {
        int outputRate = Mathf.Max(1, AudioSettings.outputSampleRate);
        int frames = audio.samples.Length / audio.channels;
        int length = Mathf.Min(MaxKernelSamples, Mathf.CeilToInt(frames * outputRate / (float)audio.sampleRate));
        wKernel = new float[length];
        xKernel = new float[length];
        yKernel = new float[length];
        zKernel = new float[length];

        float step = audio.sampleRate / (float)outputRate;
        for (int i = 0; i < length; i++)
        {
            float sourceIndex = i * step;
            wKernel[i] = ReadChannelLinear(audio, 0, sourceIndex);
            yKernel[i] = ReadChannelLinear(audio, 1, sourceIndex);
            zKernel[i] = ReadChannelLinear(audio, 2, sourceIndex);
            xKernel[i] = ReadChannelLinear(audio, 3, sourceIndex);
        }

        NormalizeKernels();
    }

    void BuildFallbackFoaRoom()
    {
        int sr = Mathf.Max(1, AudioSettings.outputSampleRate);
        int length = Mathf.Min(MaxKernelSamples, Mathf.RoundToInt(sr * 0.16f));
        wKernel = new float[length];
        xKernel = new float[length];
        yKernel = new float[length];
        zKernel = new float[length];

        AddImpulse(wKernel, 0, 0.42f);
        AddImpulse(xKernel, Mathf.RoundToInt(sr * 0.002f), 0.18f);
        AddImpulse(yKernel, Mathf.RoundToInt(sr * 0.0035f), 0.14f);
        AddImpulse(wKernel, Mathf.RoundToInt(sr * 0.019f), 0.24f);
        AddImpulse(xKernel, Mathf.RoundToInt(sr * 0.027f), -0.16f);
        AddImpulse(yKernel, Mathf.RoundToInt(sr * 0.041f), 0.13f);

        for (int i = Mathf.RoundToInt(sr * 0.052f); i < length; i++)
        {
            float t = i / (float)sr;
            float decay = Mathf.Exp(-t * 11f) * 0.08f;
            wKernel[i] += Mathf.Sin(i * 0.071f) * decay;
            xKernel[i] += Mathf.Sin(i * 0.049f + 1.7f) * decay * 0.45f;
            yKernel[i] += Mathf.Sin(i * 0.061f + 0.9f) * decay * 0.45f;
        }
    }

    void UpdateStereoKernels(bool force)
    {
        Vector3 local = GetListenerRelativeDirection();
        int bucket = Mathf.RoundToInt(Mathf.Atan2(local.x, local.z) * 16f);
        if (!force && bucket == lastDirectionBucket)
            return;

        lastDirectionBucket = bucket;
        int n = wKernel.Length;
        var l = new float[n];
        var r = new float[n];

        float front = Mathf.Clamp(local.z, -1f, 1f);
        float side = Mathf.Clamp(local.x, -1f, 1f) * width;
        float height = Mathf.Clamp(local.y, -1f, 1f) * 0.35f;

        for (int i = 0; i < n; i++)
        {
            float omni = wKernel[i] * 0.7071f;
            float frontBack = xKernel[i] * front;
            float sideEnergy = yKernel[i] * side;
            float heightEnergy = zKernel[i] * height;
            l[i] = omni + frontBack + sideEnergy + heightEnergy;
            r[i] = omni + frontBack - sideEnergy + heightEnergy;
        }

        leftKernel = l;
        rightKernel = r;
    }

    Vector3 GetListenerRelativeDirection()
    {
        if (listener == null)
            listener = FindAnyObjectByType<AudioListener>()?.transform;

        if (listener == null)
            return transform.position.normalized;

        Vector3 world = transform.position - listener.position;
        if (world.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        return listener.InverseTransformDirection(world.normalized);
    }

    static float ReadChannelLinear(SpatialAudioWav.AudioData audio, int channel, float sampleIndex)
    {
        int frames = audio.samples.Length / audio.channels;
        int i0 = Mathf.Clamp((int)sampleIndex, 0, frames - 1);
        int i1 = Mathf.Min(i0 + 1, frames - 1);
        float t = sampleIndex - i0;
        float a = audio.samples[i0 * audio.channels + channel];
        float b = audio.samples[i1 * audio.channels + channel];
        return Mathf.Lerp(a, b, t);
    }

    void NormalizeKernels()
    {
        float peak = 0f;
        for (int i = 0; i < wKernel.Length; i++)
        {
            peak = Mathf.Max(peak, Mathf.Abs(wKernel[i]));
            peak = Mathf.Max(peak, Mathf.Abs(xKernel[i]));
            peak = Mathf.Max(peak, Mathf.Abs(yKernel[i]));
            peak = Mathf.Max(peak, Mathf.Abs(zKernel[i]));
        }

        if (peak < 0.0001f)
            return;

        float gain = 0.38f / peak;
        Scale(wKernel, gain);
        Scale(xKernel, gain);
        Scale(yKernel, gain);
        Scale(zKernel, gain);
    }

    static void AddImpulse(float[] kernel, int index, float value)
    {
        if (index >= 0 && index < kernel.Length)
            kernel[index] += value;
    }

    static void Scale(float[] values, float gain)
    {
        for (int i = 0; i < values.Length; i++)
            values[i] *= gain;
    }
}
