using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Agent.Core.AgentTools;
using Agent.Core.Models;

namespace Agent.Tests
{
    [TestFixture]
    public class AgentToolEnvironmentTests
    {
        [Test]
        public void BuildPrompt_NullRegistry_Throws()
        {
            var env = new AgentToolEnvironment();
            Assert.Throws<InvalidOperationException>(() => env.BuildPrompt(AgentMode.Act));
        }

        [Test]
        public void BuildToolDefinitions_NullRegistry_Throws()
        {
            var env = new AgentToolEnvironment();
            Assert.Throws<InvalidOperationException>(() => env.BuildToolDefinitions(AgentMode.Act));
        }

        [Test]
        public void BuildPrompt_WithRegistry_IncludesToolsAndAddressing()
        {
            var registry = new AgentToolRegistry();
            registry.Register(new AgentTool("read_file", "Read files", "{}", mutates: false, (a, c) => Task.FromResult("ok")));

            var env = new AgentToolEnvironment
            {
                Registry = registry,
                ToolCallingMode = ToolCallingMode.Native,
                AllowedPermissions = AgentPermissions.All,
                UserName = "Максим",
                AddressByName = true
            };

            var prompt = env.BuildPrompt(AgentMode.Act);

            Assert.That(prompt, Does.Contain("read_file"));
            Assert.That(prompt, Does.Contain("Максим"));
        }

        [Test]
        public void BuildToolDefinitions_ReturnsRegisteredTools()
        {
            var registry = new AgentToolRegistry();
            registry.Register(new AgentTool("read_file", "Read files", "{}", mutates: false, (a, c) => Task.FromResult("ok")));

            var env = new AgentToolEnvironment
            {
                Registry = registry,
                AllowedPermissions = AgentPermissions.All
            };

            var definitions = env.BuildToolDefinitions(AgentMode.Act);

            Assert.That(definitions, Has.Count.EqualTo(1));
            Assert.That(definitions[0].Name, Is.EqualTo("read_file"));
        }
    }
}
