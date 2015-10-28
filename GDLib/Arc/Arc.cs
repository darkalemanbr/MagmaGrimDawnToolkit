using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using GDLib.Utils;
using GDLib.Utils.Lz4;

namespace GDLib.Arc {
    public class Arc : IDisposable {
        private const int DEFAULT_MAX_ALLOC = 256 * 1024 * 1024;
        private const uint ADLER32_MODULO = 65521;

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
                    _maxAlloc = Math.Max(value, 1);
                }
            }
        }
        #endregion

        #region Private Methods
        private void UpdateMeta(uint footerStart) {
            if (footerStart > _stream.Length)
                throw new ArgumentOutOfRangeException("Cannot seek past the end of the stream.");

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

            // Write all paths
            foreach (var entry in _entries) {
                entry.EntryStruct.PathOffset = pathIndexSize;
                entry.EntryStruct.PathLength = (uint)entry.EntryPath.Length;

                _writer.Write(Encoding.ASCII.GetBytes(entry.EntryPath + '\0'));

                pathIndexSize += (uint)(entry.EntryPath.Length + 1);
            }

            // Write all entry structs
            foreach (var entry in _entries) {
                _writer.Write(entry.EntryStruct.ToBytesLE());
            }

            // Truncate file at the current position
            _stream.SetLength(_stream.Position + 1);

            _header.EntryCount = (uint)_entries.Count;
            _header.ChunkCount = chunkCount;
            _header.ChunkIndexSize = chunkCount * (uint)ArcStruct.ChunkSize;
            _header.PathIndexSize = pathIndexSize;
            _header.FooterPointer = footerStart;

            _stream.Seek(8, SeekOrigin.Begin);
            _writer.Write(_header.ToBytesLE());

            _chunkIndexPointer = _header.FooterPointer;
            _pathIndexPointer = _chunkIndexPointer + _header.ChunkIndexSize;
            _entryIndexPointer = _pathIndexPointer + _header.PathIndexSize;
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

        // Adapted from 'http://stackoverflow.com/a/8828483/2969832'
        private bool EnumIsValueValid(Enum value) {
            var chr = value.ToString()[0];

            return !(char.IsDigit(chr) || chr == '-');
        }

        private void ThrowIfReadOnly() {
            if (!_stream.CanWrite)
                throw new NotSupportedException("Stream is in read-only mode.");
        }

        private void ThrowIfEntryNotOwned(ArcEntry entry) {
            if (!_entries.Contains(entry))
                throw new ArgumentOutOfRangeException("entry", "This Arc instance doesn't own that entry.");
        }

        private ArcStruct.Chunk[] GetChunks(uint startOffset, uint chunkCount) {
            // Check if we can even read those chunks
            if (startOffset + (ArcStruct.ChunkSize * chunkCount) > _header.ChunkIndexSize)
                throw new ArgumentOutOfRangeException("The requested chunks are out of the chunk index bounds.");

            _stream.Seek(_chunkIndexPointer + (startOffset * ArcStruct.ChunkSize), SeekOrigin.Begin);
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

                if (!Enum.IsDefined(typeof(StorageMode), entry.StorageMode))
                    throw new InvalidDataException("The entry has been stored using an unsupported mode.");

                using (var plainData = new MemoryStream((int)entry.EntryStruct.PlainSize)) {
                    foreach (var chunk in entry.Chunks) {
                        // Read the compressed chunk of data from the file
                        _stream.Seek(chunk.DataPointer, SeekOrigin.Begin);

                        if (entry.StorageMode == StorageMode.Plain) {
                            _reader.Read(plainData.GetBuffer(), (int)plainData.Position, (int)chunk.PlainSize);
                            plainData.Position += chunk.PlainSize;
                        }
                        else if (entry.StorageMode == StorageMode.Lz4Compressed) {
                            // Decompress it
                            var decompressedChunk = new byte[chunk.PlainSize];
                            Lz4.DecompressSafe(
                                _reader.ReadBytes((int)chunk.CompressedSize),
                                decompressedChunk, 
                                (int)chunk.CompressedSize,
                                (int)chunk.PlainSize
                            );
                            plainData.Write(decompressedChunk, 0, (int)chunk.PlainSize);

                            decompressedChunk = null;
                        }
                    }

                    return plainData.GetBuffer();
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
                    var frm = entry.EntryStruct.DataPointer + entry.EntryStruct.CompressedSize;
                    var len = (int)_stream.Length - frm;
                    MoveData(frm, (uint)len, entry.EntryStruct.DataPointer);

                    totalStripped += entry.EntryStruct.CompressedSize;

                    // Step 2: update all remaining entries
                    if (totalStripped > 0) {
                        foreach (var rEntry in _entries) {
                            if (ReferenceEquals(rEntry, entry))
                                continue;

                            if (rEntry.Chunks.Length > 0) {
                                for (uint i = 0; i < rEntry.Chunks.Length; ++i) {
                                    if (rEntry.Chunks[i].DataPointer > entry.EntryStruct.DataPointer)
                                        rEntry.Chunks[i].DataPointer -= totalStripped;
                                }

                                rEntry.EntryStruct.DataPointer = rEntry.Chunks[0].DataPointer;
                            }
                            else
                                rEntry.EntryStruct.DataPointer -= totalStripped;
                        }
                    }
                }

                _entries.Remove(entry);
                entry.Dispose(this);

                // Step 3: write updated metadata to the archive
                UpdateMeta(_header.FooterPointer - totalStripped);
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
        /// <exception cref="ArgumentNullException">The new path is <see cref="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The specified entry is not owned by this archive.</exception>
        /// <exception cref="ArgumentException">The new path is invalid.</exception>
        public void MoveEntry(ArcEntry entry, string newPath) {
            lock (_lock) {
                ThrowIfDisposed();
                ThrowIfReadOnly();
                ThrowIfEntryNotOwned(entry);

                if (newPath == null)
                    throw new ArgumentNullException("New path cannot be null.", "newPath");

                newPath = newPath.Trim();

                if (!PathUtils.EntryAbsolutePathRegex.IsMatch(newPath))
                    throw new ArgumentException("The specified path is invalid.", "newLocation");

                entry.EntryPath = newPath.TrimStart('/');

                UpdateMeta(_header.FooterPointer);
            }
        }

        /// <summary>
        /// Calculates the Adler32 checksum of the provided data.
        /// </summary>
        /// <param name="data">The data to be hashed.</param>
        /// <returns>The Adler32 checksum of the provided data.</returns>
        /// <remarks>Locks the data object.</remarks>
        public static uint Checksum(byte[] data) {
            lock (data) {
                if (data == null)
                    return 1;

                uint adler = 1; // 1 & 0xffff
                uint sum2 = 0; // (1 >> 16) & 0xffff

                uint len = (uint)data.Length;

                for (int i = 0; len > 0; ++i, --len) {
                    adler = (adler + data[i]) % ADLER32_MODULO;
                    sum2 = (sum2 + adler) % ADLER32_MODULO;
                }

                return adler | (sum2 << 16);
            }
        }

        /// <summary>
        /// Creates a new file entry in the archive using the provided path and data.
        /// </summary>
        /// <param name="path">The path of the entry in the archive.</param>
        /// <param name="data">The data of the entry.</param>
        /// <param name="storageMode">
        /// If storageMode is not StorageMode.Plain, data will be compressed accordingly.
        /// </param>
        /// <exception cref="ArgumentNullException">path or data are <see cref="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// The specified path is invalid or the specified storage mode is not supported.
        /// </exception>
        /// <returns>The <see cref="ArcEntry"/> instance of the new entry.</returns>
        /// <remarks>Locks the data object.</remarks>
        public ArcEntry CreateEntry(string path, byte[] data, StorageMode storageMode) {
            lock (data)
            lock (_lock) {
                    ThrowIfDisposed();
                    ThrowIfReadOnly();

                    if (path == null)
                        throw new ArgumentNullException("Path cannot be null.", "path");

                    if (!PathUtils.EntryAbsolutePathRegex.IsMatch(path))
                        throw new ArgumentException("The specified path is invalid.", "path");

                    if (data == null)
                        throw new ArgumentNullException("Data cannot be null.", "data");

                    if (!Enum.IsDefined(typeof(StorageMode), storageMode))
                        throw new ArgumentException("The specified storage mode is not supported.", "storageMode");

                    byte[] writeData = data;

                    if (storageMode == StorageMode.Lz4Compressed) {
                        writeData = new byte[Lz4.CompressBound(data.Length)];
                        var cSize = Lz4.CompressDefault(data, writeData, data.Length, writeData.Length);
                        Array.Resize(ref writeData, cSize);
                    }

                    _stream.Seek(_header.FooterPointer, SeekOrigin.Begin);

                    var chunk = new ArcStruct.Chunk() {
                        DataPointer = (uint)_stream.Position,
                        CompressedSize = (uint)writeData.Length,
                        PlainSize = (uint)data.Length
                    };

                    _writer.Write(writeData);

                    writeData = null;

                    var entry = new ArcEntry(
                        this,
                        new ArcStruct.Entry() {
                            StorageMode = (uint)storageMode,
                            DataPointer = chunk.DataPointer,
                            CompressedSize = chunk.CompressedSize,
                            PlainSize = chunk.PlainSize,
                            Adler32 = Checksum(data),
                            FileTime = DateTime.Now.ToFileTime()
                            // The other fields will be set by UpdateMeta()
                        },
                        path,
                        new ArcStruct.Chunk[] { chunk }
                    );

                    _entries.Add(entry);

                    UpdateMeta((uint)_stream.Position);

                    return entry;
                }
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
                maxAlloc = Math.Max(maxAlloc, 1);

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
                if (version != 3) {
                    throw new InvalidDataException(
                        string.Format("The version of the specified ARC file ({0}) is not supported.", version)
                    );
                }
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

                        foreach (var entry in _entries) {
                            entry.Dispose(this);
                        }
                        
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
