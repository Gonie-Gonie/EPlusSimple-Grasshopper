namespace GonieGonie.SimpleDragon.Internal;

/// <summary>
/// Reproduces CPython 3.12's Unicode string hash when <c>PYTHONHASHSEED=0</c>.
/// </summary>
/// <remarks>
/// The Python SimpleDragon reference derives otherwise unspecified wall azimuths from
/// <c>hash(surface.ID)</c>. The compatibility runner fixes the Python hash seed to zero,
/// so the C# port must use the same SipHash13 result instead of the process-specific
/// .NET string hash.
/// </remarks>
internal static class PythonSeedZeroStringHash
{
    private const ulong InitialV0 = 0x736f6d6570736575UL;
    private const ulong InitialV1 = 0x646f72616e646f6dUL;
    private const ulong InitialV2 = 0x6c7967656e657261UL;
    private const ulong InitialV3 = 0x7465646279746573UL;

    public static long Compute(string value)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value);
#else
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#endif

        if (value.Length == 0)
        {
            return 0L;
        }

        byte[] unicodeData = GetPythonUnicodeData(value);
        ulong hash = SipHash13(unicodeData);
        long signed = unchecked((long)hash);
        return signed == -1L ? -2L : signed;
    }

    private static byte[] GetPythonUnicodeData(string value)
    {
        var codePoints = new List<uint>(value.Length);
        uint maximum = 0U;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            uint codePoint;
            if (char.IsHighSurrogate(current)
                && index + 1 < value.Length
                && char.IsLowSurrogate(value[index + 1]))
            {
                codePoint = (uint)char.ConvertToUtf32(current, value[++index]);
            }
            else
            {
                codePoint = current;
            }

            codePoints.Add(codePoint);
            maximum = Math.Max(maximum, codePoint);
        }

        int kind = maximum <= byte.MaxValue
            ? 1
            : maximum <= ushort.MaxValue
                ? 2
                : 4;
        byte[] data = new byte[codePoints.Count * kind];
        int offset = 0;
        foreach (uint codePoint in codePoints)
        {
            for (int byteIndex = 0; byteIndex < kind; byteIndex++)
            {
                data[offset++] = (byte)(codePoint >> (byteIndex * 8));
            }
        }

        return data;
    }

    private static ulong SipHash13(IReadOnlyList<byte> data)
    {
        unchecked
        {
            ulong v0 = InitialV0;
            ulong v1 = InitialV1;
            ulong v2 = InitialV2;
            ulong v3 = InitialV3;
            int offset = 0;
            while (offset + 8 <= data.Count)
            {
                ulong lane = ReadLittleEndian(data, offset, 8);
                v3 ^= lane;
                Round(ref v0, ref v1, ref v2, ref v3);
                v0 ^= lane;
                offset += 8;
            }

            ulong final = (ulong)data.Count << 56;
            final |= ReadLittleEndian(data, offset, data.Count - offset);
            v3 ^= final;
            Round(ref v0, ref v1, ref v2, ref v3);
            v0 ^= final;
            v2 ^= 0xffUL;
            Round(ref v0, ref v1, ref v2, ref v3);
            Round(ref v0, ref v1, ref v2, ref v3);
            Round(ref v0, ref v1, ref v2, ref v3);
            return v0 ^ v1 ^ v2 ^ v3;
        }
    }

    private static ulong ReadLittleEndian(IReadOnlyList<byte> data, int offset, int count)
    {
        ulong result = 0UL;
        for (int index = 0; index < count; index++)
        {
            result |= (ulong)data[offset + index] << (index * 8);
        }

        return result;
    }

    private static void Round(ref ulong v0, ref ulong v1, ref ulong v2, ref ulong v3)
    {
        unchecked
        {
            v0 += v1;
            v1 = RotateLeft(v1, 13);
            v1 ^= v0;
            v0 = RotateLeft(v0, 32);
            v2 += v3;
            v3 = RotateLeft(v3, 16);
            v3 ^= v2;
            v0 += v3;
            v3 = RotateLeft(v3, 21);
            v3 ^= v0;
            v2 += v1;
            v1 = RotateLeft(v1, 17);
            v1 ^= v2;
            v2 = RotateLeft(v2, 32);
        }
    }

    private static ulong RotateLeft(ulong value, int count)
    {
        return (value << count) | (value >> (64 - count));
    }
}
