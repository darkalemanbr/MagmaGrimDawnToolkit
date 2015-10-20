using System;
using System.Collections.Generic;
using System.IO;

namespace GDLib.Arc
{
    public class ArcEntry
    {
        internal Arc ParentArc;
        internal ArcStruct.Entry EntryStruct;
        internal string EntryPath;
        internal DateTime LastWrite;
        internal bool ShouldDelete;
        internal string MoveTo;

        public Arc Parent { get { return ParentArc; } }
        public string Path { get { return EntryPath; } }
        public StorageMode StorageMode { get { return (StorageMode)EntryStruct.StorageMode; } }
        public long CompressedSize { get { return EntryStruct.CompressedSize; } }
        public long PlainSize { get { return EntryStruct.PlainSize; } }
        public int Adler32 { get { return EntryStruct.Adler32; } }
        public DateTime LastWriteTime { get { return LastWrite; } }

        internal ArcEntry(Arc parent, ArcStruct.Entry entryStruct, string entryPath)
        {
            ParentArc = parent;
            EntryStruct = entryStruct;
            EntryPath = entryPath;
            LastWrite = DateTime.FromFileTime(EntryStruct.FileTime);

            ShouldDelete = false;
            MoveTo = String.Empty;
        }
    }
}
