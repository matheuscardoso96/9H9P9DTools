using System;

namespace LibDeImagensGbaDs.Formats.Indexed
{
    public class FA3I5 : IIndexedFormat
    {
        public const int Bpp = 5;

        public byte[] DecompressIndexes(byte[] arquivo, ref byte[] alphaValues)
        {
            byte[] final = new byte[arquivo.Length];
            alphaValues = new byte[final.Length];

            for (int i = 0; i < arquivo.Length; i++)
            {
                int valorNaPaleta = arquivo[i] & 0x1F;
                int valorAlpha = arquivo[i] >> 5;
                valorAlpha = (valorAlpha * 4) + (valorAlpha / 2);
                valorAlpha = valorAlpha * 255 / 31;
                alphaValues[i] = (byte)valorAlpha;
                final[i] = (byte)valorNaPaleta;
            }


            return final;
        }

        public byte[] CompressIndexes(byte[] indices)
        {
            throw new NotImplementedException();
        }
    }
}
