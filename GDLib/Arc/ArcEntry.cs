using System;
using System.Collections.Generic;

namespace GDLib.Arc
{
    public class ArcEntry
    {
        internal Arc _ParentArc;
        internal ArcStruct.Entry _EntryStruct;
        internal string _EntryPath;
        internal DateTime _LastWrite;

        public Arc Parent { get { return _ParentArc; } }
        public string Path { get { return _EntryPath; } }
        public ArcStorageMode StorageMode { get { return (ArcStorageMode)_EntryStruct.StorageMode; } }
        public long CompressedSize { get { return _EntryStruct.CompressedSize; } }
        public long PlainSize { get { return _EntryStruct.PlainSize; } }
        public uint Adler32 { get { return _EntryStruct.Adler32; } }
        public DateTime LastWriteTime { get { return _LastWrite; } }

        internal ArcEntry(Arc parent, ArcStruct.Entry entryStruct, string entryPath)
        {
            _ParentArc = parent;
            _EntryStruct = entryStruct;
            _EntryPath = entryPath;
            _LastWrite = DateTime.FromFileTime((long)_EntryStruct.FileTime);
        }
    }
}
