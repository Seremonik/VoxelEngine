using NUnit.Framework;

namespace VoxelEngine.Tests
{
    public class BinaryTests
    {
        [Test]
        public void TrailingZeros()
        {
            ulong number1 = 0;
            ulong number2 =0b1000000000000000000000000000000000000000000000000000000100010001UL;
            ulong number3 =0b0000010000000000000000000000000000000000000000000000000100010000UL;
            ulong number4 =0b0000010000000000000000000000000000000000000000000000000100000000UL;
            ulong number5 =0b1000000000000000000000000000000000000000000000000000000000000000UL;
            Assert.AreEqual(64, number1.CountTrailingZeros());
            Assert.AreEqual(0, number2.CountTrailingZeros());
            Assert.AreEqual(4, number3.CountTrailingZeros());
            Assert.AreEqual(8, number4.CountTrailingZeros());
            Assert.AreEqual(63, number5.CountTrailingZeros());
        }
    }
}