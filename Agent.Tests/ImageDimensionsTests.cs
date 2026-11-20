using NUnit.Framework;
using Agent.Core.Services;

namespace Agent.Tests
{
    [TestFixture]
    public class ImageDimensionsTests
    {
        [Test]
        public void Read_NullOrTooShort_ReturnsZero()
        {
            Assert.That(ImageDimensions.Read(null, 0), Is.EqualTo((0, 0)));
            Assert.That(ImageDimensions.Read(new byte[0], 0), Is.EqualTo((0, 0)));
            Assert.That(ImageDimensions.Read(new byte[] { 0, 0 }, 2), Is.EqualTo((0, 0)));
        }

        [Test]
        public void Read_Png_ReadsBigEndianDimensions()
        {
            var png = new byte[24];
            png[0] = 0x89; png[1] = 0x50; png[2] = 0x4E; png[3] = 0x47;
            png[4] = 0x0D; png[5] = 0x0A; png[6] = 0x1A; png[7] = 0x0A;
            png[16] = 0; png[17] = 0; png[18] = 0; png[19] = 100;   // width 100
            png[20] = 0; png[21] = 0; png[22] = 0; png[23] = 50;    // height 50

            Assert.That(ImageDimensions.Read(png, png.Length), Is.EqualTo((100, 50)));
        }

        [Test]
        public void Read_Gif_ReadsLittleEndianDimensions()
        {
            var gif = new byte[10];
            gif[0] = (byte)'G'; gif[1] = (byte)'I'; gif[2] = (byte)'F';
            gif[6] = 3; gif[7] = 0; // width 3
            gif[8] = 2; gif[9] = 0; // height 2

            Assert.That(ImageDimensions.Read(gif, gif.Length), Is.EqualTo((3, 2)));
        }

        [Test]
        public void Read_Bmp_ReadsLittleEndianDimensions()
        {
            var bmp = new byte[26];
            bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
            bmp[18] = 5; // width 5
            bmp[22] = 4; // height 4

            Assert.That(ImageDimensions.Read(bmp, bmp.Length), Is.EqualTo((5, 4)));
        }

        [Test]
        public void Read_UnknownData_ReturnsZero()
        {
            Assert.That(ImageDimensions.Read(new byte[] { 1, 2, 3, 4, 5, 6 }, 6), Is.EqualTo((0, 0)));
        }
    }
}
