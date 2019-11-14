using System.Collections;

namespace GoFish.DataAccess
{
    public interface INullFieldHandler
    {
        bool IsNull(int nullFieldIndex);
    }

    public struct UIntNullFieldHandler : INullFieldHandler
    {
        private readonly uint uint32;

        public UIntNullFieldHandler(uint b)
        {
            this.uint32 = b;
        }

        public bool IsNull(int nullFieldIndex) => (uint32 & (1 << nullFieldIndex)) != 0;

    }
    public class BitArrayNullFieldHandler : INullFieldHandler
    {
        private readonly BitArray BitMap;

        public BitArrayNullFieldHandler(byte[] bitmap)
        {
            this.BitMap = new BitArray(bitmap);
        }
        public bool IsNull(int nullFieldIndex) => BitMap.Get(nullFieldIndex);
    }
}
