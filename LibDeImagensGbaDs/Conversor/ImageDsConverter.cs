using LibDeImagensGbaDs.Enums;
using LibDeImagensGbaDs.Formats.Indexed;
using LibDeImagensGbaDs.Paleta;
using LibDeImagensGbaDs.Sprites;
using LibDeImagensGbaDs.TileMap;
using System.Collections.Generic;
using System.Drawing;

namespace LibDeImagensGbaDs.Conversor
{
    public static class ImageDsConverter
    {
        public static Dictionary<ColorDepth,IIndexedFormat> _indexedFormatConverters;
        private static bool _isConvertersInitialized;

        public static Bitmap RawIndexedToBitmap(byte[] file, int width, int height, BGR565 pal, TileMode tileMode, ColorDepth colorDepth)
        {
            if (!_isConvertersInitialized)
            {
                InitilizeConverters();
            }
            byte[] alphaValues = null;
            byte[] indexes = _indexedFormatConverters[colorDepth].DecompressIndexes(file, ref alphaValues);
            if (tileMode == TileMode.Tiled)
            {
                return ImageTypeConverter.TiledToBmp(indexes, width, height, pal, null, alphaValues);
            }
            else
            {
                return ImageTypeConverter.NotTiledToBmp(indexes, width, height, pal, alphaValues);
            }
            
        }

        public static Bitmap TileMappedToBitmap(byte[] file, List<ushort> tilemap, int width, int height, BGR565 pal, ColorDepth colorDepth)
        {
            if (!_isConvertersInitialized)
            {
                InitilizeConverters();
            }
            byte[] alphaValues = null;
            byte[] indexes = _indexedFormatConverters[colorDepth].DecompressIndexes(file, ref alphaValues);
            return ImageTypeConverter.TiledToBmp(indexes, width, height, pal, tilemap, alphaValues);
        }
        public static Bitmap SpriteToBitmap(byte[] tiles, byte[] pal, TypeSprite oams, TileMode tileMode, ColorDepth colorDepth)
        {
            if (!_isConvertersInitialized)
            {
                InitilizeConverters();
            }
            byte[] alphaValues = null;
           // byte[] indexes = _indexedFormatConverters[colorDepth].DecompressIndexes(tiles, ref alphaValues);
            return ImageTypeConverter.SpriteFrameToBmp(tiles, pal, oams, tileMode, colorDepth);
        }

        public static byte[] BitmapToRawIndexed(Bitmap image, BGR565 pal, TileMode tileMode, ColorDepth colorDepth)
        {

            if (!_isConvertersInitialized)
            {
                InitilizeConverters();
            }
            byte[] alphaValues = null;
            byte[] uncompressedIndexes;

            if (tileMode == TileMode.Tiled)
            {
                uncompressedIndexes = ImageTypeConverter.GenerateTiledIndices(image, pal, null, false);
            }
            else
            {
                uncompressedIndexes = ImageTypeConverter.GenerateNotTiledToBmp(image, pal);
            }

            byte[] indexes = _indexedFormatConverters[colorDepth].CompressIndexes(uncompressedIndexes);
            return indexes;
        }

        public static byte[] BitmapToTileMapped(Bitmap image, BGR565 pal, TileMapType tileMap, ColorDepth colorDepth)
        {
            if (!_isConvertersInitialized)
            {
                InitilizeConverters();
            }
            byte[] alphaValues = null;
            byte[] uncompressedIndexes = ImageTypeConverter.GenerateTiledIndices(image, pal, tileMap, true);
            return _indexedFormatConverters[colorDepth].CompressIndexes(uncompressedIndexes);
        }

        

        //public static Bitmap SpriteToBimap(byte[] file, List<Oam> oamAttributes)
        //{
        //    return Conversor.ConvertaParaBmp(arquivo, tamanho, enderecoInicial);
        //}

        //public static Bitmap DirectMode(byte[] file, int width, int height, int startAddress)
        //{
        //    return Conversor.ConvertaParaBmp(arquivo, tamanho, enderecoInicial);
        //}

        //public static List<object> ImageParaBin(Bitmap imagem)
        //{
        //    return Conversor.ConvertaParaBin(imagem);
        //}


        private static void InitilizeConverters() 
        {
            _isConvertersInitialized = true;
            _indexedFormatConverters = new Dictionary<ColorDepth, IIndexedFormat>();
            _indexedFormatConverters.Add(ColorDepth.F1BBP, new F1BPP());
            _indexedFormatConverters.Add(ColorDepth.F2BBP, new F2BPP());
            _indexedFormatConverters.Add(ColorDepth.F4BBP, new F4BPP());
            _indexedFormatConverters.Add(ColorDepth.F8BBP, new F8BPP());
            _indexedFormatConverters.Add(ColorDepth.FA3I5, new FA3I5());
            _indexedFormatConverters.Add(ColorDepth.FA5I3, new FA5I3());
        }

    }
}
