using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDLib.Arc {
    [Flags]
    public enum WritePolicy {
        /// <summary>
        /// If there are any stray data blocks (data blocks not owned by any of the entries in the archive),
        /// then those will be overwritten first before allocating new blocks.
        /// </summary>
        OverwriteStrayBlocks = 1,

        /// <summary>
        /// If there are any stray data blocks (data blocks not owned by any of the entries in the archive), then those
        /// will remain intact and data will be stored only in new blocks.
        /// </summary>
        DontOverwriteStrayBlocks = 2
    }

    /// <remarks>
    /// <see cref="Unlink"/>, <see cref="Strip"/> and <see cref="Zerofill"/> must be mutually exclusive.
    /// </remarks>
    [Flags]
    public enum DeletePolicy {
        /// <summary>
        /// Only removes the entry from the index and leaves the data and chunk descriptors intact.
        /// It's is similar to what happens in many computer filesystems. That's why you can recover data after
        /// deletion.
        /// Also much faster since the archive doesn't have to be entirely rebuilt.
        /// </summary>
        Unlink = 1,

        /// <summary>
        /// The data and chunk descriptors are completelly stripped off the file. Takes a lot of time when dealing with
        /// very large chunks of data.
        /// </summary>
        Strip = 2,

        /// <summary>
        /// The data and chunk descriptors are zeroed (aka "nullified").
        /// Example: if we had the bytes [6C 6F 72 65 6D], after zeroing it would become [00 00 00 00 00].
        /// After zeroing though, it will still occupy the same space as it did before.
        /// </summary>
        Zerofill = 4
    }
}
