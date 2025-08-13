namespace BrickVault
{
    public class ArchiveFile
    {
        public long Offset;
        public uint CompressedSize;
        public uint DecompressedSize;
        public uint CompressionType;

        private string path;
        public virtual string Path
        {
            get => path; set => path = value;
        }

        public string GetFormattedSize()
        {
            double size = DecompressedSize;
            string[] units = { "B", "KB", "MB", "GB" };
            int unitIndex = 0;

            while (size >= 1000 && unitIndex < units.Length - 1)
            {
                size /= 1000;
                unitIndex++;
            }

            return size >= 100 ? $"{(int)size} {units[unitIndex]}" : $"{size:F1} {units[unitIndex]}";
        }

        public void SetFileData(long offset, uint compressedSize, uint decompressedSize, byte compressionType)
        {
            Offset = offset;
            CompressedSize = compressedSize;
            DecompressedSize = decompressedSize;
            CompressionType = compressionType;
        }
    }
}
