using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Configuration;
using System.Diagnostics;
using System.Text.RegularExpressions;
using GDLib.Arc;
using GDLib.Utils;

namespace GDExplorer
{
    public partial class MainForm : Form
    {
        private enum Mode {
            None,
            Arc,
            Arz
        }

        private enum Action {
            Ready,
            Loading,
            Writing
        }

        private const string W_TITLE = "GD Explorer";
        private const string W_TITLE_OPENED = "GD Explorer :: {0}";

        private static Regex _validNodeRegex;

        private Mode _mode;
        private string _openFilePath;
        private Arc _arc;
        private Arc _arz;
        private BackgroundWorker _worker;

        private Action __status;
        private Action Status {
            get {
                return __status;
            }

            set {
                switch (value) {
                    case Action.Ready:
                        statusLabel.Text = "Ready";
                        break;

                    case Action.Loading:
                        statusLabel.Text = "Loading file...";
                        break;

                    case Action.Writing:
                        statusLabel.Text = "Writing to file...";
                        break;
                }

                __status = value;
            }
        }

        static MainForm() {
            _validNodeRegex = new Regex(@"^(?!\s)[ !#-)+-.0-9;=@A-Z\[\]^-{}~]+(?<![\s.])$",
                RegexOptions.Compiled | RegexOptions.Singleline);
        }

        public MainForm()
        {
            InitializeComponent();

            _mode = Mode.None;
            _worker = new BackgroundWorker();
            Status = Action.Ready;

            Text = W_TITLE;
        }

        private TreeNode AddPathToTree(TreeNodeCollection nodes, string path) {
            var fname = Path.GetFileName(path);

            if (path != fname)
                nodes = AddPathToTree(nodes, Path.GetDirectoryName(path)).Nodes;

            var node = nodes.Find(fname, false).FirstOrDefault();

            if (node == null)
                node = nodes.Add(fname, fname);

            return node;
        }

        private void ChooseAndOpenFile(object sender, EventArgs e) {
            var selected = openFile.ShowDialog();

            if (selected != DialogResult.OK)
                return;

            CloseFile();
            _mode = Mode.None;
            Status = Action.Loading;

            try {
                _arc = new Arc(new FileStream(
                    openFile.FileName,
                    FileMode.Open, openFile.ReadOnlyChecked ? FileAccess.Read : FileAccess.ReadWrite,
                    FileShare.None
                ), false, Properties.Settings.Default.MaxAlloc);
            }
            catch (Exception exc) {
#if DEBUG
                throw exc;
#endif
                CloseFile();

                MessageBox.Show(
                    string.Format(
                        exc is InvalidDataException ?
                            "The file \"{0}\" is not valid." : "The file \"{0}\" could not be opened."
                        , openFile.FileName),
                    "", MessageBoxButtons.OK, MessageBoxIcon.Error);

                Status = Action.Ready;

                return;
            }

            _mode = Mode.Arc;
            _openFilePath = openFile.FileName;

            _arc.Entries.All(x => {
                AddPathToTree(entryTree.Nodes, x.Path);
                return true;
            });

            Text = string.Format(W_TITLE_OPENED, Path.GetFileName(_openFilePath));
            Status = Action.Ready;
        }

        private void CloseFile() {
            if (_arc != null) {
                lock (_arc) {
                    _arc.Dispose();
                    _arc = null;
                }
            }

            if (_arz != null) {
                lock (_arz) {
                    _arz.Dispose();
                    _arz = null;
                }
            }

            _openFilePath = null;
            entryTree.Nodes.Clear();
            _mode = Mode.None;
            Text = W_TITLE;
        }

        private bool RenameSingle(string oldPath, string newPath) {
            if (_arc == null)
                return false;

            oldPath = oldPath.Replace('\\', '/').Trim('/');
            newPath = '/' + newPath.Replace('\\', '/');

            lock (_arc) {
                Status = Action.Writing;

                try {
                    _arc.MoveEntry(_arc.Entries.First(x => x.Path == oldPath), newPath);
                }
                catch (Exception exc) {
#if DEBUG
                    throw exc;
#endif
                    MessageBox.Show("An error has occurred while renaming the file.",
                        "", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    Status = Action.Ready;

                    return false;
                }

                Status = Action.Ready;
            }

            return true;
        }

        private bool RenameFolder(string folder, string newName) {
            if (_arc == null)
                return false;

            folder = folder.Replace('\\', '/').Trim('/');

            lock (_arc) {
                Status = Action.Writing;

                try {
                    foreach (var entry in _arc.Entries.Where(x => x.Path.StartsWith(folder + '/'))) {
                        var newPath = '/' + newName + '/' + Path.GetFileName(entry.Path);
                        _arc.MoveEntry(entry, newPath);
                    }
                }
                catch (Exception exc) {
#if DEBUG
                    throw exc;
#endif
                    MessageBox.Show("An error has occurred while renaming the folder.",
                        "", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    Status = Action.Ready;

                    return false;
                }

                Status = Action.Ready;
            }

            return true;
        }

        private void Exit(object sender, EventArgs e) {
            Close();
        }

        private void Exit(object sender, FormClosingEventArgs e) {
            CloseFile();
        }

        private void entryTree_AfterLabelEdit(object sender, NodeLabelEditEventArgs e) {
            entryTree.LabelEdit = false;

            if (e.Label == null) {
                e.CancelEdit = true;
                return;
            }

            var name = e.Label.Trim().TrimEnd('.');

            if (!_validNodeRegex.IsMatch(name)) {
                e.CancelEdit = true;
                return;
            }

            // If node has children, it must be a folder
            if (e.Node.Nodes.Count > 0)
                e.CancelEdit = !RenameFolder(e.Node.FullPath, Path.Combine(Path.GetDirectoryName(e.Node.FullPath), name));
            else
                e.CancelEdit = !RenameSingle(e.Node.FullPath, Path.Combine(Path.GetDirectoryName(e.Node.FullPath), name));
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e) {
            if (entryTree.SelectedNode != null) {
                entryTree.LabelEdit = true;
                entryTree.SelectedNode.BeginEdit();
            }
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e) {
            CloseFile();
        }
    }
}
