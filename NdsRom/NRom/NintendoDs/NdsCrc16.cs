namespace NdsRom.NRom.NintendoDs;

/// <summary>
/// CRC-16 used by Nintendo DS headers, secure area and banners.
/// Polynomial 0xA001, initial value 0xFFFF, no final xor.
/// </summary>
internal static class NdsCrc16
{
    public static ushort Compute(ReadOnlySpan<byte> data, ushort initial = 0xFFFF)
    {
        ushort crc = initial;

        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (ushort)((crc & 1) != 0
                    ? (crc >> 1) ^ 0xA001
                    : crc >> 1);
            }
        }

        return crc;
    }
}
