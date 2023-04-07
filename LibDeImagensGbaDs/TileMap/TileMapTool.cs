using LibDeImagensGbaDs.Util;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;


namespace LibDeImagensGbaDs.TileMap
{
    public static class TileMapTool
    {
        public static Bitmap CreateWithGenericTilemap(List<Bitmap> tiles, Bitmap imagemFinal)
        {
            List<ushort> tileMap = Enumerable.Range(0, tiles.Count).Select(x => (ushort)x).ToList();
            return CreateWithTilemap(tileMap, tiles, imagemFinal, true);
        }

        public static Bitmap CreateWithTilemap(List<ushort> tileMap, List<Bitmap> tiles, Bitmap imagemFinal, bool foiGerado = false)
        {
            using (Graphics g = Graphics.FromImage(imagemFinal))
            {
                int contador = 0;
                for (int y = 0; y < imagemFinal.Height; y += 8)
                {
                    for (int x = 0; x < imagemFinal.Width; x += 8)
                    {
                        Bitmap tile;
                        if (foiGerado)
                        {
                            tile = tiles[tileMap[contador]];
                        }
                        else
                        {
                            int valor = tileMap[contador];
                            int tileNum = valor & 0x3FF;
                            valor >>= 10;
                            tile = tiles[tileNum].Clone(new Rectangle(0, 0, 8, 8), PixelFormat.Format32bppArgb);
                            int horizotal = valor & 1;
                            valor >>= 1;
                            int vertical = valor & 1;

                            if (horizotal == 1)
                                tile.RotateFlip(RotateFlipType.Rotate180FlipY);

                            if (vertical == 1)
                                tile.RotateFlip(RotateFlipType.Rotate180FlipX);


                        }

                        g.DrawImage(tile, x, y);
                        contador++;


                    }
                }

                tiles.Clear();
            }


            return imagemFinal;
        }



        public static List<ushort> GenerateTileMap(TileMapType tileMapType)
        {
            List<ushort> tilemap = new List<ushort>();
            List<Color[]> allTilesColors = GetAllTileColors(tileMapType.Tiles);
            List<Color[]> unicas = GetUniqueTilesAndColors(allTilesColors, tileMapType);

            foreach (var tile in allTilesColors)
            {
                int contador = 0;

                foreach (var tileEscolhida in unicas)
                {

                    if (CompareTilesColors(tile, tileEscolhida))
                    {
                        break;
                    }

                    contador++;
                }

                tilemap.Add((ushort)contador);
            }

           // tiles = unicas;

            return tilemap;
        }

        private static List<Color[]> GetUniqueTilesAndColors(List<Color[]> AllTileColors, TileMapType tileMapType)
        {
            List<Color[]> tilesColors = new List<Color[]>();
            List<Bitmap> unique = new List<Bitmap>();
            int counter = 0;
            foreach (var item in AllTileColors)
            {
                bool existe = false;

                foreach (var tl in tilesColors)
                {
                    if (CompareTilesColors(item, tl))
                    {
                        existe = true;
                        break;
                    }
                }

                if (!existe)
                {
                    tilesColors.Add(item);
                    unique.Add(tileMapType.Tiles[counter]);
                }

                counter++;


            }
            tileMapType.Tiles = unique;
            return tilesColors;
        }

        private static List<Color[]> GetAllTileColors(List<Bitmap> tilesImg)
        {
            List<Color[]> tilesColors = new List<Color[]>();
            foreach (var tile in tilesImg)
            {
                tilesColors.Add(tile.GetColors());
            }
            return tilesColors;
        }

        private static bool CompareTilesColors(Color[] tileColors1, Color[] tileColors2)
        {           

            bool areEqual = true;

            for (int i = 0; i < tileColors1.Length; i++)
            {
                if (!CoresSaoIguais(tileColors1[i], tileColors2[i]))
                {
                    areEqual = false;
                    break;
                }
                

            }

            return areEqual;
        }

        private static bool CoresSaoIguais(Color color1, Color color2)
        {
            return color1.R == color2.R && color1.G == color2.G && color1.B == color2.B;
        }
    }
}

