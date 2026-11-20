using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Agent.Core.AgentTools;

namespace Agent.Tests
{
    /// <summary>
    /// Guards the stagnant-loop detector used by <see cref="AgentRunner"/> to stop the agent
    /// from re-invoking the same tool with the same arguments forever.
    /// </summary>
    [TestFixture]
    public class ToolRepetitionDetectorTests
    {
        private static JObject Args(string path) => JObject.Parse("{\"path\":\"" + path + "\"}");

        [Test]
        public void BlocksOnlyAfterLimitConsecutiveIdenticalCalls()
        {
            var detector = new ToolRepetitionDetector(3);
            var args = Args("a.txt");

            Assert.That(detector.IsBlocked("read_file", args), Is.False);
            Assert.That(detector.IsBlocked("read_file", args), Is.False);
            Assert.That(detector.IsBlocked("read_file", args), Is.False);
            Assert.That(detector.IsBlocked("read_file", args), Is.True);
        }

        [Test]
        public void DifferentArgumentResetsCounter()
        {
            var detector = new ToolRepetitionDetector(2);

            Assert.That(detector.IsBlocked("read_file", Args("a.txt")), Is.False);
            Assert.That(detector.IsBlocked("read_file", Args("a.txt")), Is.False);
            Assert.That(detector.IsBlocked("read_file", Args("b.txt")), Is.False);
            Assert.That(detector.IsBlocked("read_file", Args("a.txt")), Is.False);
        }

        [Test]
        public void DifferentToolResetsCounter()
        {
            var detector = new ToolRepetitionDetector(1);
            var args = Args("a.txt");

            Assert.That(detector.IsBlocked("read_file", args), Is.False);
            Assert.That(detector.IsBlocked("write_file", args), Is.False);
        }

        [Test]
        public void ZeroLimitDisablesDetection()
        {
            var detector = new ToolRepetitionDetector(0);
            var args = Args("a.txt");

            Assert.That(detector.IsBlocked("read_file", args), Is.False);
            Assert.That(detector.IsBlocked("read_file", args), Is.False);
            Assert.That(detector.IsBlocked("read_file", args), Is.False);
        }

        [Test]
        public void RecoversAfterBlocking()
        {
            var detector = new ToolRepetitionDetector(1);
            var args = Args("a.txt");

            Assert.That(detector.IsBlocked("read_file", args), Is.False);
            Assert.That(detector.IsBlocked("read_file", args), Is.True);

            // After the block the detector resets, so a new approach can proceed.
            Assert.That(detector.IsBlocked("read_file", args), Is.False);
        }

        [Test]
        public void BuildSignature_IsIndependentOfKeyOrder()
        {
            var a = ToolRepetitionDetector.BuildSignature("t", JObject.Parse("{\"b\":2,\"a\":1}"));
            var b = ToolRepetitionDetector.BuildSignature("t", JObject.Parse("{\"a\":1,\"b\":2}"));

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void BuildSignature_NullArguments_TreatedAsEmptyObject()
        {
            Assert.That(
                ToolRepetitionDetector.BuildSignature("t", null),
                Is.EqualTo(ToolRepetitionDetector.BuildSignature("t", new JObject())));
        }

        [Test]
        public void Detection_IgnoresJsonKeyOrder()
        {
            var detector = new ToolRepetitionDetector(1);

            Assert.That(detector.IsBlocked("t", JObject.Parse("{\"a\":1,\"b\":2}")), Is.False);
            Assert.That(detector.IsBlocked("t", JObject.Parse("{\"b\":2,\"a\":1}")), Is.True);
        }
    }
}
