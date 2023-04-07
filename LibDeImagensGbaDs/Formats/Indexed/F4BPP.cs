using System;

namespace LibDeImagensGbaDs.Formats.Indexed
{
    public class F4BPP : IIndexedFormat
    {

        public const int Bpp = 4;

        public byte[] DecompressIndexes(byte[] arquivo, ref byte[] alphaValues)
        {
           
            byte[] final = new byte[arquivo.Length * 2];

            int contador = 0;
            for (int i = 0; i < arquivo.Length; i++)
            {
                int nibbleAlto = (arquivo[i] & 0xF0) >> 4;
                int nibbleBaixo = arquivo[i] & 0x0F;
                final[contador] = (byte)nibbleBaixo;
                final[contador + 1] = (byte)nibbleAlto;
                contador+=2;

            }


           return final;
        }

        public byte[] CompressIndexes(byte[] indices)
        {

            byte[] listaDeIndicesFinal = new byte[indices.Length / 2];

            int contador = 0;

            for (int i = 0; i < indices.Length; i+=2)
            {
                byte indice =(byte)((indices[i + 1] << 4)  + indices[i]);
                listaDeIndicesFinal[contador] = indice;
                contador++;
            }

            return listaDeIndicesFinal;

        }

    }
}
