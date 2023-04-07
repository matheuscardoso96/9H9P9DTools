using System;

namespace LibDeImagensGbaDs.Formats.Indexed
{
    public class FA5I3 : IIndexedFormat
    {
        public const int Bpp = 8;
        public byte[] AlphaValues { get; set; }
        public byte[] DecompressIndexes(byte[] arquivo, ref byte[] alphaValues)
        {         
            byte[] final = new byte[arquivo.Length];
            AlphaValues = new byte[arquivo.Length];

            for (int i = 0; i < arquivo.Length; i++)
            {
                int valorNaPaleta = arquivo[i] & 7;
                int valorAlpha = arquivo[i] >> 3;
                valorAlpha = valorAlpha * 255 / 31;

                AlphaValues[i] = (byte)valorAlpha;
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
