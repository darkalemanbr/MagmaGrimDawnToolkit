using System;

namespace GDLib.Arc
{
    /// <summary>
    /// Known storage modes used by Grim Dawn's ARC files.
    /// </summary>
    /// <remarks>
    /// There are, supposedly, other storage modes besides these but as of this writing I haven't seen
    /// a single entry in Grim Dawn ARC files with a mode other than 3 (LZ4). If an official modding
    /// toolset is ever released by Crate, I should check on it but for now these two should be enough.
    /// </remarks>
    public enum StorageMode
    {
        Plain = 1,
        Lz4Compressed = 3
    }
}
