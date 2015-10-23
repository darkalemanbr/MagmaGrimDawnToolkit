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

        private static byte[] GetBytesLE(uint value) {
            var bytes = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return bytes;
        }

        private static byte[] GetBytesLE(ulong value) {
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
            public uint EntryCount;
            public uint ChunkCount;
            public uint ChunkIndexSize;
            public uint PathIndexSize;
            public uint FooterPointer;

            public static Header FromBytesLE(byte[] bytes) {
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
            public uint StorageMode;
            public uint DataPointer;
            public uint CompressedSize;
            public uint PlainSize;
            public uint Adler32;
            public long FileTime;
            public uint ChunkCount;
            public uint ChunkOffset;
            public uint PathLength;
            public uint PathOffset;

            public static Entry FromBytesLE(byte[] bytes) {
                if (bytes.Length != EntrySize)
                    throw new ArgumentException(string.Format("The byte array must have length {0}.", EntrySize), "bytes");

                var obj = new Entry();

                using (var reader = new BinaryReader(new MemoryStream(bytes))) {
                    obj.StorageMode = reader.ReadUInt32();
                    obj.DataPointer = reader.ReadUInt32();
                    obj.CompressedSize = reader.ReadUInt32();
                    obj.PlainSize = reader.ReadUInt32();
                    obj.Adler32 = reader.ReadUInt32();
                    obj.FileTime = reader.ReadInt64();
                    obj.ChunkCount = reader.ReadUInt32();
                    obj.ChunkOffset = reader.ReadUInt32();
                    obj.PathLength = reader.ReadUInt32();
                    obj.PathOffset = reader.ReadUInt32();
                }

                return obj;
            }

            public byte[] ToBytesLE() {
                var bytes = new byte[EntrySize];

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
            public uint DataPointer;
            public uint CompressedSize;
            public uint PlainSize;

            public static Chunk FromBytesLE(byte[] bytes) {
                if (bytes.Length != ChunkSize)
                    throw new ArgumentException(string.Format("The byte array must have length {0}.", ChunkSize), "bytes");

                var obj = new Chunk();

                using (var reader = new BinaryReader(new MemoryStream(bytes))) {
                    obj.DataPointer = reader.ReadUInt32();
                    obj.CompressedSize = reader.ReadUInt32();
                    obj.PlainSize = reader.ReadUInt32();
                }

                return obj;
            }

            public byte[] ToBytesLE() {
                var bytes = new byte[ChunkSize];

                Buffer.BlockCopy(GetBytesLE(DataPointer), 0, bytes, 0, 4);
                Buffer.BlockCopy(GetBytesLE(CompressedSize), 0, bytes, 4, 4);
                Buffer.BlockCopy(GetBytesLE(PlainSize), 0, bytes, 8, 4);

                return bytes;
            }
        }
    }
}
