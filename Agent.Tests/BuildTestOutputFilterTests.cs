using NUnit.Framework;
using Agent.Core.Services;

namespace Agent.Tests
{
    [TestFixture]
    public class BuildTestOutputFilterTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void IsSupported_EmptyOrWhitespace_ReturnsFalse(string command)
        {
            Assert.That(BuildTestOutputFilter.IsSupported(command), Is.False);
        }

        [TestCase("dotnet build")]
        [TestCase("dotnet test")]
        [TestCase("dotnet vstest")]
        [TestCase("dotnet msbuild")]
        [TestCase("msbuild")]
        [TestCase("vstest.console")]
        public void IsSupported_RecognizesBuildAndTestCommands(string command)
        {
            Assert.That(BuildTestOutputFilter.IsSupported(command), Is.True);
        }

        [Test]
        public void IsSupported_RejectsUnrelatedCommands()
        {
            Assert.That(BuildTestOutputFilter.IsSupported("git status"), Is.False);
        }

        [Test]
        public void Filter_BuildWithError_Summarizes()
        {
            var output = "C:\\src\\File.cs(12,5): error CS1002: ; expected\nBuild succeeded\n";

            var summary = BuildTestOutputFilter.Filter("dotnet build", 1, output);

            Assert.That(summary, Does.Contain("Errors (1):"));
            Assert.That(summary, Does.Contain("CS1002: ; expected"));
            Assert.That(summary, Does.Contain("Build failed (exit 1)."));
        }

        [Test]
        public void Filter_PassingTestRun_SummarizesTotals()
        {
            var output = "Passed!  - Failed: 0, Passed: 15, Skipped: 0, Total: 15, Duration: 1 s\n";

            var summary = BuildTestOutputFilter.Filter("dotnet test", 0, output);

            Assert.That(summary, Does.Contain("Test run: PASSED (Total: 15, Passed: 15, Failed: 0, Skipped: 0)"));
        }

        [Test]
        public void Filter_FailedTestRun_ListsFailedTestWithMessage()
        {
            var output =
                "Failed!  - Failed: 1, Passed: 2, Skipped: 0, Total: 3, Duration: 1 s\n" +
                "  Failed My.Tests.Foo [10 ms]\n" +
                "  Error Message:\n" +
                "   boom\n";

            var summary = BuildTestOutputFilter.Filter("dotnet test", 1, output);

            Assert.That(summary, Does.Contain("Test run: FAILED"));
            Assert.That(summary, Does.Contain("My.Tests.Foo: boom"));
        }

        [Test]
        public void Filter_UnrecognizedOutput_ReturnsOriginalText()
        {
            Assert.That(BuildTestOutputFilter.Filter("git status", 0, "random text"), Is.EqualTo("random text"));
        }
    }
}
