using System;
using System.Collections.Generic;
using System.IO;

namespace GDLib.Arc {
    public class ArcEntry : IDisposable {
        internal Arc ParentArc;
        internal ArcStruct.Entry EntryStruct;
        internal string EntryPath;
        internal DateTime LastWrite;
        internal ArcStruct.Chunk[] Chunks;

        public Arc Parent {
            get {
                ThrowIfDisposed();

                return ParentArc;
            }
        }

        public string Path {
            get {
                ThrowIfDisposed();

                return EntryPath;
            }
        }

        public StorageMode StorageMode {
            get {
                ThrowIfDisposed();

                return (StorageMode)EntryStruct.StorageMode;
            }
        }

        public int CompressedSize {
            get {
                ThrowIfDisposed();

                return (int)EntryStruct.CompressedSize;
            }
        }

        public int PlainSize {
            get {
                ThrowIfDisposed();

                return (int)EntryStruct.PlainSize;
            }
        }

        public uint Adler32 {
            get {
                ThrowIfDisposed();

                return EntryStruct.Adler32;
            }
        }

        public DateTime LastWriteTime {
            get {
                ThrowIfDisposed();

                return LastWrite;
            }
        }

        internal ArcEntry(Arc parent, ArcStruct.Entry entryStruct, string entryPath, ArcStruct.Chunk[] chunks) {
            ParentArc = parent;
            EntryStruct = entryStruct;
            EntryPath = entryPath;
            LastWrite = DateTime.FromFileTime((long)EntryStruct.FileTime);
            Chunks = chunks;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).

                    if (Chunks.Length > 0)
                        Array.Resize(ref Chunks, 0);
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                Chunks = null;

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ArcEntry() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        internal void Dispose(Arc parent) {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        /// <summary>
        /// DON'T USE!!! Disposing the object is not allowed.
        /// </summary>
        /// <exception cref="InvalidOperationException">Disposing the object is not allowed.</exception>
        public void Dispose() {
            throw new InvalidOperationException("Disposing the object is not allowed.");
        }

        private void ThrowIfDisposed() {
            if (disposedValue)
                throw new ObjectDisposedException(this.GetType().ToString());
        }
        #endregion
    }
}
