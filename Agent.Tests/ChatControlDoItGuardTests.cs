using System;
using System.IO;
using NUnit.Framework;

namespace Agent.Tests
{
    /// <summary>
    /// Regression guard for the broken "Do it!!!" footer button. The button delegates to
    /// <see cref="Agent.Models.ChatModel.DoItCommand"/>, which sends the prompt through an
    /// <see cref="Agent.Core.Contracts.IChatPromptSink"/>. That sink must be wired to the real
    /// <see cref="Agent.Toolkit.Wpf.Controls.IChatHost"/> agent loop; otherwise the button only
    /// switches the mode and never sends the message.
    /// </summary>
    [TestFixture]
    public class ChatControlDoItGuardTests
    {
        private static string RepoRoot => FindRepoRoot();

        private static string CodePath => Path.Combine(RepoRoot, "Agent.Toolkit.Wpf", "Controls", "ChatControl.xaml.cs");

        [Test]
        public void ChatControl_WiresPromptSinkToModel()
        {
            var code = File.ReadAllText(CodePath);

            Assert.That(code, Does.Contain("SetChatPromptSink"),
                "ChatControl must attach a prompt sink to the model so the Do it!!! button can send through the host.");
            Assert.That(code, Does.Contain("AttachPromptSink"),
                "ChatControl must re-attach the prompt sink when the model or host changes.");
            Assert.That(code, Does.Contain("new HostPromptSink"),
                "ChatControl must forward the Do it!!! prompt through the host agent loop.");
        }

        [Test]
        public void ChatControl_DoItClickDelegatesToModelCommand()
        {
            var code = File.ReadAllText(CodePath);

            Assert.That(code, Does.Contain("OnDoItClick"),
                "ChatControl must keep the Do it!!! button click handler.");
            Assert.That(code, Does.Contain("DoItCommand"),
                "OnDoItClick must delegate to ChatModel.DoItCommand.");
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
