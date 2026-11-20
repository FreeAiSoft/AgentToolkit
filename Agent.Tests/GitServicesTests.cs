using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Agent.Core.Services;

namespace Agent.Tests
{
    /// <summary>
    /// Pure helpers for parsing git output and sanitizing the git child-process environment,
    /// extracted so they can be tested without a git executable.
    /// </summary>
    [TestFixture]
    public class GitServicesTests
    {
        // ---------- GitOutputParser ----------

        [Test]
        public void SplitLines_HandlesMixedNewlines()
        {
            var lines = GitOutputParser.SplitLines("a\nb\r\nc\rd").ToArray();

            Assert.That(lines, Is.EqualTo(new[] { "a", "b", "c", "d" }));
        }

        [Test]
        public void SplitLines_NullEmptyAndWhitespace()
        {
            Assert.That(GitOutputParser.SplitLines(null), Is.Empty);
            Assert.That(GitOutputParser.SplitLines(""), Is.Empty);
            Assert.That(GitOutputParser.SplitLines("   "), Is.EqualTo(new[] { "   " }));
        }

        [Test]
        public void ExtractChangedPath_ReturnsPath()
        {
            Assert.That(GitOutputParser.ExtractChangedPath(" M src/file.txt"), Is.EqualTo("src/file.txt"));
            Assert.That(GitOutputParser.ExtractChangedPath("?? new.txt"), Is.EqualTo("new.txt"));
        }

        [Test]
        public void ExtractChangedPath_ShortOrNull_ReturnsNull()
        {
            Assert.That(GitOutputParser.ExtractChangedPath(null), Is.Null);
            Assert.That(GitOutputParser.ExtractChangedPath(""), Is.Null);
            Assert.That(GitOutputParser.ExtractChangedPath("MM"), Is.Null);
        }

        [Test]
        public void FormatLoggedOutput_TrimsTrailingWhitespace()
        {
            Assert.That(GitOutputParser.FormatLoggedOutput(null), Is.EqualTo(""));
            Assert.That(GitOutputParser.FormatLoggedOutput(""), Is.EqualTo(""));
            Assert.That(GitOutputParser.FormatLoggedOutput("  hello  "), Is.EqualTo("  hello"));
        }

        [Test]
        public void FormatLoggedOutput_TruncatesLongText()
        {
            var longText = new string('a', 100);
            var result = GitOutputParser.FormatLoggedOutput(longText, 10);

            Assert.That(result, Does.StartWith(new string('a', 10)));
            Assert.That(result, Does.Contain("output truncated"));
            Assert.That(result, Does.Contain("100 chars total"));
        }

        // ---------- GitEnvironmentSanitizer ----------

        [Test]
        public void SanitizedVariables_ContainsTheSevenRedirectors()
        {
            Assert.That(GitEnvironmentSanitizer.SanitizedVariables, Has.Count.EqualTo(7));
            Assert.That(GitEnvironmentSanitizer.SanitizedVariables, Does.Contain("GIT_DIR"));
            Assert.That(GitEnvironmentSanitizer.SanitizedVariables, Does.Contain("GIT_WORK_TREE"));
        }

        [Test]
        public void Sanitize_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => GitEnvironmentSanitizer.Sanitize(null));
        }

        [Test]
        public void Sanitize_RemovesRedirectingVariables()
        {
            var psi = new ProcessStartInfo { FileName = "git" };
            psi.Environment["GIT_DIR"] = "/tmp/x";
            psi.Environment["GIT_WORK_TREE"] = "/tmp/x";
            psi.Environment["MY_VAR"] = "keep";

            GitEnvironmentSanitizer.Sanitize(psi);

            Assert.That(psi.Environment.ContainsKey("GIT_DIR"), Is.False);
            Assert.That(psi.Environment.ContainsKey("GIT_WORK_TREE"), Is.False);
            Assert.That(psi.Environment["MY_VAR"], Is.EqualTo("keep"));
        }

        [Test]
        public void WithForcedConfig_HandlesNullEmptyAndArguments()
        {
            Assert.That(
                GitEnvironmentSanitizer.WithForcedConfig(null),
                Is.EqualTo(GitEnvironmentSanitizer.ForcedConfigArguments));
            Assert.That(
                GitEnvironmentSanitizer.WithForcedConfig(""),
                Is.EqualTo(GitEnvironmentSanitizer.ForcedConfigArguments));
            Assert.That(
                GitEnvironmentSanitizer.WithForcedConfig("log --oneline"),
                Is.EqualTo(GitEnvironmentSanitizer.ForcedConfigArguments + " log --oneline"));
        }
    }
}
