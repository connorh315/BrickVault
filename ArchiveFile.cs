namespace BrickVault
{
    public class ArchiveFile
    {
        public long Offset;
        public uint CompressedSize;
        public uint DecompressedSize;
        public uint CompressionType;

        public string Path;

        public string GetFormattedSize()
        {
            double size = DecompressedSize;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int unitIndex = 0;

            while (size >= 1000 && unitIndex < units.Length - 1)
            {
                size /= 1000;
                unitIndex++;
            }

            return size >= 100 ? $"{(int)size} {units[unitIndex]}" : $"{size:F1} {units[unitIndex]}";
        }
    }
}
