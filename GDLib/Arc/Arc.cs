using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using Nuclex.Support.Cloning;

namespace GDLib.Arc
{
    public class Arc : IDisposable
    {
        #region Private Fields
        private Stream _stream;
        private bool _readOnly;
        private bool _leaveOpen;
        private BinaryReader _reader;
        private BinaryWriter _writer;
        private ArcStruct.Header _header;
        private List<ArcEntry> _entriesInternal;
        private List<ArcEntry> _entries;
        private ReadOnlyCollection<ArcEntry> _entriesReadOnly;
        private uint _chunkIndexPointer;
        private uint _pathIndexPointer;
        private uint _entryIndexPointer;
        #endregion

        #region Public Properties
        public bool IsReadOnly { get { return _readOnly; } }
        public ReadOnlyCollection<ArcEntry> Entries { get { return _entriesReadOnly; } }
        #endregion

        #region Private Methods
        
        #endregion

        #region Public Methods
        
        #endregion

        #region Class Constructor
        public Arc(Stream stream) : this(stream, true, false) { }
        public Arc(Stream stream, bool readOnly) : this(stream, readOnly, false) { }
        public Arc(Stream stream, bool readOnly, bool leaveOpen)
        {
            Init(stream, readOnly, leaveOpen);

            Validate();
            Parse();
        }

        private void Init(Stream stream, bool readOnly, bool leaveOpen)
        {
            if (!stream.CanRead)
                throw new NotSupportedException("The specified stream is not readable.");

            if (!readOnly && !stream.CanWrite)
                throw new NotSupportedException("The specified stream is not writable.");

            if (!stream.CanSeek)
                throw new NotSupportedException("The specified stream is not seekable.");

            _stream = stream;
            _readOnly = readOnly;
            _leaveOpen = leaveOpen;
            _reader = new BinaryReader(stream, Encoding.ASCII, true);
            if (!readOnly) _writer = new BinaryWriter(stream, Encoding.ASCII, true);
            _entries = new List<ArcEntry> { };
            _entriesReadOnly = _entries.AsReadOnly();
        }

        private void Validate()
        {
            if (_stream.Length < 2048)
                throw new InvalidDataException("The specified file has less than 2048 bytes.");

            _stream.Seek(0, SeekOrigin.Begin);

            if (_reader.ReadChars(4).ToString() != "ARC\0")
                throw new InvalidDataException("The magic number of the specified file is invalid.");

            var version = _reader.ReadUInt32();
            if (version != 3)
                throw new InvalidDataException(String.Format("The version of the specified ARC file ({0}) is not supported.", version));
        }

        private void Parse()
        {
            _stream.Seek(8, SeekOrigin.Begin);
            _header = ArcStruct.Header.FromBytes(_reader.ReadBytes(ArcStruct.HeaderSize));

            _chunkIndexPointer = _header.FooterPointer;
            _pathIndexPointer = _chunkIndexPointer + _header.ChunkIndexSize;
            _entryIndexPointer = _pathIndexPointer + _header.PathIndexSize;

            for (uint i = 0; i < _header.EntryCount; ++i)
            {
                _stream.Seek(_entryIndexPointer + (i * ArcStruct.EntrySize), SeekOrigin.Begin);
                var entryStruct = ArcStruct.Entry.FromBytes(_reader.ReadBytes(ArcStruct.EntrySize));

                _stream.Seek(_pathIndexPointer + entryStruct.PathOffset, SeekOrigin.Begin);
                var entryPath = _reader.ReadChars((int)entryStruct.PathLength).ToString();

                _entries.Add(new ArcEntry(this, entryStruct, entryPath));
            }
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).

                    _writer.Dispose();
                    _reader.Dispose();

                    if (!_leaveOpen)
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
        ~Arc()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            if (disposedValue)
                throw new ObjectDisposedException(this.GetType().ToString());
        }
        #endregion
    }
}
