using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GDLib.Utils.Lz4;
using GDLib.Arc;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Diagnostics;

namespace GDLibTest {
    /// <summary>
    /// Summary description for TempTest1
    /// </summary>
    [TestClass]
    public class MiscTest {
        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext {
            get {
                return testContextInstance;
            }
            set {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        /// <summary>
        /// Finds the location where Grim Dawn is installed.
        /// </summary>
        /// <returns>Returns the path when it's found and exists; <see cref="string.Empty"/> otherwise.</returns>
        internal static string FindGrimDawnInstallPath() {
            var regVal = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 219990", "InstallLocation", null);

            if (regVal != null && Directory.Exists((string)regVal))
                return (string)regVal;
            else
                return "";
        }

        [TestMethod]
        public void ArcEntryTypesAreAllKnown() {
            var gdInstallPath = FindGrimDawnInstallPath();

#if DEBUG
            Debug.WriteLine("GD install path: " + gdInstallPath, TestContext.TestName);
#endif

            // Pass the test if it can't find the install location
            if (gdInstallPath == "")
                return;

            foreach (var file in Directory.GetFiles(Path.Combine(gdInstallPath, "resources"), "*.arc")) {
#if DEBUG
                Debug.WriteLine(string.Format("Checking {0} ...", file), TestContext.TestName);
#endif
                using (var arc = new Arc(new FileStream(file, FileMode.Open, FileAccess.Read))) {
                    foreach (var entry in arc.Entries) {
#if DEBUG
                        Debug.WriteLine(entry.Path);
                        Debug.WriteLine("  StorageMode: " + entry.StorageMode.ToString());
                        Debug.WriteLine("  CompressedSize: " + entry.CompressedSize);
                        Debug.WriteLine("  PlainSize: " + entry.PlainSize);
                        Debug.WriteLine("  Adler32: " + entry.Adler32);
                        Debug.WriteLine("  LastWriteTime: " + entry.LastWriteTime);
                        Debug.WriteLine("");
#endif
                        Assert.IsTrue(Enum.IsDefined(typeof(StorageMode), entry.StorageMode));
                    }
                }
            }
        }

        [TestMethod]
        public void IsLz4ReallyWorking() {
            const string str =
@"C is an imperative (procedural) language. It was designed to be compiled
using a relatively straightforward compiler, to provide low-level access to memory,
to provide language constructs that map efficiently to machine instructions, and to
require minimal run-time support. C was therefore useful for many applications that
had formerly been coded in assembly language, such as in system programming.

Despite its low-level capabilities, the language was designed to encourage
cross-platform programming. A standards-compliant and portably written C program
can be compiled for a very wide variety of computer platforms and operating systems
with few changes to its source code. The language has become available on a very
wide range of platforms, from embedded microcontrollers to supercomputers.";

            var strBytes = Encoding.UTF8.GetBytes(str);
#if DEBUG
            Debug.WriteLine("String UTF8 bytes length: " + strBytes.Length, TestContext.TestName);
#endif
            byte[] compressed = new byte[Lz4.CompressBound(strBytes.Length)];
            var compressedBlockSize = Lz4.CompressDefault(strBytes, compressed, strBytes.Length, compressed.Length);
            Assert.IsTrue(compressedBlockSize > 0);
#if DEBUG
            Debug.WriteLine(string.Format("Compressed block length: {0}; bound length: {1}", compressedBlockSize, compressed.Length), TestContext.TestName);
#endif
            var decompressed = new byte[strBytes.Length];
            Assert.IsTrue(Lz4.DecompressSafe(compressed, decompressed, compressedBlockSize, decompressed.Length) > 0);
#if DEBUG
            Debug.WriteLine("Decompressed bytes length: " + decompressed.Length, TestContext.TestName);
#endif
            // Is decompressed data the same as the original?
            Assert.IsTrue(decompressed.SequenceEqual(strBytes));
        }
    }
}
