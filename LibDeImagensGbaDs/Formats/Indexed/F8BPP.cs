using System;

namespace LibDeImagensGbaDs.Formats.Indexed
{
    public class F8BPP: IIndexedFormat
    {
        public const int Bpp = 8;
        public byte[] AlphaValues { get; set; }

        public byte[] DecompressIndexes(byte[] arquivo, ref byte[] alphaValues)
        {
            return arquivo;
        }

        public byte[] CompressIndexes(byte[] indices)
        {
            return indices;
        }
    }
}
