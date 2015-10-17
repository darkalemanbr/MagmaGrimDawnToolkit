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
            public uint EntryCount;
            public uint ChunkCount;
            public uint ChunkIndexSize;
            public uint PathIndexSize;
            public uint FooterPointer;

            public static Header FromBytes(byte[] bytes)
            {
                if (bytes.Length != HeaderSize)
                    throw new ArgumentException(string.Format("The byte array must have length {0}.", HeaderSize), "bytes");

                var obj = new Header();

                using (var reader = new BinaryReader(new MemoryStream(bytes))) {
                    obj.EntryCount = reader.ReadUInt32();
                    obj.ChunkCount = reader.ReadUInt32();
                    obj.ChunkIndexSize = reader.ReadUInt32();
                    obj.PathIndexSize = reader.ReadUInt32();
                    obj.FooterPointer = reader.ReadUInt32();
                }

                return obj;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Entry
        {
            public uint StorageMode;
            public uint DataPointer;
            public uint CompressedSize;
            public uint PlainSize;
            public uint Adler32;
            public ulong FileTime;
            public uint ChunkCount;
            public uint ChunkOffset;
            public uint PathLength;
            public uint PathOffset;

            public static Entry FromBytes(byte[] bytes)
            {
                if (bytes.Length != EntrySize)
                    throw new ArgumentException(string.Format("The byte array must have length {0}.", EntrySize), "bytes");

                var obj = new Entry();

                using (var reader = new BinaryReader(new MemoryStream(bytes)))
                {
                    obj.StorageMode = reader.ReadUInt32();
                    obj.DataPointer = reader.ReadUInt32();
                    obj.CompressedSize = reader.ReadUInt32();
                    obj.PlainSize = reader.ReadUInt32();
                    obj.Adler32 = reader.ReadUInt32();
                    obj.FileTime = reader.ReadUInt64();
                    obj.ChunkCount = reader.ReadUInt32();
                    obj.ChunkOffset = reader.ReadUInt32();
                    obj.PathLength = reader.ReadUInt32();
                    obj.PathOffset = reader.ReadUInt32();
                }

                return obj;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Chunk
        {
            public uint DataOffset;
            public uint CompressedSize;
            public uint PlainSize;

            public static Chunk FromBytes(byte[] bytes)
            {
                if (bytes.Length != ChunkSize)
                    throw new ArgumentException(string.Format("The byte array must have length {0}.", ChunkSize), "bytes");

                var obj = new Chunk();

                using (var reader = new BinaryReader(new MemoryStream(bytes)))
                {
                    obj.DataOffset = reader.ReadUInt32();
                    obj.CompressedSize = reader.ReadUInt32();
                    obj.PlainSize = reader.ReadUInt32();
                }

                return obj;
            }
        }
    }
}
