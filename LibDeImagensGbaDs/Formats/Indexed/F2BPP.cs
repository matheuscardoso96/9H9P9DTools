using System;
using System.Collections;

namespace LibDeImagensGbaDs.Formats.Indexed
{
    public class F2BPP : IIndexedFormat
    {
        public const int Bpp = 2;
        public byte[] AlphaValues { get; set; }

        public byte[] DecompressIndexes(byte[] arquivo, ref byte[] alphaValues)
        {

            BitArray bitArray = new BitArray(arquivo);
            byte[] final = new byte[arquivo.Length * 4];
            
            int contador = 0;
            for (int i = 0; i < bitArray.Length; i += 2) 
            {
                int valor1 = bitArray[i] ? 1 : 0;
                int valor2 = bitArray[i + 1] ? 1 : 0;
                int valorFinal = (valor2 << 1) | valor1;
                final[contador] = (byte)valorFinal;
                contador++;
            }
                

            return final;
        }

        public byte[] CompressIndexes(byte[] indices)
        {
            throw new NotImplementedException();
        }
    }
}
