using System;
using System.Runtime.InteropServices;
using System.IO;

namespace GDLib.Arc
{
    internal static class ArcStruct
    {
        public static readonly int HeaderSize;
        public static readonly int EntrySize;
        public static readonly int ChunkSize;

        static ArcStruct() {
            HeaderSize = Marshal.SizeOf<Header>();
            EntrySize = Marshal.SizeOf<Entry>();
            ChunkSize = Marshal.SizeOf<Chunk>();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Header
        {
            public int EntryCount;
            public int ChunkCount;
            public int ChunkIndexSize;
            public int PathIndexSize;
            public int FooterPointer;

            public static Header FromBytes(byte[] bytes)
            {
                if (bytes.Length != HeaderSize)
                    throw new ArgumentException(string.Format("The byte array must have length {0}.", HeaderSize), "bytes");

                var obj = new Header();

                using (var reader = new BinaryReader(new MemoryStream(bytes))) {
                    obj.EntryCount = reader.ReadInt32();
                    obj.ChunkCount = reader.ReadInt32();
                    obj.ChunkIndexSize = reader.ReadInt32();
                    obj.PathIndexSize = reader.ReadInt32();
                    obj.FooterPointer = reader.ReadInt32();
                }

                return obj;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Entry
        {
            public int StorageMode;
            public int DataPointer;
            public int CompressedSize;
            public int PlainSize;
            public int Adler32;
            public long FileTime;
            public int ChunkCount;
            public int ChunkOffset;
            public int PathLength;
            public int PathOffset;

            public static Entry FromBytes(byte[] bytes)
            {
                if (bytes.Length != EntrySize)
                    throw new ArgumentException(string.Format("The byte array must have length {0}.", EntrySize), "bytes");

                var obj = new Entry();

                using (var reader = new BinaryReader(new MemoryStream(bytes)))
                {
                    obj.StorageMode = reader.ReadInt32();
                    obj.DataPointer = reader.ReadInt32();
                    obj.CompressedSize = reader.ReadInt32();
                    obj.PlainSize = reader.ReadInt32();
                    obj.Adler32 = reader.ReadInt32();
                    obj.FileTime = reader.ReadInt64();
                    obj.ChunkCount = reader.ReadInt32();
                    obj.ChunkOffset = reader.ReadInt32();
                    obj.PathLength = reader.ReadInt32();
                    obj.PathOffset = reader.ReadInt32();
                }

                return obj;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Chunk
        {
            public int DataPointer;
            public int CompressedSize;
            public int PlainSize;

            public static Chunk FromBytes(byte[] bytes)
            {
                if (bytes.Length != ChunkSize)
                    throw new ArgumentException(string.Format("The byte array must have length {0}.", ChunkSize), "bytes");

                var obj = new Chunk();

                using (var reader = new BinaryReader(new MemoryStream(bytes)))
                {
                    obj.DataPointer = reader.ReadInt32();
                    obj.CompressedSize = reader.ReadInt32();
                    obj.PlainSize = reader.ReadInt32();
                }

                return obj;
            }
        }
    }
}
