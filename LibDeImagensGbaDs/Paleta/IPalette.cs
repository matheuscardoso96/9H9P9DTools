using System.Drawing;

namespace LibDeImagensGbaDs.Paleta
{
    public interface IPalette
    {
        Color[] Colors { get; set; }
        byte GetNearColorIndex(Color c1);
    }
}
