﻿using System;
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
        private bool _leaveOpen;
        private BinaryReader _reader;
        private BinaryWriter _writer;
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
        #endregion

        #region Private Methods

        #endregion

        #region Public Methods
        public byte[] ReadEntry(ArcEntry entry)
        {
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

            if (!_entries.Contains(entry))
                throw new ArgumentOutOfRangeException("entry", "This Arc instance doesn't own that entry.");

            switch (entry.StorageMode)
            {
                // I have yet to see an entry with this type, but atom0s' code has it so I'll keep it...
                case StorageMode.Plain:
                    _stream.Seek(entry.EntryStruct.DataPointer, SeekOrigin.Begin);
                    return _reader.ReadBytes((int)entry.EntryStruct.PlainSize);

                case StorageMode.Lz4Compressed:
                    // TODO: IMPLEMENT
                    break;

                default:
                    throw new InvalidDataException("The entry has been stored using an unsupported mode.");
            }

            return new byte[] { };
        }

        public void WriteChanges(CreatePolicy createPolicy = CreatePolicy.OverwriteStrayBlocks, DeletePolicy deletePolicy = DeletePolicy.Strip)
        {
            ThrowIfDisposed();

            throw new NotImplementedException();
        }
        #endregion

        #region Class Constructor
        public Arc(Stream stream) : this(stream, false) { }
        public Arc(Stream stream, bool leaveOpen)
        {
            Init(stream, leaveOpen);

            Validate();
            Parse();
        }

        private void Init(Stream stream, bool leaveOpen)
        {
            if (!stream.CanRead)
                throw new NotSupportedException("The specified stream is not readable.");

            if (!stream.CanSeek)
                throw new NotSupportedException("The specified stream is not seekable.");

            _stream = stream;
            _leaveOpen = leaveOpen;
            _reader = new BinaryReader(stream, Encoding.ASCII, true);
            if (_stream.CanWrite) _writer = new BinaryWriter(stream, Encoding.ASCII, true);
            _entries = new List<ArcEntry> { };
            _entriesReadOnly = _entries.AsReadOnly();
        }

        private void Validate()
        {
            if (_stream.Length < 2048)
                throw new InvalidDataException("The specified file has less than 2048 bytes.");

            _stream.Seek(0, SeekOrigin.Begin);

            //if (_reader.ReadChars(4).ToString() != "ARC\0")
            if (_reader.ReadUInt32() != 0x435241)
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
                var entryPath = new string(_reader.ReadChars((int)entryStruct.PathLength));

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

                    if (_writer != null) _writer.Dispose();
                    if (_reader != null) _reader.Dispose();
                    if (!_leaveOpen && _stream != null) _stream.Dispose();
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
