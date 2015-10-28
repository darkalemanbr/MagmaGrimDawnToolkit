using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;

namespace GDExplorer {
    internal class EntryTreeSorter : IComparer {
        public int Compare(object x, object y) {
            var xx = x as TreeNode;
            var yy = y as TreeNode;

            if (xx.Nodes.Count == 0 && yy.Nodes.Count > 0)
                return 1;
            else if (xx.Nodes.Count > 0 && yy.Nodes.Count == 0)
                return -1;
            else {
                return string.Compare(xx.Text, yy.Text);
            }
        }
    }
}
