using System;
using System.Runtime.InteropServices;

namespace GDLib.Utils.Lz4 {
    /// <summary>
    /// Primitive bindings for liblz4.
    /// </summary>
    public static class Lz4 {
        public const int MaxInputSize = int.MaxValue;

        public static int CompressBound(int isize) {
            return isize + (isize / 255) + 16;
        }

        [DllImport("lz4.dll",
            CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "LZ4_compress_default")]
        public static extern int CompressDefault([In] byte[] source, [Out] byte[] dest,
            int sourceSize, int maxDestSize);

        [DllImport("lz4.dll",
            CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "LZ4_decompress_safe")]
        public static extern int DecompressSafe([In] byte[] source, [Out] byte[] dest,
            int compressedSize, int maxDecompressedSize);
    }
}
