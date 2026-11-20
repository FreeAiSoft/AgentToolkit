using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Agent.Core.AgentTools;
using Agent.Core.Models;

namespace Agent.Tests
{
    [TestFixture]
    public class NativeToolCatalogTests
    {
        [Test]
        public void All_HasExpectedCoreTools()
        {
            var names = NativeToolCatalog.All.Select(e => e.Name).ToList();

            Assert.That(names, Does.Contain("read_files"));
            Assert.That(names, Does.Contain("build"));
            Assert.That(names, Does.Contain("editor"));
            Assert.That(names, Does.Contain("run_commands"));
            Assert.That(names, Does.Contain("ask_question"));
            Assert.That(names, Does.Contain("submit_and_exit"));
            Assert.That(names, Does.Contain("update_memory"));
        }

        [Test]
        public void All_NamesAreUniqueAndWellFormed()
        {
            var names = NativeToolCatalog.All.Select(e => e.Name).ToList();

            Assert.That(names, Is.Unique);
            Assert.That(names.Any(n => string.IsNullOrWhiteSpace(n)), Is.False);
        }

        [Test]
        public void All_EntriesHaveGroupAndDescription()
        {
            var invalid = NativeToolCatalog.All.Any(e =>
                string.IsNullOrWhiteSpace(e.Group) || string.IsNullOrWhiteSpace(e.Description));

            Assert.That(invalid, Is.False);
        }

        [TestCase("build", true)]
        [TestCase("editor", true)]
        [TestCase("run_commands", true)]
        [TestCase("download_file", true)]
        [TestCase("update_memory", true)]
        [TestCase("read_files", false)]
        [TestCase("git_status", false)]
        [TestCase("ask_question", false)]
        [TestCase("submit_and_exit", false)]
        public void MutatesFlag_MatchesMutationSemantics(string name, bool mutates)
        {
            var entry = NativeToolCatalog.All.Single(e => e.Name == name);

            Assert.That(entry.Mutates, Is.EqualTo(mutates));
        }
    }

    [TestFixture]
    public class AgentToolCatalogEntryTests
    {
        [Test]
        public void Constructor_AssignsProperties()
        {
            var entry = new AgentToolCatalogEntry("read_files", "Files", "Read files", mutates: false);

            Assert.That(entry.Name, Is.EqualTo("read_files"));
            Assert.That(entry.Group, Is.EqualTo("Files"));
            Assert.That(entry.Description, Is.EqualTo("Read files"));
            Assert.That(entry.Mutates, Is.False);
        }
    }

    [TestFixture]
    public class ToolApprovalTests
    {
        [Test]
        public void DecisionEnum_HasApproveAndReject()
        {
            Assert.That((int)ToolApprovalDecision.Approve, Is.EqualTo(0));
            Assert.That((int)ToolApprovalDecision.Reject, Is.EqualTo(1));
        }

        [Test]
        public void Request_DefaultsAreEmpty()
        {
            var request = new ToolApprovalRequest();

            Assert.That(request.ToolName, Is.EqualTo(""));
            Assert.That(request.Permission, Is.EqualTo((AgentPermissions)0));
            Assert.That(request.Mutates, Is.False);
            Assert.That(request.Description, Is.EqualTo(""));
            Assert.That(request.ArgumentsSummary, Is.EqualTo(""));
        }

        [Test]
        public async Task Gate_ReceivesRequestAndToken()
        {
            ToolApprovalRequest received = null;
            using var cts = new CancellationTokenSource();
            var token = cts.Token;

            ToolApprovalGate gate = (request, ct) =>
            {
                received = request;
                Assert.That(ct, Is.EqualTo(token));
                return Task.FromResult(ToolApprovalDecision.Approve);
            };

            var decision = await gate(new ToolApprovalRequest { ToolName = "editor" }, token);

            Assert.That(decision, Is.EqualTo(ToolApprovalDecision.Approve));
            Assert.That(received.ToolName, Is.EqualTo("editor"));
        }
    }

    [TestFixture]
    public class AgentToolRegistryEdgeTests
    {
        private static AgentTool ReadOnly(string name) =>
            new AgentTool(name, name, "{}", mutates: false, (a, c) => Task.FromResult("ok"));

        private static AgentTool Mutating(string name) =>
            new AgentTool(name, name, "{}", mutates: true, (a, c) => Task.FromResult("ok"));

        [Test]
        public void Register_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AgentToolRegistry().Register(null));
        }

        [Test]
        public void Register_SameNameOverwrites()
        {
            var registry = new AgentToolRegistry();
            registry.Register(ReadOnly("t"));
            registry.Register(Mutating("t"));

            Assert.That(registry.Find("t").Mutates, Is.True);
        }

        [Test]
        public void Find_NullOrWhitespace_ReturnsNull()
        {
            var registry = new AgentToolRegistry();
            Assert.That(registry.Find(null), Is.Null);
            Assert.That(registry.Find("  "), Is.Null);
        }

        [Test]
        public void Disable_HidesToolAndBlocksExecution()
        {
            var registry = new AgentToolRegistry();
            registry.Register(ReadOnly("read"));
            registry.Register(Mutating("write"));

            registry.Disable("write");

            Assert.That(registry.ForMode(AgentMode.Act).Select(t => t.Name), Is.EqualTo(new[] { "read" }));
            Assert.That(registry.TryGetExecutableTool("write", AgentMode.Act, AgentPermissions.All, out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("disabled"));
        }

        [Test]
        public void Disable_NullOrWhitespace_IsNoOp()
        {
            var registry = new AgentToolRegistry();
            registry.Register(ReadOnly("read"));

            registry.Disable(null);
            registry.Disable("   ");

            Assert.That(registry.ForMode(AgentMode.Act), Has.Count.EqualTo(1));
        }

        [Test]
        public void ForMode_Help_ExposesNoTools()
        {
            var registry = new AgentToolRegistry();
            registry.Register(ReadOnly("read"));

            Assert.That(registry.ForMode(AgentMode.Help), Is.Empty);
        }

        [Test]
        public void TryGetExecutableTool_Unknown_ReturnsBlockReason()
        {
            var registry = new AgentToolRegistry();

            Assert.That(registry.TryGetExecutableTool("nope", AgentMode.Act, AgentPermissions.All, out var tool, out var reason), Is.False);
            Assert.That(tool, Is.Null);
            Assert.That(reason, Does.Contain("Unknown tool"));
            Assert.That(reason, Does.Contain("nope"));
        }

        [Test]
        public void TryGetExecutableTool_MutatingInPlan_ReturnsPlanReason()
        {
            var registry = new AgentToolRegistry();
            registry.Register(Mutating("write"));

            Assert.That(registry.TryGetExecutableTool("write", AgentMode.Plan, AgentPermissions.All, out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("Plan mode"));
        }

        [Test]
        public void TryGetExecutableTool_HelpMode_ReturnsHelpReason()
        {
            var registry = new AgentToolRegistry();
            registry.Register(ReadOnly("read"));

            Assert.That(registry.TryGetExecutableTool("read", AgentMode.Help, AgentPermissions.All, out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("Help mode"));
        }

        [Test]
        public void TryGetExecutableTool_DisabledPermission_ReturnsPermissionReason()
        {
            var registry = new AgentToolRegistry();
            registry.Register(new AgentTool("run", "run", "{}", mutates: true,
                (a, c) => Task.FromResult("ok"), permission: AgentPermissions.ExecuteCommand));

            Assert.That(registry.TryGetExecutableTool("run", AgentMode.Act, AgentPermissions.ReadFiles, out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("permission"));
        }

        [Test]
        public void TryGetExecutableTool_Allowed_ReturnsToolAndNullReason()
        {
            var registry = new AgentToolRegistry();
            registry.Register(ReadOnly("read"));

            Assert.That(registry.TryGetExecutableTool("read", AgentMode.Plan, AgentPermissions.All, out var tool, out var reason), Is.True);
            Assert.That(tool.Name, Is.EqualTo("read"));
            Assert.That(reason, Is.Null);
        }
    }

    [TestFixture]
    public class AgentToolTests
    {
        [Test]
        public void Constructor_NullName_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentTool(null, "", "{}", false, (a, c) => Task.FromResult("")));
        }

        [Test]
        public void Constructor_NullHandler_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AgentTool("t", "", "{}", false, null));
        }

        [Test]
        public void Constructor_DefaultsDescriptionSchemaAndPermission()
        {
            var read = new AgentTool("t", null, null, mutates: false, (a, c) => Task.FromResult(""));
            Assert.That(read.Description, Is.EqualTo(""));
            Assert.That(read.ParametersSchema, Is.EqualTo("{}"));
            Assert.That(read.Permission, Is.EqualTo(AgentPermissions.ReadFiles));

            var write = new AgentTool("t", null, null, mutates: true, (a, c) => Task.FromResult(""));
            Assert.That(write.Permission, Is.EqualTo(AgentPermissions.EditFiles));
        }

        [Test]
        public async Task ExecuteAsync_PassesNonNullArguments()
        {
            JObject received = null;
            var tool = new AgentTool("t", "", "{}", false, (a, c) =>
            {
                received = a;
                return Task.FromResult("ok");
            });

            await tool.ExecuteAsync(null, CancellationToken.None);

            Assert.That(received, Is.Not.Null);
            Assert.That(received, Is.Empty);
        }

        [Test]
        public async Task GetDisplayResultAsync_NoDisplayHandler_ReturnsNull()
        {
            var tool = new AgentTool("t", "", "{}", false, (a, c) => Task.FromResult("ok"));

            Assert.That(await tool.GetDisplayResultAsync(null, CancellationToken.None), Is.Null);
        }

        [Test]
        public async Task GetDisplayResultAsync_UsesDisplayHandler()
        {
            var tool = new AgentTool("t", "", "{}", false,
                (a, c) => Task.FromResult("full"),
                (a, c) => Task.FromResult("short"));

            Assert.That(await tool.GetDisplayResultAsync(null, CancellationToken.None), Is.EqualTo("short"));
        }
    }
}
