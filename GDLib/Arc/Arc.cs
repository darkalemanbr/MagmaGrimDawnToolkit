using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using GDLib.Utils;
using GDLib.Utils.Lz4;

namespace GDLib.Arc {
    public class Arc : IDisposable {
        #region Private Fields
        private Stream _stream;
        private bool _leaveOpen;

        private BinaryReader _reader;
        private BinaryWriter _writer;

        private ArcStruct.Header _header;
        private List<ArcEntry> _entries;
        private ReadOnlyCollection<ArcEntry> _entriesReadOnly;

        private int _chunkIndexPointer;
        private int _pathIndexPointer;
        private int _entryIndexPointer;
        #endregion

        #region Public Properties
        public bool IsReadOnly { get { return !_stream.CanWrite; } }
        public ReadOnlyCollection<ArcEntry> Entries { get { return _entriesReadOnly; } }
        #endregion

        #region Private Methods
        private DataBlock[]
            FindStrayBlocks() {
            var strayBlocks = new List<DataBlock> { };

            var allChunks = new List<ArcStruct.Chunk>(GetChunks(0, _header.ChunkCount));
            var disownedChunks = new List<ArcStruct.Chunk>(allChunks);

            // Find owners for the chunks
            foreach (var entry in _entries) {
                foreach (var chunk in entry.Chunks) {
                    foreach (var disChunk in disownedChunks) {
                        if (Equals(chunk, disChunk))
                            disownedChunks.Remove(disChunk);
                    }
                }
            }

            foreach (var disChunk in disownedChunks) {
                strayBlocks.Add(new DataBlock(disChunk.DataPointer, disChunk.CompressedSize));
            }

            // Find data not owned by any chunks
            var ownedBlocks = 

            return strayBlocks;
        }

        private 

        /// <summary>
        /// Checks if input has only one of the values set.
        /// </summary>
        /// <param name="input">The input value.</param>
        /// <param name="values">The values to check against.</param>
        /// <returns>Returns true if input has only one of the values, false otherwise.</returns>
        private bool EnumHasOnlyOne(Enum input, params Enum[] values) {
            if (values.Length < 1)
                return false;

            var hasFlag = false;

            foreach (var value in values) {
                if (input.HasFlag(value)) {
                    if (hasFlag)
                        return false;
                    else
                        hasFlag = true;
                }
            }

            return hasFlag;
        }

        private bool EnumIsValueValid(Enum value) {
            var chr = value.ToString()[0];

            if (char.IsDigit(chr) || chr == '-')
                return false;

            return true;
        }

        private void ThrowIfEntryNotOwned(ArcEntry entry) {
            if (!_entries.Contains(entry))
                throw new ArgumentOutOfRangeException("entry", "This Arc instance doesn't own that entry.");
        }

        private ArcStruct.Chunk[] GetEntryChunks(ArcEntry entry) {
            return GetChunks(entry.EntryStruct.ChunkOffset, entry.EntryStruct.ChunkCount);
        }

        private ArcStruct.Chunk[] GetChunks(long startOffset, int chunkCount) {
            // Check if we can even read those chunks
            if (startOffset + (ArcStruct.HeaderSize * chunkCount) > _header.ChunkIndexSize)
                throw new ArgumentOutOfRangeException("The requested chunks are out of the chunk index bounds.");

            _stream.Seek(_chunkIndexPointer + startOffset, SeekOrigin.Begin);
            var chunks = new ArcStruct.Chunk[chunkCount];
            for (int i = 0; i < chunkCount; ++i) {
                chunks[i] = ArcStruct.Chunk.FromBytesLE(_reader.ReadBytes(ArcStruct.ChunkSize));
            }

            return chunks;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Reads an entry in the archive and returns its data.
        /// </summary>
        /// <param name="entry">An entry owned by this archive.</param>
        /// <returns>The data read.</returns>
        public byte[] ReadEntry(ArcEntry entry) {
            ThrowIfDisposed();

            /*
             * Addendum -- Checking for corrupted data
             * 
             * I have decided to delegate the burden of checking if the data is really corrupted to the
             * applications using this library. The ArcEntry class provides the Adler32 checksum stored
             * in the ARC file and the Arc class provides a Checksum() method for that very reason.
             * Why? Because I believe the users should have the freedom to decide whether they want to accept
             * the corrupted data or not. Also, YOLO.
             */

            ThrowIfEntryNotOwned(entry);

            switch (entry.StorageMode) {
                // I have yet to see an entry with this type, but atom0s' code has it so I'll keep it...
                case StorageMode.Plain:
                    _stream.Seek(entry.EntryStruct.DataPointer, SeekOrigin.Begin);
                    return _reader.ReadBytes(entry.EntryStruct.PlainSize);

                case StorageMode.Lz4Compressed:
                    {
                        var plainDataOffset = 0;
                        var plainData = new byte[entry.EntryStruct.PlainSize];

                        foreach (var chunk in entry.Chunks) {
                            // Read the compressed chunk of data from the file
                            _stream.Seek(chunk.DataPointer, SeekOrigin.Begin);
                            _reader.ReadBytes(chunk.CompressedSize);

                            // Decompress it
                            var decompressedChunk = new byte[chunk.PlainSize];
                            Lz4.DecompressSafe(_reader.ReadBytes(chunk.CompressedSize), decompressedChunk, chunk.CompressedSize, chunk.PlainSize);

                            // Append it
                            Buffer.BlockCopy(decompressedChunk, 0, plainData, plainDataOffset, chunk.PlainSize);

                            // Move the offset ahead
                            plainDataOffset += chunk.PlainSize;

                            // Explicitly allow the GC to collect it
                            decompressedChunk = null;
                        }

                        return plainData;
                    }

                default:
                    throw new InvalidDataException("The entry has been stored using an unsupported mode.");
            }
        }

        /// <summary>
        /// Marks an entry to be deleted when <see cref="Arc.WriteChanges"/> is called.
        /// </summary>
        /// <param name="entry">An entry owned by this archive.</param>
        public void DeleteEntry(ArcEntry entry) {
            ThrowIfDisposed();

            ThrowIfEntryNotOwned(entry);

            entry.ShouldDelete = true;
        }

        public void RestoreEntry(ArcEntry entry) {
            ThrowIfDisposed();

            ThrowIfEntryNotOwned(entry);

            entry.ShouldDelete = false;
        }

        /// <summary>
        /// Marks an entry to be moved to a new location when <see cref="Arc.WriteChanges"/> is called.
        /// </summary>
        /// <param name="entry">An entry owned by this archive.</param>
        /// <param name="newLocation">
        /// The new path of the entry.
        /// 
        /// To be valid it must be:
        /// - Absolute and canonical, like "/my/stored/file.ext" (file extension is not required)
        /// - Contain only non-extended ASCII characters, except for these: [control characters] : * ? " < > |
        /// </param>
        /// <exception cref="ArgumentException">Throws if the new path is invalid.</exception>
        public void MoveEntry(ArcEntry entry, string newLocation) {
            ThrowIfDisposed();

            ThrowIfEntryNotOwned(entry);

            newLocation = newLocation.Trim();

            if (!PathUtils.EntryAbsolutePathRegex.IsMatch(newLocation))
                throw new ArgumentException("The specified path is invalid.", "newLocation");

            if (newLocation != entry.Path)
                entry.MoveTo = newLocation;
        }

        public void WriteChanges(
            WritePolicy writePolicy = WritePolicy.OverwriteStrayBlocks,
            DeletePolicy deletePolicy = DeletePolicy.Strip,
            int maxAllocWhenStrip = 256 * 1024 * 1024 // 256 MiB
        ) {
            ThrowIfDisposed();

            if (!EnumIsValueValid(writePolicy))
                throw new ArgumentOutOfRangeException("createPolicy", "Specified value is not a valid CreatePolicy.");

            if (!EnumIsValueValid(deletePolicy))
                throw new ArgumentOutOfRangeException("deletePolicy", "Specified value is not a valid DeletePolicy.");

            if (!EnumHasOnlyOne(writePolicy, WritePolicy.OverwriteStrayBlocks, WritePolicy.DontOverwriteStrayBlocks))
                throw new ArgumentException("CreatePolicy.OverwriteStrayBlocks and CreatePolicy.DontOverwriteStrayBlocks must be mutually exclusive.", "createPolicy");

            if (!EnumHasOnlyOne(deletePolicy, DeletePolicy.Unlink, DeletePolicy.Strip, DeletePolicy.Zerofill))
                throw new ArgumentException("DeletePolicy.Unlink, DeletePolicy.Strip and DeletePolicy.Zerofill must be mutually exclusive.", "deletePolicy");

            using (var footerWriter = new BinaryWriter(new MemoryStream(), Encoding.ASCII))
            using (var pathWriter = new BinaryWriter(new MemoryStream(), Encoding.ASCII))
            using (var chunkWriter = new BinaryWriter(new MemoryStream(), Encoding.ASCII))
            using (var entryWriter = new BinaryWriter(new MemoryStream(), Encoding.ASCII)) {
                var freedChunks = new List<ArcStruct.Chunk> { };
                int totalDataSize = 0;

                foreach (var entry in _entries) {
                    totalDataSize += entry.EntryStruct.CompressedSize;

                    if (!entry.ShouldDelete) {
                        var path = entry.MoveTo != "" ? entry.MoveTo : entry.EntryPath;
                        var pathOffset = pathWriter.BaseStream.Position;
                        var pathBytes = Encoding.ASCII.GetBytes(path + '\0');
                        pathWriter.Write(pathBytes);

                        entry.EntryStruct.PathOffset = (int)pathOffset;
                        entry.EntryStruct.PathLength = pathBytes.Length - 1; // -1 for the null terminator
                        entry.EntryPath = path;
                        entry.MoveTo = "";

                        var chunkOffset = chunkWriter.BaseStream.Position;
                        foreach (var chunk in entry.Chunks) {
                            chunkWriter.Write(chunk.ToBytesLE());
                        }

                        entry.EntryStruct.ChunkOffset = (int)chunkOffset;

                        entryWriter.Write(entry.EntryStruct.ToBytesLE());
                    }
                    else {
                        if (deletePolicy.HasFlag(DeletePolicy.Unlink)) {
                            // Keep chunk data only
                            foreach (var chunk in entry.Chunks) {
                                chunkWriter.Write(chunk.ToBytesLE());
                            }
                        }
                        else if (
                            deletePolicy.HasFlag(DeletePolicy.Strip) ||
                            deletePolicy.HasFlag(DeletePolicy.Zerofill)
                        ) {
                            freedChunks.AddRange(entry.Chunks);
                        }
                    }
                }

                /*
                // Get the ranges of the data we want to keep
                var dataRanges = new List<int[]> { };
                dataRanges.Add(new int[] {
                    0,
                    freedChunks[0].DataPointer
                });

                for (int i = 0; i < freedChunks.Count - 1; ++i) {
                    dataRanges.Add(new int[] {
                        freedChunks[i].DataPointer + freedChunks[i].CompressedSize,
                        freedChunks[i + 1].DataPointer
                    });
                }

                var lastFreedChunk = freedChunks[freedChunks.Count - 1];
                var lastFreedChunkEnd = lastFreedChunk.DataPointer + lastFreedChunk.CompressedSize;
                if (lastFreedChunkEnd != _header.FooterPointer) {
                    dataRanges.Add(new int[] {
                        lastFreedChunkEnd,
                        _header.FooterPointer
                    });
                }

                if (deletePolicy.HasFlag(DeletePolicy.Strip)) {
                    byte[] tmpbuf;

                    int writePointer = dataRanges[0][1];

                    for (int i = 1; i < dataRanges.Count; ++i) {
                        // The size of the data chunk
                        var dataSize = dataRanges[i][1] - dataRanges[i][0];
                        var remainingSize = dataSize;

                        // Write data as many times as needed so it doesn't exceed maxAllocWhenStrip
                        while (remainingSize > 0) {
                            var readLen = Math.Min(remainingSize, maxAllocWhenStrip);

                            _stream.Seek(dataRanges[i][0], SeekOrigin.Begin);
                            tmpbuf = _reader.ReadBytes(readLen);

                            _stream.Seek(writePointer, SeekOrigin.Begin);
                            _writer.Write(tmpbuf);

                            writePointer += readLen;
                            remainingSize -= readLen;

                            tmpbuf = null;
                        }
                    }
                    _header.FooterPointer = writePointer;
                }
                else if (deletePolicy.HasFlag(DeletePolicy.Zerofill)) {
                    for (int i = 0; i < dataRanges.Count; ++i) {
                        _stream.Seek(dataRanges[i][1], SeekOrigin.Begin);

                        for (int c = 0; c < dataRanges[i + 1][0] - dataRanges[i][1]; ++c) {
                            _writer.Write('\0');
                        }
                    }
                }
                */

                if (deletePolicy.HasFlag(DeletePolicy.Strip)) {
                    
                }
                else if (deletePolicy.HasFlag(DeletePolicy.Zerofill)) {

                }

                // Write header
            }
        }
        #endregion

        #region Class Constructor
        public Arc(Stream stream) : this(stream, false) { }
        public Arc(Stream stream, bool leaveOpen) {
            Init(stream, leaveOpen);

            Validate();
            Parse();
        }

        private void Init(Stream stream, bool leaveOpen) {
            if (!stream.CanRead)
                throw new NotSupportedException("The specified stream is not readable.");

            if (!stream.CanSeek)
                throw new NotSupportedException("The specified stream is not seekable.");

            _stream = stream;
            _leaveOpen = leaveOpen;

            _reader = new BinaryReader(stream, Encoding.ASCII, true);
            if (_stream.CanWrite)
                _writer = new BinaryWriter(stream, Encoding.ASCII, true);

            _entries = new List<ArcEntry> { };
            _entriesReadOnly = _entries.AsReadOnly();
        }

        private void Validate() {
            if (_stream.Length < 2048)
                throw new InvalidDataException("The specified file has less than 2048 bytes.");

            _stream.Seek(0, SeekOrigin.Begin);

            //if (_reader.ReadInt32() != 0x435241)
            if (new string(_reader.ReadChars(4)) != "ARC\0")
                throw new InvalidDataException("The magic number of the specified file is invalid.");

            var version = _reader.ReadInt32();
            if (version != 3)
                throw new InvalidDataException(String.Format("The version of the specified ARC file ({0}) is not supported.", version));
        }

        private void Parse() {
            _stream.Seek(8, SeekOrigin.Begin);
            _header = ArcStruct.Header.FromBytesLE(_reader.ReadBytes(ArcStruct.HeaderSize));

            _chunkIndexPointer = _header.FooterPointer;
            _pathIndexPointer = _chunkIndexPointer + _header.ChunkIndexSize;
            _entryIndexPointer = _pathIndexPointer + _header.PathIndexSize;

            for (int i = 0; i < _header.EntryCount; ++i) {
                _stream.Seek(_entryIndexPointer + (i * ArcStruct.EntrySize), SeekOrigin.Begin);
                var entryStruct = ArcStruct.Entry.FromBytesLE(_reader.ReadBytes(ArcStruct.EntrySize));

                _stream.Seek(_pathIndexPointer + entryStruct.PathOffset, SeekOrigin.Begin);
                var entryPath = new string(_reader.ReadChars(entryStruct.PathLength));

                _entries.Add(new ArcEntry(
                    this, entryStruct, entryPath, GetChunks(entryStruct.ChunkOffset, entryStruct.ChunkCount
                )));
            }
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).

                    if (_writer != null)
                        _writer.Dispose();
                    if (_reader != null)
                        _reader.Dispose();
                    if (!_leaveOpen && _stream != null)
                        _stream.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                _stream = null;
                _reader = null;
                _writer = null;
                _entries = null;

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~Arc() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed() {
            if (disposedValue)
                throw new ObjectDisposedException(this.GetType().ToString());
        }
        #endregion
    }
}
