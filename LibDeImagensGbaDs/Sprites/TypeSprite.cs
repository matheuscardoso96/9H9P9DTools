using LibDeImagensGbaDs.Tipos;
using System.Collections.Generic;


namespace LibDeImagensGbaDs.Sprites
{
    public class TypeSprite : TipoBase
    {
        public List<Oam> Oams { get; set; }
        public int TileBoundary { get; set; }
    }
}
