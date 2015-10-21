using System;

namespace GDLib.Arc {
    internal struct DataBlock {
        public int Pointer;
        public int Length;

        public DataBlock(int pointer, int length) {
            Pointer = pointer;
            Length = length;
        }
    }
}
