using System;
using System.Runtime.InteropServices;
using System.IO;

namespace GDLib.Arc {
    internal static class ArcStruct {
        public static readonly int HeaderSize;
        public static readonly int EntrySize;
        public static readonly int ChunkSize;

        private static byte[] GetBytesLE(int value) {
            var bytes = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return bytes;
        }

        private static byte[] GetBytesLE(long value) {
            var bytes = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return bytes;
        }

        static ArcStruct() {
            HeaderSize = Marshal.SizeOf<Header>();
            EntrySize = Marshal.SizeOf<Entry>();
            ChunkSize = Marshal.SizeOf<Chunk>();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Header {
            public int EntryCount;
            public int ChunkCount;
            public int ChunkIndexSize;
            public int PathIndexSize;
            public int FooterPointer;

            public static Header FromBytesLE(byte[] bytes) {
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

            public byte[] ToBytesLE() {
                var bytes = new byte[HeaderSize];

                Buffer.BlockCopy(GetBytesLE(EntryCount), 0, bytes, 0, 4);
                Buffer.BlockCopy(GetBytesLE(ChunkCount), 0, bytes, 4, 4);
                Buffer.BlockCopy(GetBytesLE(ChunkIndexSize), 0, bytes, 8, 4);
                Buffer.BlockCopy(GetBytesLE(PathIndexSize), 0, bytes, 12, 4);
                Buffer.BlockCopy(GetBytesLE(FooterPointer), 0, bytes, 16, 4);

                return bytes;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Entry {
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

            public static Entry FromBytesLE(byte[] bytes) {
                if (bytes.Length != EntrySize)
                    throw new ArgumentException(string.Format("The byte array must have length {0}.", EntrySize), "bytes");

                var obj = new Entry();

                using (var reader = new BinaryReader(new MemoryStream(bytes))) {
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

            public byte[] ToBytesLE() {
                var bytes = new byte[HeaderSize];

                Buffer.BlockCopy(GetBytesLE(StorageMode), 0, bytes, 0, 4);
                Buffer.BlockCopy(GetBytesLE(DataPointer), 0, bytes, 4, 4);
                Buffer.BlockCopy(GetBytesLE(CompressedSize), 0, bytes, 8, 4);
                Buffer.BlockCopy(GetBytesLE(PlainSize), 0, bytes, 12, 4);
                Buffer.BlockCopy(GetBytesLE(Adler32), 0, bytes, 16, 4);
                Buffer.BlockCopy(GetBytesLE(FileTime), 0, bytes, 20, 4);
                Buffer.BlockCopy(GetBytesLE(ChunkCount), 0, bytes, 28, 4);
                Buffer.BlockCopy(GetBytesLE(ChunkOffset), 0, bytes, 32, 4);
                Buffer.BlockCopy(GetBytesLE(PathLength), 0, bytes, 36, 4);
                Buffer.BlockCopy(GetBytesLE(PathOffset), 0, bytes, 40, 4);

                return bytes;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Chunk {
            public int DataPointer;
            public int CompressedSize;
            public int PlainSize;

            public static Chunk FromBytesLE(byte[] bytes) {
                if (bytes.Length != ChunkSize)
                    throw new ArgumentException(string.Format("The byte array must have length {0}.", ChunkSize), "bytes");

                var obj = new Chunk();

                using (var reader = new BinaryReader(new MemoryStream(bytes))) {
                    obj.DataPointer = reader.ReadInt32();
                    obj.CompressedSize = reader.ReadInt32();
                    obj.PlainSize = reader.ReadInt32();
                }

                return obj;
            }

            public byte[] ToBytesLE() {
                var bytes = new byte[HeaderSize];

                Buffer.BlockCopy(GetBytesLE(DataPointer), 0, bytes, 0, 4);
                Buffer.BlockCopy(GetBytesLE(CompressedSize), 0, bytes, 4, 4);
                Buffer.BlockCopy(GetBytesLE(PlainSize), 0, bytes, 8, 4);

                return bytes;
            }
        }
    }
}
