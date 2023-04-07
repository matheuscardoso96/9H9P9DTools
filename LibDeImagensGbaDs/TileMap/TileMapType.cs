using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace LibDeImagensGbaDs.TileMap
{
    public class TileMapType
    {
        public List<ushort> Tilemap { get; set; }
        public List<Bitmap> Tiles { get; set; }
    }
}
