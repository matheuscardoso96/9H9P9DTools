using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace LibDeImagensGbaDs.Sprites
{
    public static class SpriteTool
    {
        public static Bitmap CreateFrame(List<Oam> oams)
        {
            Bitmap final = new Bitmap(512, 256);

            using (Graphics g = Graphics.FromImage(final))
            {
                foreach (var item in oams)
                {

                }
            }

            
            return null;
        }
    }
}
