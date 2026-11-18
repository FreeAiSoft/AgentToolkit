using NUnit.Framework;
using PackTool;

namespace Agent.Tests
{
    [TestFixture]
    public class VersionBumperTests
    {
        [Test]
        public void ComputeNext_ThreePartVersion_StartsRevisionAtOne()
        {
            Assert.That(VersionBumper.ComputeNext("1.7.2"), Is.EqualTo("1.7.2.1"));
        }

        [Test]
        public void ComputeNext_FourPartZeroRevision_IncrementsToOne()
        {
            Assert.That(VersionBumper.ComputeNext("1.16.2.0"), Is.EqualTo("1.16.2.1"));
        }

        [Test]
        public void ComputeNext_FourPartVersion_IncrementsRevision()
        {
            Assert.That(VersionBumper.ComputeNext("1.4.5.6"), Is.EqualTo("1.4.5.7"));
        }

        [Test]
        public void ComputeNext_NullOrEmpty_ReturnsDefault()
        {
            Assert.That(VersionBumper.ComputeNext(null), Is.EqualTo("1.0.0.0"));
            Assert.That(VersionBumper.ComputeNext(""), Is.EqualTo("1.0.0.0"));
        }

        [Test]
        public void FindMaxVersion_PicksHighest()
        {
            var result = VersionBumper.FindMaxVersion(new[] { "1.16.0", "1.16.2", "1.15.1" });

            Assert.That(result, Is.EqualTo("1.16.2"));
        }

        [Test]
        public void FindMaxVersion_FourPartBeatsThreePart()
        {
            var result = VersionBumper.FindMaxVersion(new[] { "1.16.2", "1.16.2.0" });

            Assert.That(result, Is.EqualTo("1.16.2.0"));
        }

        [Test]
        public void FindMaxVersion_IgnoresUnparsable()
        {
            var result = VersionBumper.FindMaxVersion(new[] { "garbage", "1.16.1" });

            Assert.That(result, Is.EqualTo("1.16.1"));
        }

        [Test]
        public void FindMaxVersion_Empty_ReturnsNull()
        {
            Assert.That(VersionBumper.FindMaxVersion(new string[0]), Is.Null);
        }
    }
}
