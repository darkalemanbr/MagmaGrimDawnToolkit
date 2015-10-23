using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GDLib.Arc;
using System.IO;
using System.Diagnostics;

namespace GDLibTest {
    /// <summary>
    /// Summary description for UnitTest1
    /// </summary>
    [TestClass]
    public class ArcTest {
        public ArcTest() {

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
            var gdPath = MiscTest.FindGrimDawnInstallPath();

            if (gdPath == "")
                return;

            var arcPath = Path.Combine(TestContext.DeploymentDirectory, "Quests.arc");
            File.Copy(Path.Combine(gdPath, @"resources\Quests.arc"), arcPath, true);

            int finalEntryCount;

            using (var questsArc = new Arc(new FileStream(arcPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))) {
                var initialEntryCount = questsArc.Entries.Count;

                Assert.IsTrue(initialEntryCount > 0);
#if DEBUG
                Debug.WriteLine("Entry count: " + questsArc.Entries.Count, TestContext.TestName);
#endif
                var data = questsArc.ReadEntry(questsArc.Entries[0]);
                Assert.IsTrue(data.Length == questsArc.Entries[0].PlainSize);
#if DEBUG
                Debug.WriteLine("First entry size: " + data.Length);
                Debug.WriteLine("First entry path: " + questsArc.Entries[0].Path);
#endif
                questsArc.MoveEntry(questsArc.Entries[0], @"/new/location.ext");
                Assert.IsTrue(questsArc.Entries[0].Path == @"/new/location.ext");

                questsArc.DeleteEntry(questsArc.Entries[0]);
                finalEntryCount = questsArc.Entries.Count;
                Assert.IsTrue(finalEntryCount == initialEntryCount - 1);
            }

            // Reopen the file to see if it's still valid
            using (var questsArc = new Arc(new FileStream(arcPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))) {
                Assert.IsTrue(questsArc.Entries.Count == finalEntryCount);
            }
        }

        [TestMethod]
        public void NewArc() {
            var file = Path.Combine(Path.GetTempPath(), TestContext.TestName + ".arc");

            using (var newArc = Arc.New(file)) {
                Assert.IsTrue(newArc.Entries.Count == 0);
            }

            File.Delete(file);
        }
    }
}
