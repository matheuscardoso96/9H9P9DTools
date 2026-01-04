using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Lib999
{
    public static class SirUtils
    {
        public static uint DecodeVarint(byte[] buff)
        {

            if (buff.Length < 1)
                return 0;

            if (buff[0] <= 0x80)
                return buff[0];

            int x = 0;

            for (int i = 0; i < buff.Length; i++)
            {
                x = x << 7;

                x |= buff[i] & 0x7F;

                if ((buff[i] & 0x80) == 0)
                {
                    return (uint)x;
                }
            }


            return (uint)x;
        }

        public static byte[] EncodeVarint(uint x)
        {
            if (x >> 7 == 0)
            {
                return new byte[]{
            (byte)x,
        };
            }

            if (x >> 14 == 0)
            {
                return new byte[]{
            (byte)(0x80 | x >> 7),
            (byte)(127 & x)
        };
            }

            if (x >> 21 == 0)
            {
                return new byte[]{
            (byte)(0x80 | x >> 14),
            (byte)(0x80 | x >> 7),
            (byte)(127 & x)
        };
            }

            return new byte[]{
        (byte)(0x80 | x >> 21),
        (byte)(0x80 | x >> 14),
        (byte)(0x80 | x >> 7),
        (byte)(127 & x)
        };
    }

        public static void AlignBy(this BinaryWriter bw, int value)
        {
            if (bw.BaseStream.Position % value != 0)
            {
                while (bw.BaseStream.Position % value != 0)
                    bw.Write((byte)0xAA);
            }

        }

        public static string ReadNullTerminatedString(BinaryReader br)
        {
            var sb = new StringBuilder();
            char c;
            while ((c = br.ReadChar()) != '\0')
            {
                sb.Append(c);
            }
            return sb.ToString();
        }
    }



}
