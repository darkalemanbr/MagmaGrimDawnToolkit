using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using GDLib.Utils;
using GDLib.Utils.Lz4;

namespace GDLib.Arc {
    public class Arc : IDisposable {
        private const int DEFAULT_MAX_ALLOC = 256 * 1024 * 1024;

        private const uint ADLER32 = 65521;

        #region Private Fields
        private readonly object _lock = new object();

        private Stream _stream;
        private bool _leaveOpen;

        private BinaryReader _reader;
        private BinaryWriter _writer;

        private int _maxAlloc;

        private ArcStruct.Header _header;
        private List<ArcEntry> _entries;
        private ReadOnlyCollection<ArcEntry> _entriesReadOnly;

        private uint _chunkIndexPointer;
        private uint _pathIndexPointer;
        private uint _entryIndexPointer;
        #endregion

        #region Public Properties
        public bool IsReadOnly { get { return !_stream.CanWrite; } }
        public ReadOnlyCollection<ArcEntry> Entries { get { return _entriesReadOnly; } }
        public int MaxAlloc {
            get { return _maxAlloc; }
            set {
                lock (_lock) {
                    _maxAlloc = value;
                }
            }
        }
        #endregion

        #region Private Methods
        private void UpdateMeta(uint footerStart) {
            uint chunkCount = 0;
            uint pathIndexSize = 0;

            _stream.Seek(footerStart, SeekOrigin.Begin);

            // Write all chunks structs
            foreach (var entry in _entries) {
                entry.EntryStruct.ChunkOffset = chunkCount;
                entry.EntryStruct.ChunkCount = (uint)entry.Chunks.Length;

                foreach (var chunk in entry.Chunks) {
                    _writer.Write(chunk.ToBytesLE());

                    chunkCount++;
                }
            }

            foreach (var entry in _entries) {
                entry.EntryStruct.PathOffset = pathIndexSize;
                entry.EntryStruct.PathLength = (uint)entry.EntryPath.Length;

                _writer.Write(Encoding.ASCII.GetBytes(entry.EntryPath + '\0'));

                pathIndexSize += (uint)(entry.EntryPath.Length + 1);
            }

            foreach (var entry in _entries) {
                _writer.Write(entry.EntryStruct.ToBytesLE());
            }

            _header.EntryCount = (uint)_entries.Count;
            _header.ChunkCount = chunkCount;
            _header.ChunkIndexSize = chunkCount * (uint)ArcStruct.ChunkSize;
            _header.PathIndexSize = pathIndexSize;
            _header.FooterPointer = footerStart;

            _stream.Seek(8, SeekOrigin.Begin);
            _writer.Write(_header.ToBytesLE());
        }

        private void MoveData(uint frm, uint len, uint to) {
            if (
                frm > _stream.Length - 1 ||
                len < 1 || frm + len > _stream.Length ||
                to > _stream.Length - 1
            )
                throw new ArgumentOutOfRangeException();

            var buf = new byte[Math.Min(len, _maxAlloc)];

            while (len > 0) {
                var readLen = (uint)Math.Min(len, buf.Length);

                _stream.Seek(frm, SeekOrigin.Begin);
                _reader.Read(buf, 0, (int)readLen);

                _stream.Seek(to, SeekOrigin.Begin);
                _stream.Write(buf, 0, (int)readLen);

                frm += readLen;
                to += readLen;
                len -= readLen;
            }

            buf = null;
        }

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

        private void ThrowIfReadOnly() {
            if (!_stream.CanWrite)
                throw new NotSupportedException("Stream is in read-only mode.");
        }

        private void ThrowIfEntryNotOwned(ArcEntry entry) {
            if (!_entries.Contains(entry))
                throw new ArgumentOutOfRangeException("entry", "This Arc instance doesn't own that entry.");
        }

        private ArcStruct.Chunk[] GetEntryChunks(ArcEntry entry) {
            return GetChunks(entry.EntryStruct.ChunkOffset, entry.EntryStruct.ChunkCount);
        }

        private ArcStruct.Chunk[] GetChunks(uint startOffset, uint chunkCount) {
            // Check if we can even read those chunks
            if (startOffset + (ArcStruct.ChunkSize * chunkCount) > _header.ChunkIndexSize)
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
        /// <exception cref="ArgumentOutOfRangeException">The specified entry is not owned by this archive.</exception>
        /// <exception cref="InvalidDataException">The entry has been stored using an unsupported mode.</exception>
        public byte[] ReadEntry(ArcEntry entry) {
            lock (_lock) {
                ThrowIfDisposed();
                ThrowIfEntryNotOwned(entry);

                /*
                 * Addendum -- Checking for corrupted data
                 * 
                 * I have decided to delegate the burden of checking if the data is really corrupted to the
                 * applications using this library. The ArcEntry class provides the Adler32 checksum stored
                 * in the ARC file and the Arc class provides a Checksum() method for that very reason.
                 * Why? Because I believe the users should have the freedom to decide whether they want to accept
                 * the corrupted data or not. Also, YOLO.
                 */

                switch (entry.StorageMode) {
                    // I have yet to see an entry with this type, but atom0s' code has it so I'll keep it...
                    case StorageMode.Plain:
                        _stream.Seek(entry.EntryStruct.DataPointer, SeekOrigin.Begin);
                        return _reader.ReadBytes((int)entry.EntryStruct.PlainSize);

                    case StorageMode.Lz4Compressed:
                        using (var plainData = new MemoryStream(new byte[entry.PlainSize], 0, entry.PlainSize, true, true)) {
                            foreach (var chunk in entry.Chunks) {
                                // Read the compressed chunk of data from the file
                                _stream.Seek(chunk.DataPointer, SeekOrigin.Begin);

                                // Decompress it
                                var decompressedChunk = new byte[chunk.PlainSize];
                                Lz4.DecompressSafe(_reader.ReadBytes((int)chunk.CompressedSize), decompressedChunk, (int)chunk.CompressedSize, (int)chunk.PlainSize);
                                plainData.Write(decompressedChunk, (int)plainData.Position, (int)chunk.PlainSize);

                                decompressedChunk = null;
                            }

                            return plainData.GetBuffer();
                        }

                    default:
                        throw new InvalidDataException("The entry has been stored using an unsupported mode.");
                }
            }
        }

        /// <summary>
        /// Removes an entry from the archive permanently.
        /// </summary>
        /// <param name="entry">An entry owned by this archive.</param>
        /// <exception cref="ArgumentOutOfRangeException">The specified entry is not owned by this archive.</exception>
        public void DeleteEntry(ArcEntry entry) {
            lock (_lock) {
                ThrowIfDisposed();
                ThrowIfReadOnly();
                ThrowIfEntryNotOwned(entry);

                uint totalStripped = 0;

                if (entry.EntryStruct.CompressedSize > 0) {
                    // Step 1: strip entry data
                    foreach (var chunk in entry.Chunks) {
                        var untouched = false;

                        // Check if the current chunk is not owned by any other entry
                        foreach (var otherEntry in _entries) {
                            if (ReferenceEquals(otherEntry, entry))
                                continue;

                            foreach (var otherChunk in otherEntry.Chunks) {
                                if (Equals(otherChunk.DataPointer, chunk.DataPointer)) {
                                    untouched = true;
                                    break;
                                }
                            }

                            if (untouched)
                                break;
                        }

                        // If so, leave it alone
                        if (untouched)
                            continue;

                        var frm = chunk.DataPointer + chunk.CompressedSize;
                        var len = (int)_stream.Length - frm;
                        MoveData(frm, (uint)len, chunk.DataPointer);

                        totalStripped += chunk.CompressedSize;
                    }

                    /* OBSOLETE
                    // Strip chunks from the index
                    if (entry.EntryStruct.ChunkCount > 0) {
                        var frm = _entryIndexPointer +
                            entry.EntryStruct.ChunkOffset +
                            (entry.EntryStruct.ChunkCount * ArcStruct.ChunkSize);
                        var len = (int)_stream.Length - frm;
                        MoveData(frm, len, _entryIndexPointer + entry.EntryStruct.ChunkOffset);
                    }
                    */

                    // Step 2: update all remaining entries
                    for (int i = 0; i < _entries.Count; ++i) {
                        if (ReferenceEquals(_entries[i], entry))
                            continue;

                        if (totalStripped > 0) {
                            if (_entries[i].Chunks.Length > 0) {
                                for (int c = 0; c < _entries[i].Chunks.Length; ++c) {
                                    if (_entries[i].Chunks[c].DataPointer > _entries[i].Chunks[c].DataPointer) {
                                        _entries[i].Chunks[c].DataPointer -= totalStripped;
                                    }
                                }

                                _entries[i].EntryStruct.DataPointer = _entries[i].Chunks[0].DataPointer;
                            }
                            else
                                _entries[i].EntryStruct.DataPointer -= totalStripped;
                        }
                    }
                }

                _entries.Remove(entry);
                entry.Dispose(this);

                // Step 3: write updated metadata to the archive
                UpdateMeta(_header.FooterPointer - totalStripped);

                // Step 4: trim unused bytes at the end of the file
                _stream.SetLength(
                    _header.FooterPointer +
                    _header.ChunkIndexSize +
                    _header.PathIndexSize +
                    (_header.EntryCount * ArcStruct.EntrySize)
                );
            }
        }

        /// <summary>
        /// Sets the path of an entry.
        /// </summary>
        /// <param name="entry">An entry owned by this archive.</param>
        /// <param name="newPath">
        /// A new path for the entry.
        /// <para />
        /// To be valid it must be:
        /// - Absolute and canonical like "/my/stored/file.ext" (file extension is not required)
        /// - Contain only non-extended ASCII characters, except for these: [control characters] : * ? " < > |
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">The specified entry is not owned by this archive.</exception>
        /// <exception cref="ArgumentException">The new path is invalid.</exception>
        public void MoveEntry(ArcEntry entry, string newPath) {
            lock (_lock) {
                ThrowIfDisposed();
                ThrowIfReadOnly();
                ThrowIfEntryNotOwned(entry);

                newPath = newPath.Trim();

                if (!PathUtils.EntryAbsolutePathRegex.IsMatch(newPath))
                    throw new ArgumentException("The specified path is invalid.", "newLocation");

                entry.EntryPath = newPath;

                UpdateMeta(_header.FooterPointer);
            }
        }

        /// <summary>
        /// Calculates the Adler32 checksum of the provided data.
        /// </summary>
        /// <param name="data">The data to be hashed.</param>
        /// <returns>The Adler32 checksum of the provided data.</returns>
        public static uint Checksum(byte[] data) {
            if (data == null)
                return 1;

            uint adler = 1; // 1 & 0xffff
            uint sum2 = 0; // (1 >> 16) & 0xffff

            uint len = (uint)data.Length;

            for (int i = 0; len > 0; ++i, --len) {
                adler = (adler + data[i]) % ADLER32;
                sum2 = (sum2 + adler) % ADLER32;
            }

            return adler | (sum2 << 16);
        }

        /// <summary>
        /// Creates a new file entry in the archive using the provided path and data.
        /// </summary>
        /// <param name="path">The path of the entry in the archive.</param>
        /// <param name="data">The data of the entry.</param>
        /// <param name="storageMode">
        /// If storageMode is not StorageMode.Plain, data will be compressed accordingly.
        /// </param>
        /// <returns>The <see cref="ArcEntry"/> instance of the new entry.</returns>
        public ArcEntry CreateEntry(string path, byte[] data, StorageMode storageMode) {
            throw new NotImplementedException();
        }
        #endregion

        #region Public Static Methods
        /// <summary>
        /// Creates a new ARC v3 file and returns its instance.
        /// </summary>
        /// <param name="path">The location where to create the file.</param>
        /// <exception cref="IOException">The specified path is an already existing file.</exception>
        /// <returns>An <see cref="Arc"/> instance of the newly created file.</returns>
        public static Arc New(string path) { return New(path, DEFAULT_MAX_ALLOC); }

        /// <summary>
        /// Creates a new ARC v3 file and returns its instance.
        /// </summary>
        /// <param name="path">The location where to create the file.</param>
        /// <param name="maxAlloc">
        /// The maximum amount memory to be allocated when moving data within the file, in bytes.
        /// </param>
        /// <exception cref="IOException">The specified path is an already existing file.</exception>
        /// <returns>An <see cref="Arc"/> instance of the newly created file.</returns>
        public static Arc New(string path, int maxAlloc) {
            var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, true)) {
                stream.Seek(0, SeekOrigin.Begin);

                writer.Write(Encoding.ASCII.GetBytes("ARC\0")); // Magic
                writer.Write(3); // Version
                writer.Write(new ArcStruct.Header() {
                    EntryCount = 0,
                    ChunkCount = 0,
                    ChunkIndexSize = 0,
                    PathIndexSize = 0,
                    FooterPointer = 2048
                }.ToBytesLE());

                // Padding bytes
                var pad = new byte[2048 - (8 + ArcStruct.HeaderSize)];
                Array.Clear(pad, 0, pad.Length);
                writer.Write(pad);
                pad = null;
            }

            return new Arc(stream, false, maxAlloc);
        }
        #endregion

        #region Class Constructor
        /// <summary>
        /// Opens an ARC v3 file from an existing stream.
        /// </summary>
        /// <param name="stream">A stream containing a valid ARC v3 file.</param>
        /// <exception cref="NotSupportedException">The specified stream is not seekable or readable.</exception>
        /// <exception cref="InvalidDataException">The specified stream doesn't contain a valid ARC v3 file.</exception>
        public Arc(Stream stream) : this(stream, false, DEFAULT_MAX_ALLOC) { }

        /// <summary>
        /// Opens an ARC v3 file from an existing stream.
        /// </summary>
        /// <param name="stream">A stream containing a valid ARC v3 file.</param>
        /// <param name="leaveOpen">If after the instance is disposed, the stream should be kept open.</param>
        /// <exception cref="NotSupportedException">The specified stream is not seekable or readable.</exception>
        /// <exception cref="InvalidDataException">The specified stream doesn't contain a valid ARC v3 file.</exception>
        public Arc(Stream stream, bool leaveOpen) : this(stream, leaveOpen, DEFAULT_MAX_ALLOC) { }

        /// <summary>
        /// Opens an ARC v3 file from an existing stream.
        /// </summary>
        /// <param name="stream">A stream containing a valid ARC v3 file.</param>
        /// <param name="leaveOpen">If after the instance is disposed, the stream should be kept open.</param>
        /// <param name="maxAlloc">
        /// The maximum amount memory to be allocated when moving data within the file, in bytes.
        /// </param>
        /// <exception cref="NotSupportedException">The specified stream is not seekable or readable.</exception>
        /// <exception cref="InvalidDataException">The specified stream doesn't contain a valid ARC v3 file.</exception>
        public Arc(Stream stream, bool leaveOpen, int maxAlloc) {
            Init(stream, leaveOpen, maxAlloc);

            Validate();
            Parse();
        }

        private void Init(Stream stream, bool leaveOpen, int maxAlloc) {
            lock (_lock) {
                if (!stream.CanRead)
                    throw new NotSupportedException("The specified stream is not readable.");

                if (!stream.CanSeek)
                    throw new NotSupportedException("The specified stream is not seekable.");

                _stream = stream;
                _leaveOpen = leaveOpen;
                _maxAlloc = maxAlloc;

                _reader = new BinaryReader(stream, Encoding.ASCII, true);
                if (_stream.CanWrite)
                    _writer = new BinaryWriter(stream, Encoding.ASCII, true);

                _entries = new List<ArcEntry> { };
                _entriesReadOnly = _entries.AsReadOnly();
            }
        }

        private void Validate() {
            lock (_lock) {
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
        }

        private void Parse() {
            lock (_lock) {
                _stream.Seek(8, SeekOrigin.Begin);
                _header = ArcStruct.Header.FromBytesLE(_reader.ReadBytes(ArcStruct.HeaderSize));

                _chunkIndexPointer = _header.FooterPointer;
                _pathIndexPointer = _chunkIndexPointer + _header.ChunkIndexSize;
                _entryIndexPointer = _pathIndexPointer + _header.PathIndexSize;

                for (int i = 0; i < _header.EntryCount; ++i) {
                    _stream.Seek(_entryIndexPointer + (i * ArcStruct.EntrySize), SeekOrigin.Begin);
                    var entryStruct = ArcStruct.Entry.FromBytesLE(_reader.ReadBytes(ArcStruct.EntrySize));

                    _stream.Seek(_pathIndexPointer + entryStruct.PathOffset, SeekOrigin.Begin);
                    var entryPath = new string(_reader.ReadChars((int)entryStruct.PathLength));

                    _entries.Add(new ArcEntry(
                        this, entryStruct, entryPath, GetChunks(entryStruct.ChunkOffset, entryStruct.ChunkCount)
                    ));
                }
            }
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            lock (_lock) {
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
