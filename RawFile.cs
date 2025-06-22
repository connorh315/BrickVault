using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrickVault
{
    public class RawFile
    {
        public Stream fileStream;
        public RawFile(Stream stream) { fileStream = stream; }

        public string FileLocation { get; private set; }

        public RawFile(string fileLocation)
        {
            fileStream = File.OpenRead(fileLocation);
            FileLocation = fileLocation;
        }

        public RawFile CreateView()
        {
            return new RawFile(FileLocation);
        }

        public long Position => fileStream.Position;

        public long Seek(long offset, SeekOrigin origin) => fileStream.Seek(offset, origin);

        private byte[] ReadBlock(int size, bool bigEndian)
        {
            byte[] data = new byte[size];
            fileStream.Read(data, 0, size);
            if (bigEndian)
            {
                Array.Reverse(data);
            }
            return data;
        }

        public byte ReadByte()
        {
            return (byte)fileStream.ReadByte();
        }

        public short ReadShort(bool bigEndian = false)
        {
            return BitConverter.ToInt16(ReadBlock(2, bigEndian));
        }

        public ushort ReadUShort(bool bigEndian = false)
        {
            return BitConverter.ToUInt16(ReadBlock(2, bigEndian));
        }

        public int ReadInt(bool bigEndian = false)
        {
            return BitConverter.ToInt32(ReadBlock(4, bigEndian));
        }

        public uint ReadUInt(bool bigEndian = false)
        {
            return BitConverter.ToUInt32(ReadBlock(4, bigEndian));
        }

        public long ReadLong(bool bigEndian = false)
        {
            return BitConverter.ToInt64(ReadBlock(8, bigEndian));
        }

        public ulong ReadULong(bool bigEndian = false)
        {
            return BitConverter.ToUInt64(ReadBlock(8, bigEndian));
        }

        public string ReadString(int length)
        {
            byte[] array = new byte[length];
            fileStream.Read(array, 0, length);
            if (array[array.Length - 1] == 0)
            {
                return Encoding.Default.GetString(array, 0, array.Length - 1);
            }
            return Encoding.Default.GetString(array);
        }

        public string ReadNullString()
        {
            string combined = "";
            while (true)
            {
                byte currByte = (byte)fileStream.ReadByte();
                if (currByte == 0) break;

                combined += (char)currByte;
            }

            return combined;
        }

        /// <summary>
        /// Reads a pascal string with a SHORT preceding it
        /// </summary>
        /// <param name="bigEndian"></param>
        /// <param name="security"></param>
        /// <returns></returns>
        /// <exception cref="DataMisalignedException"></exception>
        public string ReadPascalString(bool bigEndian = true, ushort security = 256)
        {
            ushort length = ReadUShort(true);
            if (length > security)
            {
                Console.WriteLine("Attempting to read string of length {0} at position {1}!", length, Position);
                throw new DataMisalignedException("Potential bad string quashed");
            }

            return ReadString(length);
        }

        public void ReadInto(byte[] destination, int size)
        {
            fileStream.Read(destination, 0, size);
        }
    }
}
