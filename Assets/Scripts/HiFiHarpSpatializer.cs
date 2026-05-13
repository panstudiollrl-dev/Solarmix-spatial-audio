using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class HiFiHarpSpatializer : MonoBehaviour, IPlanetSpatializer
{
    const int MaxKernelSamples = 1536;
    const int MaxSparseTaps = 96;
    const int RingSize = 4096;

    struct Tap
    {
        public int delay;
        public float omni;
        public float front;
        public float side;
        public float height;
    }

    struct KernelSet
    {
        public float[] w;
        public float[] x;
        public float[] y;
        public float[] z;
    }

    [Range(0f, 1f)] public float roomAmount = 0.76f;
    [Range(0f, 1f)] public float directAmount = 0.72f;
    [Range(0f, 1f)] public float width = 1f;
    public int rirIndex;

    float[] wKernel;
    float[] xKernel;
    float[] yKernel;
    float[] zKernel;
    KernelSet[] rirKernels = Array.Empty<KernelSet>();
    Tap[] taps = Array.Empty<Tap>();
    readonly float[] ring = new float[RingSize];
    int ringIndex;
    Transform listener;
    int lastKernelIndex = -1;
    float cachedSide;
    float targetSide;
    float cachedFront = 1f;
    float targetFront = 1f;
    float cachedHeight;
    float targetHeight;
    float cachedDistanceGain = 0.75f;
    float targetDistanceGain = 0.75f;
    float cachedRoomDistanceGain = 0.65f;
    float targetRoomDistanceGain = 0.65f;

    void Awake()
    {
        listener = FindAnyObjectByType<AudioListener>()?.transform;
        LoadOrBuildKernels();
        UpdateSparseTaps(true);
    }

    void Update()
    {
        UpdateSparseTaps(false);
    }

    public void ProcessSample(float mono, out float left, out float right)
    {
        ring[ringIndex] = mono;

        cachedSide += (targetSide - cachedSide) * 0.0016f;
        cachedFront += (targetFront - cachedFront) * 0.0016f;
        cachedHeight += (targetHeight - cachedHeight) * 0.0016f;
        cachedDistanceGain += (targetDistanceGain - cachedDistanceGain) * 0.0011f;
        cachedRoomDistanceGain += (targetRoomDistanceGain - cachedRoomDistanceGain) * 0.0011f;

        float side = cachedSide;
        int itdSamples = Mathf.RoundToInt(Mathf.Abs(side) * width * 36f);
        float leftDirect = side > 0f ? ReadRingDelay(itdSamples) : mono;
        float rightDirect = side < 0f ? ReadRingDelay(itdSamples) : mono;

        float pan = Mathf.Clamp(side * width * 1.18f, -0.98f, 0.98f);
        float angle = (pan + 1f) * Mathf.PI * 0.25f;
        float leftGain = Mathf.Cos(angle);
        float rightGain = Mathf.Sin(angle);
        float frontPresence = Mathf.Lerp(0.82f, 1.06f, Mathf.Clamp01((cachedFront + 1f) * 0.5f));

        float l = leftDirect * directAmount * cachedDistanceGain * frontPresence * leftGain;
        float r = rightDirect * directAmount * cachedDistanceGain * frontPresence * rightGain;

        var activeTaps = taps;
        float roomFront = cachedFront * 0.85f;
        float roomSide = cachedSide * 0.9f;
        float roomHeight = cachedHeight * 0.3f;
        for (int i = 0; i < activeTaps.Length; i++)
        {
            int idx = ringIndex - activeTaps[i].delay;
            while (idx < 0) idx += RingSize;
            float s = ring[idx & (RingSize - 1)];
            float omni = activeTaps[i].omni;
            float frontBack = activeTaps[i].front * roomFront;
            float sideEnergy = activeTaps[i].side * roomSide;
            float heightEnergy = activeTaps[i].height * roomHeight;
            l += s * (omni + frontBack + sideEnergy + heightEnergy) * roomAmount * cachedRoomDistanceGain;
            r += s * (omni + frontBack - sideEnergy + heightEnergy) * roomAmount * cachedRoomDistanceGain;
        }

        ringIndex = (ringIndex + 1) & (RingSize - 1);

        left = Mathf.Clamp(l, -1f, 1f);
        right = Mathf.Clamp(r, -1f, 1f);
    }

    void LoadOrBuildKernels()
    {
        string root = Path.Combine(Application.streamingAssetsPath, "HiFiHARP");
        string[] wavs = Directory.Exists(root)
            ? Directory.GetFiles(root, "*.wav", SearchOption.AllDirectories)
            : Array.Empty<string>();
        Array.Sort(wavs, StringComparer.OrdinalIgnoreCase);

        var loaded = new List<KernelSet>();
        int maxFiles = Mathf.Min(9, wavs.Length);
        for (int i = 0; i < maxFiles; i++)
        {
            if (!SpatialAudioWav.TryRead(wavs[i], out var audio) || audio.channels < 4)
                continue;

            loaded.Add(BuildFromFoaWav(audio));
        }

        if (loaded.Count > 0)
        {
            rirKernels = loaded.ToArray();
            ApplyKernel(0);
            Debug.Log("HiFi-HARP spatializer loaded " + loaded.Count + " FOA RIRs from " + root);
            return;
        }

        BuildFallbackFoaRoom();
        Debug.Log("HiFi-HARP spatializer using lightweight built-in FOA fallback. Put FOA wav files under StreamingAssets/HiFiHARP to use the dataset.");
    }

    KernelSet BuildFromFoaWav(SpatialAudioWav.AudioData audio)
    {
        int outputRate = Mathf.Max(1, AudioSettings.outputSampleRate);
        int frames = audio.samples.Length / audio.channels;
        int length = Mathf.Min(MaxKernelSamples, Mathf.CeilToInt(frames * outputRate / (float)audio.sampleRate));
        var kernel = new KernelSet
        {
            w = new float[length],
            x = new float[length],
            y = new float[length],
            z = new float[length]
        };

        float step = audio.sampleRate / (float)outputRate;
        for (int i = 0; i < length; i++)
        {
            float sourceIndex = i * step;
            kernel.w[i] = ReadChannelLinear(audio, 0, sourceIndex);
            kernel.y[i] = ReadChannelLinear(audio, 1, sourceIndex);
            kernel.z[i] = ReadChannelLinear(audio, 2, sourceIndex);
            kernel.x[i] = ReadChannelLinear(audio, 3, sourceIndex);
        }

        NormalizeKernel(ref kernel);
        return kernel;
    }

    void BuildFallbackFoaRoom()
    {
        int sr = Mathf.Max(1, AudioSettings.outputSampleRate);
        int length = Mathf.Min(MaxKernelSamples, Mathf.RoundToInt(sr * 0.12f));
        wKernel = new float[length];
        xKernel = new float[length];
        yKernel = new float[length];
        zKernel = new float[length];

        AddImpulse(wKernel, 0, 0.3f);
        AddImpulse(xKernel, Mathf.RoundToInt(sr * 0.002f), 0.11f);
        AddImpulse(yKernel, Mathf.RoundToInt(sr * 0.0035f), 0.1f);
        AddImpulse(wKernel, Mathf.RoundToInt(sr * 0.018f), 0.16f);
        AddImpulse(xKernel, Mathf.RoundToInt(sr * 0.031f), -0.1f);
        AddImpulse(yKernel, Mathf.RoundToInt(sr * 0.044f), 0.08f);

        for (int i = Mathf.RoundToInt(sr * 0.055f); i < length; i += 83)
        {
            float t = i / (float)sr;
            float decay = Mathf.Exp(-t * 12f) * 0.045f;
            wKernel[i] += Mathf.Sin(i * 0.071f) * decay;
            xKernel[i] += Mathf.Sin(i * 0.049f + 1.7f) * decay * 0.5f;
            yKernel[i] += Mathf.Sin(i * 0.061f + 0.9f) * decay * 0.5f;
        }

        rirKernels = new[]
        {
            new KernelSet { w = wKernel, x = xKernel, y = yKernel, z = zKernel }
        };
    }

    void UpdateSparseTaps(bool force)
    {
        Vector3 world;
        Vector3 local = GetListenerRelativeDirection(out world);
        targetSide = Mathf.Clamp(local.x, -1f, 1f);
        targetFront = Mathf.Clamp(local.z, -1f, 1f);
        targetHeight = Mathf.Clamp(local.y, -1f, 1f);
        float distance = world.magnitude;
        float distance01 = Mathf.Clamp01((distance - 35f) / 620f);
        targetDistanceGain = Mathf.Lerp(1.12f, 0.86f, distance01);
        targetRoomDistanceGain = Mathf.Lerp(0.9f, 1.58f, distance01);
        int kernelIndex = SelectKernelIndex();
        if (!force && kernelIndex == lastKernelIndex)
            return;

        lastKernelIndex = kernelIndex;
        ApplyKernel(kernelIndex);

        var candidates = new List<Tap>(MaxSparseTaps * 2);
        int stride = Mathf.Max(1, wKernel.Length / MaxSparseTaps);
        for (int i = 0; i < wKernel.Length; i += stride)
        {
            AddCandidate(candidates, i);
        }

        AddStrongestLocalMaxima(candidates);
        candidates.Sort((a, b) => a.delay.CompareTo(b.delay));

        if (candidates.Count > MaxSparseTaps)
            candidates.RemoveRange(MaxSparseTaps, candidates.Count - MaxSparseTaps);

        taps = candidates.ToArray();
    }

    int SelectKernelIndex()
    {
        if (rirKernels == null || rirKernels.Length == 0)
            return 0;

        int index = rirIndex;
        return Mathf.Clamp(index, 0, rirKernels.Length - 1);
    }

    void ApplyKernel(int index)
    {
        if (rirKernels == null || rirKernels.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, rirKernels.Length - 1);
        wKernel = rirKernels[index].w;
        xKernel = rirKernels[index].x;
        yKernel = rirKernels[index].y;
        zKernel = rirKernels[index].z;
    }

    void AddStrongestLocalMaxima(List<Tap> candidates)
    {
        var peaks = new List<int>(MaxSparseTaps);
        for (int i = 1; i < wKernel.Length - 1; i++)
        {
            float e = AbsFoa(i);
            if (e > AbsFoa(i - 1) && e >= AbsFoa(i + 1))
                peaks.Add(i);
        }

        peaks.Sort((a, b) => AbsFoa(b).CompareTo(AbsFoa(a)));
        int count = Mathf.Min(MaxSparseTaps / 2, peaks.Count);
        for (int i = 0; i < count; i++)
            AddCandidate(candidates, peaks[i]);
    }

    void AddCandidate(List<Tap> candidates, int index)
    {
        float omni = wKernel[index] * 0.7071f;
        float front = xKernel[index];
        float side = yKernel[index];
        float height = zKernel[index];
        if (Mathf.Abs(omni) + Mathf.Abs(front) + Mathf.Abs(side) + Mathf.Abs(height) < 0.00035f)
            return;

        candidates.Add(new Tap { delay = index, omni = omni, front = front, side = side, height = height });
    }

    float AbsFoa(int index)
    {
        return Mathf.Abs(wKernel[index]) + Mathf.Abs(xKernel[index]) + Mathf.Abs(yKernel[index]) + Mathf.Abs(zKernel[index]);
    }

    float ReadRingDelay(int delay)
    {
        int idx = ringIndex - delay;
        while (idx < 0) idx += RingSize;
        return ring[idx & (RingSize - 1)];
    }

    Vector3 GetListenerRelativeDirection(out Vector3 world)
    {
        if (listener == null)
            listener = FindAnyObjectByType<AudioListener>()?.transform;

        if (listener == null)
        {
            world = transform.position;
            return transform.position.normalized;
        }

        world = transform.position - listener.position;
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

    void NormalizeKernel(ref KernelSet kernel)
    {
        float peak = 0f;
        for (int i = 0; i < kernel.w.Length; i++)
        {
            peak = Mathf.Max(peak, Mathf.Abs(kernel.w[i]));
            peak = Mathf.Max(peak, Mathf.Abs(kernel.x[i]));
            peak = Mathf.Max(peak, Mathf.Abs(kernel.y[i]));
            peak = Mathf.Max(peak, Mathf.Abs(kernel.z[i]));
        }

        if (peak < 0.0001f)
            return;

        float gain = 0.28f / peak;
        Scale(kernel.w, gain);
        Scale(kernel.x, gain);
        Scale(kernel.y, gain);
        Scale(kernel.z, gain);
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
