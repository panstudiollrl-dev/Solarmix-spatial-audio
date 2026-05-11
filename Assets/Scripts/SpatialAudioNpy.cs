using System;
using System.IO;
using System.Text;

public static class SpatialAudioNpy
{
    public struct ArrayData
    {
        public int[] shape;
        public float[] values;
    }

    public static bool TryRead(string path, out ArrayData data)
    {
        data = default;
        if (!File.Exists(path))
            return false;

        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 16 || bytes[0] != 0x93 || Encoding.ASCII.GetString(bytes, 1, 5) != "NUMPY")
            return false;

        int major = bytes[6];
        int headerLengthOffset = 8;
        int headerLengthSize = major >= 2 ? 4 : 2;
        int headerLength = headerLengthSize == 2
            ? BitConverter.ToUInt16(bytes, headerLengthOffset)
            : BitConverter.ToInt32(bytes, headerLengthOffset);
        int headerOffset = headerLengthOffset + headerLengthSize;
        string header = Encoding.ASCII.GetString(bytes, headerOffset, headerLength);

        string descr = ExtractString(header, "'descr': '", "'");
        bool fortran = header.Contains("'fortran_order': True");
        int[] shape = ExtractShape(header);
        if (fortran || shape.Length == 0)
            return false;

        int count = 1;
        for (int i = 0; i < shape.Length; i++)
            count *= shape[i];

        int dataOffset = headerOffset + headerLength;
        var values = new float[count];
        if (descr == "<f4" || descr == "|f4")
        {
            for (int i = 0; i < count; i++)
                values[i] = BitConverter.ToSingle(bytes, dataOffset + i * 4);
        }
        else if (descr == "<f8" || descr == "|f8")
        {
            for (int i = 0; i < count; i++)
                values[i] = (float)BitConverter.ToDouble(bytes, dataOffset + i * 8);
        }
        else
        {
            return false;
        }

        data = new ArrayData { shape = shape, values = values };
        return true;
    }

    static string ExtractString(string header, string prefix, string suffix)
    {
        int start = header.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0) return "";
        start += prefix.Length;
        int end = header.IndexOf(suffix, start, StringComparison.Ordinal);
        return end < 0 ? "" : header.Substring(start, end - start);
    }

    static int[] ExtractShape(string header)
    {
        int start = header.IndexOf("'shape': (", StringComparison.Ordinal);
        if (start < 0) return Array.Empty<int>();
        start += "'shape': (".Length;
        int end = header.IndexOf(")", start, StringComparison.Ordinal);
        if (end < 0) return Array.Empty<int>();

        string[] parts = header.Substring(start, end - start).Split(',');
        var tmp = new int[parts.Length];
        int n = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            if (p.Length == 0) continue;
            if (int.TryParse(p, out int v))
                tmp[n++] = v;
        }

        var shape = new int[n];
        Array.Copy(tmp, shape, n);
        return shape;
    }
}
