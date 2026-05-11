using System;
using System.Collections.Generic;
using System.IO;

public static class SpatialAudioWav
{
    public struct AudioData
    {
        public int sampleRate;
        public int channels;
        public float[] samples;
    }

    public static bool TryRead(string path, out AudioData data)
    {
        data = default;
        if (!File.Exists(path))
            return false;

        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 44 || ReadAscii(bytes, 0, 4) != "RIFF" || ReadAscii(bytes, 8, 4) != "WAVE")
            return false;

        int offset = 12;
        int audioFormat = 0;
        int channels = 0;
        int sampleRate = 0;
        int bitsPerSample = 0;
        int dataOffset = -1;
        int dataSize = 0;

        while (offset + 8 <= bytes.Length)
        {
            string chunkId = ReadAscii(bytes, offset, 4);
            int chunkSize = BitConverter.ToInt32(bytes, offset + 4);
            int chunkData = offset + 8;

            if (chunkId == "fmt ")
            {
                audioFormat = BitConverter.ToInt16(bytes, chunkData);
                channels = BitConverter.ToInt16(bytes, chunkData + 2);
                sampleRate = BitConverter.ToInt32(bytes, chunkData + 4);
                bitsPerSample = BitConverter.ToInt16(bytes, chunkData + 14);
            }
            else if (chunkId == "data")
            {
                dataOffset = chunkData;
                dataSize = chunkSize;
            }

            offset = chunkData + chunkSize + (chunkSize & 1);
        }

        if (channels <= 0 || sampleRate <= 0 || dataOffset < 0 || dataSize <= 0)
            return false;

        int bytesPerSample = bitsPerSample / 8;
        int sampleCount = dataSize / bytesPerSample;
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            int p = dataOffset + i * bytesPerSample;
            samples[i] = ReadSample(bytes, p, audioFormat, bitsPerSample);
        }

        data = new AudioData { sampleRate = sampleRate, channels = channels, samples = samples };
        return true;
    }

    public static string FindFirstWav(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        var wavs = new List<string>(Directory.GetFiles(directory, "*.wav", SearchOption.AllDirectories));
        wavs.Sort(StringComparer.OrdinalIgnoreCase);
        return wavs.Count > 0 ? wavs[0] : null;
    }

    static float ReadSample(byte[] bytes, int offset, int audioFormat, int bitsPerSample)
    {
        if (audioFormat == 3 && bitsPerSample == 32)
            return BitConverter.ToSingle(bytes, offset);

        if (audioFormat != 1)
            return 0f;

        switch (bitsPerSample)
        {
            case 16:
                return BitConverter.ToInt16(bytes, offset) / 32768f;
            case 24:
                int v = bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16);
                if ((v & 0x800000) != 0) v |= unchecked((int)0xff000000);
                return v / 8388608f;
            case 32:
                return BitConverter.ToInt32(bytes, offset) / 2147483648f;
            default:
                return 0f;
        }
    }

    static string ReadAscii(byte[] bytes, int offset, int count)
    {
        return System.Text.Encoding.ASCII.GetString(bytes, offset, count);
    }
}
