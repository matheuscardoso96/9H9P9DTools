using System.Collections;

namespace Lib999.Compression
{
    public class ATP6
    {

        //Decode from https://github.com/pleonex/tinke/blob/master/Plugins/999HRPERDOOR/999HRPERDOOR/AT6P.cs
        public static MemoryStream Decode(Stream encoded)
        {
            MemoryStream? decoded = null;
            BinaryReader br = new BinaryReader(encoded);

            // Header check (AT6P)
            if (br.ReadUInt32() != 0x50365441)
                throw new InvalidDataException("Wrong file header");

            // Get decoded size
            encoded.Position = 0x10;
            int decSize = (int)(br.ReadUInt32() & 0x00FFFFFF);
            decoded = new MemoryStream(decSize);

            encoded.Position = 0x14;
            decoded.Position = 0;

            if (decSize <= 4)
                return decoded;

            int prev = -1;
            int code = encoded.ReadByte();
            decoded.WriteByte((byte)code);
            encoded.Position++;

            uint nbits = 0;
            uint flags = 0;

            // Decode until fill buffer
            while (decoded.Position < decSize)
            {
                // Fill the flag
                while (nbits < 17)
                {
                    if (encoded.Position < encoded.Length)
                    {
                        var encodedByte = encoded.ReadByte();
                        var encodedByteShift = (uint)(encodedByte << (int)nbits);
                        flags |= encodedByteShift;
                    }


                    nbits += 8;
                }

                int nbit;
                for (nbit = 0; nbit <= 8; nbit++)
                {
                    var bits = (1 << nbit);
                    var result = flags & bits;
                    if (result != 0)
                        break;
                }

                if (nbit > 8)
                {
                    throw new Exception("ERROR: Invalid control mask");
                }

                uint n = (uint)(1 << nbit) - 1;
                var totalTosum = (flags >> (nbit + 1)) & n;
                n += totalTosum;

                if (n == 1)
                {
                    if (prev == -1)
                    {
                        throw new Exception("ERROR: Unexpected control mask found");
                    }
                    if (prev > 1)
                    {

                    }
                    decoded.WriteByte((byte)prev);

                    int t = prev;
                    prev = code;
                    code = t;
                }
                else
                {
                    if (n != 0)
                    {
                        prev = code;

                    }

                    var bits = (int)(n >> 1);
                    var bits2 = (int)(1 - 2 * (n & 1));
                    var code0 = bits * bits2;
                    var multCode = code + code0;
                    code = (int)(multCode & 0xFF);
                    decoded.WriteByte((byte)code);
                }

                var bitsToDrop = 2 * nbit + 1;
                flags >>= bitsToDrop;
                nbits -= (uint)(bitsToDrop);
            }

            decoded.Flush();
            br.Close();
            
            return decoded;
        }

        public static byte[] Encode(byte[] decoded)
        {
            MemoryStream? encoded = new MemoryStream();
            BinaryWriter bw = new(encoded);
            bw.Write(0x50365441);
            bw.BaseStream.Position = 0x14;
            
            var bytesFromFirstByte = GetBits(decoded[0]);
            var buffer = new List<bool>();
            buffer.AddRange(bytesFromFirstByte);

            buffer.AddRange(CreteBits(16 - bytesFromFirstByte.Count));

            int previusPreviusCode = 0;
            var previusCode = decoded[0];

            for (int i = 1; i < decoded.Length; i++)
            {
                var actualCode = decoded[i];

                if (previusPreviusCode == actualCode)
                {
                    AppendAntepenultimateCommand(buffer);

                    if (previusCode != actualCode)
                        previusPreviusCode = previusCode;
                    else
                        previusPreviusCode = -1;

                }
                else if (previusCode == actualCode)
                    buffer.Add(true);
                else
                {
                    AnaliseBytes(buffer, previusCode, actualCode);
                    previusPreviusCode = previusCode;
                }

                previusCode = actualCode;
            }

            BitArray bitArray = new(buffer.ToArray());
            var bytesFinal = BitArrayToByteArray(bitArray);
            bw.Write(bytesFinal);
            bw.BaseStream.Position = 0x10;
            bw.Write(decoded.Length);
            bw.BaseStream.Position = 0x5;
            bw.Write((short)(bytesFinal.Length + 0x14));
            bw.Close();
            return encoded.ToArray();
        }

        private static void AnaliseBytes(List<bool> buffer, int previusCode, int actualCode)
        {
            var valueAndOperation = GetBestDiffAndOperation(previusCode, actualCode);
            var finalCode = valueAndOperation.isSum ? valueAndOperation.value << 1 : (valueAndOperation.value << 1) + 1;

            if (IsFinalCodeEqualsAMask(finalCode))
            {
                var maskBits = GetBits(finalCode + 1);
                buffer.AddRange(maskBits);
                buffer.AddRange(CreteBits(maskBits.Count - 1));
            }
            else
            {
                var bitCountFromDiference = GetBitCount(valueAndOperation.value);
                var bitsFromCountFromDiference = CreteBits(bitCountFromDiference);
                var bitMask = (1 << bitCountFromDiference) - 1;

                var bitsFromMask = GetBits(bitMask + 1);
                buffer.AddRange(bitsFromMask);
                buffer.AddRange(bitsFromCountFromDiference);
                var Size = buffer.Count - bitsFromCountFromDiference.Count;

                var valueToSumToMask2 = finalCode - bitMask;
                var bitsFromValueToSumMask = GetBits(valueToSumToMask2);


                for (int y = 0; y < bitsFromValueToSumMask.Count; y++)
                {
                    buffer[Size] = bitsFromValueToSumMask[y];
                    Size++;

                }
            }
        }

        private static bool IsFinalCodeEqualsAMask(int value) 
        {

            int maskValue = 0x3;

            while (maskValue <= 0xFF)
            {
                if (value == maskValue)
                    return true;

                maskValue = (maskValue <<  1) + 1;
            }

            return false;        
        }

        private static void AppendAntepenultimateCommand(List<bool> buffer)
        {
            var bits1 = GetBits(0b10);
            buffer.AddRange(bits1);
            buffer.Add(false);
        }

        private static (int value, bool isSum) GetBestDiffAndOperation(int previusCode, int actualCode)
        {
            (int value, bool isSum) valueAndOperation;

            if (previusCode > actualCode)
                valueAndOperation = SubIsTheBestOrSum(previusCode, actualCode);
            else
                valueAndOperation = SumIsTheBestOrSub(previusCode, actualCode);

            return valueAndOperation;
        }

        private static (int value, bool isSum) SubIsTheBestOrSum(int previusCode, int actualCode)
        {
            int valueWithMinusBits;
            int valueToSub = previusCode - actualCode;

            int valueToSum = 0;
            while (((previusCode + valueToSum) & 0xFF) != actualCode)
                valueToSum++;

            bool sumOperation = false;
            if (valueToSub == valueToSum)
                valueWithMinusBits = valueToSub;
            else if (valueToSub < valueToSum)
                valueWithMinusBits = valueToSub;
            else
            {
                valueWithMinusBits = valueToSum;
                sumOperation = true;
            }


            return (valueWithMinusBits, sumOperation);
        }

        private static (int value, bool isSum) SumIsTheBestOrSub(int previusCode, int actualCode)
        {
            int valueWithMinusBits;
            int valueToSum = actualCode - previusCode;

            int valueToSub = 0;
            while (((previusCode - valueToSub) & 0xFF) != actualCode)
                valueToSub++;

            bool sumOperation = false;

            if (valueToSum == valueToSub)
                valueWithMinusBits = valueToSum;
            else if(valueToSum < valueToSub)
            {
                valueWithMinusBits = valueToSum;
                sumOperation = true;
            }
            else
                valueWithMinusBits = valueToSub;

            return (valueWithMinusBits, sumOperation);
        }

        public static byte[] BitArrayToByteArray(BitArray bits)
        {
            byte[] ret = new byte[(bits.Length - 1) / 8 + 1];
            bits.CopyTo(ret, 0);
            return ret;
        }

        static int GetBitCount(int n)
        {
            int count = 0;
            while (n != 0)
            {
                n >>= 1;
                count++;
            }
            return count;
        }

        static List<bool> GetBits(int n)
        {
            int count = 0;
            var array = new List<bool>();
            while (n != 0)
            {
                bool v = (n & 1) == 1;
                array.Add(v);
                n >>= 1;
                count++;
            }

            return array;
        }

        static List<bool> CreteBits(int n)
        {
            var array = new List<bool>();
            for (int i = 0; i < n; i++)
                array.Add(false);


            return array;
        }
    }

}
