using System;
using System.IO;
using NUnit.Framework;

namespace Agent.Tests
{
    /// <summary>
    /// Regression guard for the broken per-message copy button. <c>ChatMessagesControl</c>
    /// raises <c>CopyMessageRequested</c>, but that event needs a subscriber in
    /// <c>ChatControl</c> that actually writes the formatted message to the clipboard.
    /// </summary>
    [TestFixture]
    public class ChatControlCopyGuardTests
    {
        private static string RepoRoot => FindRepoRoot();

        private static string CodePath => Path.Combine(RepoRoot, "Agent.Toolkit.Wpf", "Controls", "ChatControl.xaml.cs");

        [Test]
        public void ChatControl_SubscribesToPerMessageCopy()
        {
            var code = File.ReadAllText(CodePath);

            Assert.That(code, Does.Contain("ChatMessages.CopyMessageRequested +="),
                "ChatControl must wire the per-message copy event.");
            Assert.That(code, Does.Contain("OnCopyMessageRequested"),
                "ChatControl must implement the per-message copy handler.");
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Agent.Toolkit.Wpf")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "Agent.Tests")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate the AgentShared repository root.");
        }
    }
}