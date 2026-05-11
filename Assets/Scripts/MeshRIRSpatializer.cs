using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class MeshRIRSpatializer : MonoBehaviour, IPlanetSpatializer
{
    const int MaxKernelSamples = 2048;
    const int RingSize = 4096;

    [Range(0f, 1f)] public float rirAmount = 0.58f;
    [Range(0f, 1f)] public float directAmount = 0.92f;
    [Range(0f, 1f)] public float stereoWidth = 0.75f;
    [Range(0f, 0.002f)] public float maxInterauralDelay = 0.00065f;

    float[] monoKernel;
    readonly float[] ring = new float[RingSize];
    int ringIndex;
    Transform listener;

    void Awake()
    {
        listener = FindAnyObjectByType<AudioListener>()?.transform;
        LoadOrBuildKernel();
    }

    public void ProcessSample(float mono, out float left, out float right)
    {
        Vector3 local = GetListenerRelativeDirection();
        float side = Mathf.Clamp(local.x, -1f, 1f);
        float distance = Mathf.Max(1f, Vector3.Distance(transform.position, listener != null ? listener.position : Vector3.zero));
        float airGain = 1f / Mathf.Sqrt(distance * 0.02f + 1f);

        ring[ringIndex] = mono;

        int delaySamples = Mathf.RoundToInt(Mathf.Abs(side) * maxInterauralDelay * AudioSettings.outputSampleRate);
        int leftDelay = side > 0f ? delaySamples : 0;
        int rightDelay = side < 0f ? delaySamples : 0;
        float leftGain = (1f - side * stereoWidth * 0.35f) * airGain;
        float rightGain = (1f + side * stereoWidth * 0.35f) * airGain;

        float l = ReadRing(leftDelay) * directAmount * leftGain;
        float r = ReadRing(rightDelay) * directAmount * rightGain;

        var kernel = monoKernel;
        int n = kernel != null ? kernel.Length : 0;
        for (int i = 0; i < n; i++)
        {
            float wet = ReadRing(i + delaySamples / 2) * kernel[i] * rirAmount;
            float spread = Mathf.Sin(i * 0.017f + side * 1.3f) * stereoWidth;
            l += wet * (1f - spread * 0.25f);
            r += wet * (1f + spread * 0.25f);
        }

        ringIndex++;
        if (ringIndex >= RingSize) ringIndex = 0;

        left = Mathf.Clamp(l, -1f, 1f);
        right = Mathf.Clamp(r, -1f, 1f);
    }

    void LoadOrBuildKernel()
    {
        string root = Path.Combine(Application.streamingAssetsPath, "MeshRIR");
        string wav = SpatialAudioWav.FindFirstWav(root);
        if (!string.IsNullOrEmpty(wav) && SpatialAudioWav.TryRead(wav, out var audio))
        {
            monoKernel = BuildFromWav(audio);
            Debug.Log("MeshRIR spatializer loaded wav RIR: " + wav);
            return;
        }

        string npy = FindFirstMeshIr(root);
        if (!string.IsNullOrEmpty(npy) && SpatialAudioNpy.TryRead(npy, out var array))
        {
            monoKernel = BuildFromNpy(array);
            Debug.Log("MeshRIR spatializer loaded numpy RIR: " + npy);
            return;
        }

        monoKernel = BuildFallbackMeshKernel();
        Debug.Log("MeshRIR spatializer using built-in mesh fallback. Put MeshRIR ir_*.npy or exported wav files under StreamingAssets/MeshRIR to use the dataset.");
    }

    float[] BuildFromWav(SpatialAudioWav.AudioData audio)
    {
        int outputRate = Mathf.Max(1, AudioSettings.outputSampleRate);
        int frames = audio.samples.Length / audio.channels;
        int length = Mathf.Min(MaxKernelSamples, Mathf.CeilToInt(frames * outputRate / (float)audio.sampleRate));
        var kernel = new float[length];
        float step = audio.sampleRate / (float)outputRate;

        for (int i = 0; i < length; i++)
        {
            float sourceIndex = i * step;
            int i0 = Mathf.Clamp((int)sourceIndex, 0, frames - 1);
            int i1 = Mathf.Min(i0 + 1, frames - 1);
            float t = sourceIndex - i0;
            float a = 0f;
            float b = 0f;
            for (int c = 0; c < audio.channels; c++)
            {
                a += audio.samples[i0 * audio.channels + c];
                b += audio.samples[i1 * audio.channels + c];
            }
            kernel[i] = Mathf.Lerp(a, b, t) / audio.channels;
        }

        Normalize(kernel);
        return kernel;
    }

    float[] BuildFromNpy(SpatialAudioNpy.ArrayData array)
    {
        int irLen = array.shape[array.shape.Length - 1];
        int length = Mathf.Min(MaxKernelSamples, irLen);
        var kernel = new float[length];

        int rowOffset = 0;
        if (array.shape.Length >= 2)
        {
            int rows = array.values.Length / irLen;
            int row = Mathf.Abs(GetInstanceID()) % Mathf.Max(1, rows);
            rowOffset = row * irLen;
        }

        for (int i = 0; i < length; i++)
            kernel[i] = array.values[rowOffset + i];

        Normalize(kernel);
        return kernel;
    }

    float[] BuildFallbackMeshKernel()
    {
        int sr = Mathf.Max(1, AudioSettings.outputSampleRate);
        int length = Mathf.Min(MaxKernelSamples, Mathf.RoundToInt(sr * 0.14f));
        var kernel = new float[length];

        AddImpulse(kernel, Mathf.RoundToInt(sr * 0.006f), 0.22f);
        AddImpulse(kernel, Mathf.RoundToInt(sr * 0.013f), -0.18f);
        AddImpulse(kernel, Mathf.RoundToInt(sr * 0.022f), 0.15f);
        AddImpulse(kernel, Mathf.RoundToInt(sr * 0.037f), -0.11f);

        for (int i = Mathf.RoundToInt(sr * 0.045f); i < length; i++)
        {
            float t = i / (float)sr;
            kernel[i] += Mathf.Sin(i * 0.083f) * Mathf.Exp(-t * 13f) * 0.055f;
        }

        return kernel;
    }

    float ReadRing(int delay)
    {
        int idx = ringIndex - delay;
        while (idx < 0) idx += RingSize;
        return ring[idx % RingSize];
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

    static string FindFirstMeshIr(string root)
    {
        if (!Directory.Exists(root))
            return null;

        var files = new List<string>(Directory.GetFiles(root, "ir_*.npy", SearchOption.AllDirectories));
        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files.Count > 0 ? files[0] : null;
    }

    static void Normalize(float[] kernel)
    {
        float peak = 0f;
        for (int i = 0; i < kernel.Length; i++)
            peak = Mathf.Max(peak, Mathf.Abs(kernel[i]));

        if (peak < 0.0001f)
            return;

        float gain = 0.36f / peak;
        for (int i = 0; i < kernel.Length; i++)
            kernel[i] *= gain;
    }

    static void AddImpulse(float[] kernel, int index, float value)
    {
        if (index >= 0 && index < kernel.Length)
            kernel[index] += value;
    }
}
