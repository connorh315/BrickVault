namespace BrickVault
{
    public class ArchiveFile
    {
        public long Offset;
        public uint CompressedSize;
        public uint DecompressedSize;
        public uint CompressionType;

        public string Path;
    }
}
