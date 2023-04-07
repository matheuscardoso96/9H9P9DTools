
namespace LibDeImagensGbaDs.Formats.Indexed
{
    public interface IIndexedFormat
    {
        
        byte[] DecompressIndexes(byte[] file, ref byte[] AlphaValues);
        byte[] CompressIndexes(byte[] indices);
    }
}
