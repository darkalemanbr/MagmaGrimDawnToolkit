using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.InteropServices;
using System.Reflection;
using GDLib.Arc;

namespace GDLibTest
{
    [TestClass]
    public class StructSize
    {
        Type _structClass;

        public StructSize()
        {
            var assembly = Assembly.Load("GDLib");
            Assert.IsNotNull(assembly);

            _structClass = assembly.GetType("GDLib.Arc.ArcStruct");
            Assert.IsNotNull(_structClass);
        }

        [TestMethod]
        public void HeaderSize()
        {
            var headerStruct = _structClass.GetNestedType("Header");
            Assert.IsNotNull(headerStruct);

            Assert.AreEqual(20, Marshal.SizeOf(headerStruct));
        }

        [TestMethod]
        public void EntrySize()
        {
            var entryStruct = _structClass.GetNestedType("Entry");
            Assert.IsNotNull(entryStruct);

            Assert.AreEqual(44, Marshal.SizeOf(entryStruct));
        }

        [TestMethod]
        public void ChunkSize()
        {
            var chunkStruct = _structClass.GetNestedType("Chunk");
            Assert.IsNotNull(chunkStruct);

            Assert.AreEqual(12, Marshal.SizeOf(chunkStruct));
        }
    }
}
