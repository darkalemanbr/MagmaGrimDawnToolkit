using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GDLib.Arc;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace GDLibTest {
    /// <summary>
    /// Summary description for UnitTest1
    /// </summary>
    [TestClass]
    public class ArcTest {
        string questsPath;

        public ArcTest() {
            var gdPath = MiscTest.FindGrimDawnInstallPath();

            if (gdPath == null)
                return;

            questsPath = Path.Combine(gdPath, @"resources\text_en.arc");
        }

        private string CopyArcFile() {
            if (questsPath == null)
                return null;

            var path = Path.Combine(TestContext.DeploymentDirectory, "file.arc");

            File.Copy(questsPath, path, true);

            return path;
        }

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

        [TestMethod]
        public void ExistingArc() {
            var testArcPath = CopyArcFile();

            if (testArcPath == null)
                return;

            int finalEntryCount;

            using (var questsArc = new Arc(new FileStream(testArcPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))) {
                var initialEntryCount = questsArc.Entries.Count;

                Assert.IsTrue(initialEntryCount > 0);
#if DEBUG
                Debug.WriteLine("Entry count: " + questsArc.Entries.Count, TestContext.TestName);
#endif
                var entry = questsArc.Entries[0];

                var data = questsArc.ReadEntry(entry);
                Assert.IsTrue(data.Length == entry.PlainSize);
#if DEBUG
                Debug.WriteLine("Entry size: " + data.Length);
                Debug.WriteLine("Entry path: " + entry.Path);
#endif
                var sum = Arc.Checksum(data);

                Assert.IsTrue(sum == entry.Adler32);
#if DEBUG
                Debug.WriteLine("Entry checksum: " + entry.Adler32);
#endif
                questsArc.MoveEntry(entry, @"/new/location.ext");
                Assert.IsTrue(entry.Path == @"/new/location.ext");

                questsArc.DeleteEntry(entry);

                try {
                    var shouldThrowCauseDisposed = entry.PlainSize;

                    Assert.Fail();
                }
                catch (ObjectDisposedException)
                { }

                var entry2 = questsArc.CreateEntry(@"/porn.stash", data, StorageMode.Lz4Compressed);
                Assert.IsTrue(entry2.Adler32 == sum);

                finalEntryCount = questsArc.Entries.Count;
                Assert.IsTrue(finalEntryCount == initialEntryCount);
            }

            // Reopen the file to see if it's still valid
            using (var questsArc = new Arc(new FileStream(testArcPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))) {
                Assert.IsTrue(questsArc.Entries.Count == finalEntryCount);

                var entry = questsArc.Entries[0];

                var data = questsArc.ReadEntry(entry);
                Assert.IsTrue(Arc.Checksum(data) == entry.Adler32);
            }
        }

        [TestMethod]
        public void CreateNewArc() {
            var file = Path.Combine(Path.GetTempPath(), TestContext.TestName + ".arc");

            using (var newArc = Arc.New(file)) {
                Assert.IsTrue(newArc.Entries.Count == 0);
            }

            File.Delete(file);
        }
    }
}
