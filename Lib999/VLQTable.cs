using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lib999
{
    public class VLQTable
    {
        public List<byte> DataVLQ { get; set; } = new();
        public List<byte> DataVLQOg { get; set; } = new();
        public List<uint> DecompressedValues { get; set; } = new();
        public List<uint> OgDecompressedValuesValues { get; set; } = new();
        public List<uint> TopValues { get; set; } = new();
        public VLQTable(BinaryReader br, string fname = "")
        {
            

            var position = br.BaseStream.Position;
            byte unknown = br.ReadByte();
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                DataVLQ.Add(unknown);
                unknown = br.ReadByte();
            }
            DataVLQOg = DataVLQ;
            /*if (unknown != 0x4)
            {
                var r2 = 0;
                var r5 = 0;
                while (unknown != 4 && unknown != 0xAA || unknown != 0x00)
                {
                    r2 = r2 << 7;
                    r5 = unknown & 0x7F;
                    r2 = r2 ^ r5;
                    r5 = unknown & 0x80;
                    unknown = br.ReadByte();
                }
            }
            else
            {
                Unknown.Add(unknown);
                unknown = br.ReadByte();
            }*/

            br.BaseStream.Position = position;
            ReadOffestArea(br, fname);

            //var lists = new Dictionary<uint, List<uint>>();
            //lists.Add(0, new List<uint>());

            //var key = 0u;
            //foreach (var item in DecompressedValues)
            //{

            //    //if (lists.ContainsKey(key)) { continue; }

            //    if (item != 4)
            //    {
            //      key =  item;
            //      lists.Add((uint)key, new List<uint>());
            //       continue;
            //    }


            //    lists[key].Add(item);

            //}
            OgDecompressedValuesValues = DecompressedValues;
            TopValues = DecompressedValues.Where(x => x > 4).ToList();
        }

        public VLQTable()
        {
            DecompressedValues = new List<uint>();
            DataVLQ = new List<byte>();
        }

        public void ReadOffestArea(BinaryReader br, string fname="") 
        {

            var t = SirUtils.EncodeVarint(0xBC);
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                byte code = br.ReadByte();

                if (code == 0)
                    break;

                var bytesToRead = new List<byte> { code };

                while ((code & 0x80) != 0)
                {
                    code = br.ReadByte();
                    bytesToRead.Add(code);
                }

                var decoded = SirUtils.DecodeVarint(bytesToRead.ToArray());
                DecompressedValues.Add(decoded);
            }

            var sting = string.Join("\r\n", DecompressedValues.Select(x => x.ToString("X8")));
        }

        public void ConvertToDataVLQ() 
        {
            foreach (var offset in DecompressedValues)
            {
                if (offset > 8)
                {
                    var encoded = SirUtils.EncodeVarint(offset);
                    DataVLQ.AddRange(encoded);
                }
                else
                    DataVLQ.Add((byte)offset);
                
            }
        }

        internal void WriteDataVLQ(BinaryWriter bw)
        {
            bw.Write(DataVLQ.ToArray());
        }
    }
}
