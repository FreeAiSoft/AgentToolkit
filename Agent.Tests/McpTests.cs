using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Agent.Core.Mcp;

namespace Agent.Tests
{
    [TestFixture]
    public class McpServerSettingsTests
    {
        [Test]
        public void Parse_NullOrEmpty_ReturnsEmpty()
        {
            Assert.That(McpServerSettings.Parse(null), Is.Empty);
            Assert.That(McpServerSettings.Parse(""), Is.Empty);
            Assert.That(McpServerSettings.Parse("   "), Is.Empty);
        }

        [Test]
        public void Parse_InvalidJson_ReturnsEmpty()
        {
            Assert.That(McpServerSettings.Parse("not json"), Is.Empty);
        }

        [Test]
        public void Parse_WrappedObject_ParsesCommandServer()
        {
            const string json = @"{
                ""mcpServers"": {
                    ""playwright"": {
                        ""command"": ""npx"",
                        ""args"": [""@playwright/mcp"", ""--headless""],
                        ""env"": { ""PLAYWRIGHT_BROWSERS_PATH"": ""/tmp"" },
                        ""disabled"": true,
                        ""timeout"": 60
                    }
                }
            }";

            var servers = McpServerSettings.Parse(json);

            Assert.That(servers, Has.Count.EqualTo(1));
            var server = servers[0];
            Assert.That(server.Name, Is.EqualTo("playwright"));
            Assert.That(server.Command, Is.EqualTo("npx"));
            Assert.That(server.Args, Is.EqualTo(new[] { "@playwright/mcp", "--headless" }));
            Assert.That(server.Env["PLAYWRIGHT_BROWSERS_PATH"], Is.EqualTo("/tmp"));
            Assert.That(server.Disabled, Is.True);
            Assert.That(server.Enabled, Is.False);
            Assert.That(server.TimeoutSeconds, Is.EqualTo(60));
            Assert.That(server.Type, Is.EqualTo(McpTransportType.Stdio));
        }

        [Test]
        public void Parse_BareObjectWithoutWrapper_IsTolerated()
        {
            const string json = @"{ ""server"": { ""command"": ""uvx"", ""args"": [""x""] } }";

            var servers = McpServerSettings.Parse(json);

            Assert.That(servers, Has.Count.EqualTo(1));
            Assert.That(servers[0].Name, Is.EqualTo("server"));
            Assert.That(servers[0].Command, Is.EqualTo("uvx"));
        }

        [Test]
        public void Parse_UrlWithoutType_ResolvesToStreamableHttp()
        {
            const string json = @"{ ""mcpServers"": { ""http"": { ""url"": ""http://localhost:3000/mcp"" } } }";

            var server = McpServerSettings.Parse(json)[0];

            Assert.That(server.Url, Is.EqualTo("http://localhost:3000/mcp"));
            Assert.That(server.Type, Is.EqualTo(McpTransportType.StreamableHttp));
            Assert.That(server.IsRemote, Is.True);
        }

        [TestCase("sse", McpTransportType.Sse)]
        [TestCase("http", McpTransportType.StreamableHttp)]
        [TestCase("streamablehttp", McpTransportType.StreamableHttp)]
        [TestCase("streamable-http", McpTransportType.StreamableHttp)]
        [TestCase("stdio", McpTransportType.Stdio)]
        public void Parse_ExplicitType_IsResolved(string type, McpTransportType expected)
        {
            var json = @"{ ""mcpServers"": { ""s"": { ""command"": ""x"", ""type"": """ + type + @""" } } }";

            var server = McpServerSettings.Parse(json)[0];

            Assert.That(server.Type, Is.EqualTo(expected));
        }

        [Test]
        public void ToJson_RoundTrips()
        {
            const string json = @"{
                ""mcpServers"": {
                    ""a"": { ""command"": ""npx"", ""args"": [""x""], ""env"": { ""K"": ""V"" } },
                    ""b"": { ""url"": ""http://h/mcp"", ""timeout"": 45 }
                }
            }";

            var first = McpServerSettings.Parse(json);
            var serialized = McpServerSettings.ToJson(first);
            var second = McpServerSettings.Parse(serialized);

            Assert.That(second, Has.Count.EqualTo(2));
            Assert.That(second[0].Name, Is.EqualTo("a"));
            Assert.That(second[0].Command, Is.EqualTo("npx"));
            Assert.That(second[0].Args, Is.EqualTo(new[] { "x" }));
            Assert.That(second[0].Env["K"], Is.EqualTo("V"));
            Assert.That(second[1].Name, Is.EqualTo("b"));
            Assert.That(second[1].Url, Is.EqualTo("http://h/mcp"));
            Assert.That(second[1].TimeoutSeconds, Is.EqualTo(45));
        }

        [Test]
        public void ToJson_SkipsNullAndUnnamedServers()
        {
            var json = McpServerSettings.ToJson(new McpServerConfig[]
            {
                null,
                new McpServerConfig { Name = "", Command = "x" },
                new McpServerConfig { Name = "ok", Command = "x" }
            });

            var parsed = McpServerSettings.Parse(json);
            Assert.That(parsed, Has.Count.EqualTo(1));
            Assert.That(parsed[0].Name, Is.EqualTo("ok"));
        }
    }

    [TestFixture]
    public class McpSchemaConverterTests
    {
        [Test]
        public void ToInformal_NullOrNoProperties_ReturnsEmptyObject()
        {
            Assert.That(McpSchemaConverter.ToInformal(null), Is.EqualTo("{}"));
            Assert.That(McpSchemaConverter.ToInformal(new JObject()), Is.EqualTo("{}"));
            Assert.That(McpSchemaConverter.ToInformal(JObject.Parse("{\"type\":\"object\"}")), Is.EqualTo("{}"));
        }

        [Test]
        public void ToInformal_MapsTypesRequiredEnumNestedAndArray()
        {
            var schema = JObject.Parse(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""url"": { ""type"": ""string"" },
                    ""width"": { ""type"": ""integer"" },
                    ""ratio"": { ""type"": ""number"" },
                    ""ok"": { ""type"": ""boolean"" },
                    ""tags"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } },
                    ""nested"": { ""type"": ""object"", ""properties"": { ""x"": { ""type"": ""integer"" } } },
                    ""mode"": { ""type"": ""string"", ""enum"": [""a"", ""b""] }
                },
                ""required"": [""url""]
            }");

            var informal = McpSchemaConverter.ToInformal(schema);

            Assert.That(informal, Does.Contain("\"url\":\"string\""));
            Assert.That(informal, Does.Contain("\"width?\":\"integer\""));
            Assert.That(informal, Does.Contain("\"ratio?\":\"number\""));
            Assert.That(informal, Does.Contain("\"ok?\":\"boolean\""));
            Assert.That(informal, Does.Contain("\"tags?\":[\"string\"]"));
            Assert.That(informal, Does.Contain("\"nested?\":{\"x\":\"integer\"}"));
            Assert.That(informal, Does.Contain("\"mode?\":\"a|b\""));
        }
    }

    [TestFixture]
    public class McpToolResultParserTests
    {
        [Test]
        public void ToText_Null_ReturnsEmptyPlaceholder()
        {
            Assert.That(McpToolResultParser.ToText(null), Is.EqualTo("(empty MCP result)"));
        }

        [Test]
        public void ToText_ConcatenatesTextBlocks()
        {
            var result = new McpCallToolResult
            {
                Content = new[]
                {
                    new McpContentBlock { Type = "text", Text = "a" },
                    new McpContentBlock { Type = "text", Text = "b" }
                }
            };

            Assert.That(McpToolResultParser.ToText(result), Is.EqualTo("a\nb"));
        }

        [Test]
        public void ToText_ImageBlock_ReportsPlaceholder()
        {
            var result = new McpCallToolResult
            {
                Content = new[] { new McpContentBlock { Type = "image", MimeType = "image/png", Data = "abcd" } }
            };

            Assert.That(McpToolResultParser.ToText(result), Is.EqualTo("[image: image/png (base64, 4 chars)]"));
        }

        [Test]
        public void ToText_ResourceBlock_ReportsUri()
        {
            var result = new McpCallToolResult
            {
                Content = new[] { new McpContentBlock { Type = "resource", ResourceUri = "file:///x" } }
            };

            Assert.That(McpToolResultParser.ToText(result), Is.EqualTo("[resource: file:///x]"));
        }

        [Test]
        public void ToText_Error_PrefixesAndFillsEmptyContent()
        {
            var empty = new McpCallToolResult { IsError = true, Content = new List<McpContentBlock>() };
            Assert.That(
                McpToolResultParser.ToText(empty),
                Is.EqualTo("[MCP error] (MCP tool returned an error with no content)"));

            var withText = new McpCallToolResult
            {
                IsError = true,
                Content = new[] { new McpContentBlock { Type = "text", Text = "boom" } }
            };
            Assert.That(McpToolResultParser.ToText(withText), Is.EqualTo("[MCP error] boom"));
        }

        [Test]
        public void ToText_EmptyContent_ReturnsNoContent()
        {
            var result = new McpCallToolResult { Content = new List<McpContentBlock>() };
            Assert.That(McpToolResultParser.ToText(result), Is.EqualTo("(no content)"));
        }
    }

    [TestFixture]
    public class McpToolResultModelTests
    {
        [Test]
        public void CallToolResult_FromJson_ParsesContent()
        {
            var json = JObject.Parse(@"{ ""isError"": false, ""content"": [ { ""type"": ""text"", ""text"": ""hi"" } ] }");

            var result = McpCallToolResult.FromJson(json);

            Assert.That(result.IsError, Is.False);
            Assert.That(result.Content, Has.Count.EqualTo(1));
            Assert.That(result.Content[0].Type, Is.EqualTo("text"));
            Assert.That(result.Content[0].Text, Is.EqualTo("hi"));
        }

        [Test]
        public void CallToolResult_FromJson_StructuredContentBecomesText()
        {
            var json = JObject.Parse(@"{ ""structuredContent"": { ""a"": 1 } }");

            var result = McpCallToolResult.FromJson(json);

            Assert.That(result.Content, Has.Count.EqualTo(1));
            Assert.That(result.Content[0].Type, Is.EqualTo("text"));
            Assert.That(result.Content[0].Text, Does.Contain("a"));
        }

        [Test]
        public void CallToolResult_FromJson_Null_ReturnsEmpty()
        {
            var result = McpCallToolResult.FromJson(null);
            Assert.That(result.IsError, Is.False);
            Assert.That(result.Content, Is.Empty);
        }

        [Test]
        public void Resource_FromJson_ParsesFields()
        {
            var list = McpResource.FromList(JArray.Parse(@"[{ ""uri"": ""u"", ""name"": ""n"", ""description"": ""d"", ""mimeType"": ""t"" }]"));

            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list[0].Uri, Is.EqualTo("u"));
            Assert.That(list[0].Name, Is.EqualTo("n"));
            Assert.That(list[0].Description, Is.EqualTo("d"));
            Assert.That(list[0].MimeType, Is.EqualTo("t"));
        }

        [Test]
        public void Prompt_FromJson_ParsesFields()
        {
            var list = McpPrompt.FromList(JArray.Parse(@"[{ ""name"": ""p"", ""description"": ""d"" }]"));

            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list[0].Name, Is.EqualTo("p"));
            Assert.That(list[0].Description, Is.EqualTo("d"));
        }

        [Test]
        public void Tool_FromJson_DefaultsSchemaWhenMissing()
        {
            var tool = McpTool.FromJson(JObject.Parse(@"{ ""name"": ""t"", ""description"": ""d"" }"));

            Assert.That(tool.Name, Is.EqualTo("t"));
            Assert.That(tool.Description, Is.EqualTo("d"));
            Assert.That(tool.InputSchema, Is.Not.Null);
            Assert.That(tool.InputSchema, Is.Empty);
        }

        [Test]
        public void ResourceContent_FromJson_ParsesTextAndBlob()
        {
            var json = JObject.Parse(@"{ ""contents"": [
                { ""uri"": ""u1"", ""text"": ""hello"" },
                { ""uri"": ""u2"", ""blob"": ""ZGF0YQ=="", ""mimeType"": ""image/png"" }
            ] }");

            var content = McpResourceContent.FromJson(json);

            Assert.That(content.Contents, Has.Count.EqualTo(2));
            Assert.That(content.Contents[0].Type, Is.EqualTo("text"));
            Assert.That(content.Contents[0].Text, Is.EqualTo("hello"));
            Assert.That(content.Contents[1].Type, Is.EqualTo("image"));
            Assert.That(content.Contents[1].Data, Is.EqualTo("ZGF0YQ=="));
        }

        [Test]
        public void PromptResult_ToText_FlattensMessages()
        {
            var json = JObject.Parse(@"{ ""description"": ""d"", ""messages"": [
                { ""role"": ""user"", ""content"": ""hi"" },
                { ""role"": ""assistant"", ""content"": { ""type"": ""text"", ""text"": ""yo"" } }
            ] }");

            var result = McpPromptResult.FromJson(json);

            Assert.That(result.Description, Is.EqualTo("d"));
            Assert.That(result.ToText(), Is.EqualTo("[user] hi" + Environment.NewLine + "[assistant] yo"));
        }

        [Test]
        public void McpException_FromJson_ExtractsCodeAndMessage()
        {
            var ex = McpException.FromJson(JObject.Parse(@"{ ""code"": -32602, ""message"": ""boom"" }"));

            Assert.That(ex.Code, Is.EqualTo(-32602));
            Assert.That(ex.Message, Is.EqualTo("boom"));
        }
    }

    [TestFixture]
    public class McpServerConfigTests
    {
        [Test]
        public void Defaults()
        {
            var config = new McpServerConfig();

            Assert.That(config.Name, Is.EqualTo(""));
            Assert.That(config.Command, Is.EqualTo(""));
            Assert.That(config.Args, Is.Empty);
            Assert.That(config.Env, Is.Empty);
            Assert.That(config.Type, Is.EqualTo(McpTransportType.Stdio));
            Assert.That(config.TimeoutSeconds, Is.EqualTo(30));
            Assert.That(config.Enabled, Is.True);
            Assert.That(config.IsRemote, Is.False);
        }
    }

    [TestFixture]
    public class McpToolAdapterTests
    {
        [Test]
        public void Constructor_NullTool_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new McpToolAdapter("s", null, McpTestHelpers.NewInitializedClient(new FakeMcpTransport())));
        }

        [Test]
        public void Constructor_NullClient_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new McpToolAdapter("s", new McpTool(), null));
        }

        [Test]
        public void Constructor_NamespacesAndSanitizes()
        {
            var tool = new McpTool { Name = "click", Description = "  click the thing  " };
            var adapter = new McpToolAdapter("playwright", tool, McpTestHelpers.NewInitializedClient(new FakeMcpTransport()));

            Assert.That(adapter.Name, Is.EqualTo("mcp__playwright__click"));
            Assert.That(adapter.Description, Is.EqualTo("click the thing"));
            Assert.That(adapter.Mutates, Is.True);
            Assert.That(adapter.Permission, Is.EqualTo(Agent.Core.Models.AgentPermissions.Mcp));
            Assert.That(adapter.CompletesRun, Is.False);
        }

        [Test]
        public void ExecuteAsync_ClientNotInitialized_ReturnsFailureText()
        {
            var transport = new FakeMcpTransport();
            var client = new McpClient(transport); // not initialized
            var adapter = new McpToolAdapter("s", new McpTool { Name = "t" }, client);

            var result = adapter.ExecuteAsync(new JObject(), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result, Does.Contain("MCP tool \"t\" failed"));
        }

        [Test]
        public void ExecuteAsync_ReturnsParsedText()
        {
            var transport = new FakeMcpTransport();
            var client = McpTestHelpers.NewInitializedClient(transport);
            transport.Results.Enqueue(new JObject
            {
                ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = "done" })
            });
            var adapter = new McpToolAdapter("s", new McpTool { Name = "t" }, client);

            var result = adapter.ExecuteAsync(new JObject(), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result, Is.EqualTo("done"));
        }
    }

    [TestFixture]
    public class McpContextToolsTests
    {
        [Test]
        public void Create_NullClient_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => McpContextTools.Create("s", null));
        }

        [Test]
        public void Create_ReturnsFourNamespacedTools()
        {
            var tools = McpContextTools.Create("server", McpTestHelpers.NewInitializedClient(new FakeMcpTransport()));

            Assert.That(tools, Has.Count.EqualTo(4));
            Assert.That(tools.Select(t => t.Name), Is.EqualTo(new[]
            {
                "mcp__server__list_resources",
                "mcp__server__read_resource",
                "mcp__server__list_prompts",
                "mcp__server__get_prompt"
            }));
        }
    }

    [TestFixture]
    public class McpClientTests
    {
        [Test]
        public void Constructor_NullTransport_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new McpClient(null));
        }

        [Test]
        public async Task InitializeAsync_PerformsHandshake()
        {
            var transport = new FakeMcpTransport();
            transport.Results.Enqueue(new JObject { ["protocolVersion"] = McpClient.ProtocolVersion });
            var client = new McpClient(transport);

            await client.InitializeAsync(CancellationToken.None);

            Assert.That(client.IsInitialized, Is.True);
            Assert.That(transport.IsStarted, Is.True);
            Assert.That(transport.Sent, Has.Count.EqualTo(2));
            Assert.That(transport.Sent[0], Does.Contain("\"method\":\"initialize\""));
            Assert.That(transport.Sent[1], Does.Contain("\"method\":\"notifications/initialized\""));
        }

        [Test]
        public void CallToolAsync_BeforeInitialize_Throws()
        {
            var client = new McpClient(new FakeMcpTransport());

            Assert.ThrowsAsync<InvalidOperationException>(
                () => client.CallToolAsync("t", new JObject(), CancellationToken.None));
        }

        [Test]
        public async Task CallToolAsync_ReturnsParsedResult()
        {
            var transport = new FakeMcpTransport();
            var client = McpTestHelpers.NewInitializedClient(transport);

            transport.Results.Enqueue(new JObject
            {
                ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = "hello" })
            });

            var result = await client.CallToolAsync("t", new JObject { ["x"] = 1 }, CancellationToken.None);

            Assert.That(result.IsError, Is.False);
            Assert.That(result.Content[0].Text, Is.EqualTo("hello"));
        }

        [Test]
        public async Task CallToolAsync_ErrorResponse_ThrowsMcpException()
        {
            var transport = new FakeMcpTransport();
            var client = McpTestHelpers.NewInitializedClient(transport);

            transport.Errors.Enqueue(new McpException(-32602, "boom"));

            var ex = Assert.ThrowsAsync<McpException>(() => client.CallToolAsync("t", null, CancellationToken.None));
            Assert.That(ex.Code, Is.EqualTo(-32602));
        }

        [Test]
        public async Task ListToolsAsync_ReturnsTools()
        {
            var transport = new FakeMcpTransport();
            var client = McpTestHelpers.NewInitializedClient(transport);

            transport.Results.Enqueue(new JObject
            {
                ["tools"] = new JArray(new JObject { ["name"] = "t", ["description"] = "d" })
            });

            var tools = await client.ListToolsAsync(CancellationToken.None);

            Assert.That(tools, Has.Count.EqualTo(1));
            Assert.That(tools[0].Name, Is.EqualTo("t"));
        }

        [Test]
        public async Task ProgressNotification_RaisesEvent()
        {
            var transport = new FakeMcpTransport();
            var client = McpTestHelpers.NewInitializedClient(transport);

            McpProgressEventArgs received = null;
            client.ProgressReceived += (s, e) => received = e;

            transport.Raise(new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "notifications/progress",
                ["params"] = new JObject { ["progressToken"] = "tok", ["progress"] = 42, ["total"] = 100 }
            });

            Assert.That(received, Is.Not.Null);
            Assert.That(received.ProgressToken, Is.EqualTo("tok"));
            Assert.That(received.Progress, Is.EqualTo(42));
            Assert.That(received.Total, Is.EqualTo(100));
        }

        [Test]
        public void Dispose_StopsTransportAndThrowsAfter()
        {
            var transport = new FakeMcpTransport();
            var client = McpTestHelpers.NewInitializedClient(transport);

            client.Dispose();

            Assert.That(transport.Disposed, Is.True);
            Assert.ThrowsAsync<ObjectDisposedException>(() => client.CallToolAsync("t", null, CancellationToken.None));
        }
    }

    internal static class McpTestHelpers
    {
        public static McpClient NewInitializedClient(FakeMcpTransport transport)
        {
            var client = new McpClient(transport);
            transport.Results.Enqueue(new JObject { ["protocolVersion"] = McpClient.ProtocolVersion });
            client.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
            return client;
        }
    }

    /// <summary>In-memory transport that answers queued requests with canned results/errors.</summary>
    internal sealed class FakeMcpTransport : IMcpTransport
    {
        public bool IsStarted { get; private set; }
        public bool Disposed { get; private set; }
        public List<string> Sent { get; } = new List<string>();
        public Queue<JObject> Results { get; } = new Queue<JObject>();
        public Queue<McpException> Errors { get; } = new Queue<McpException>();

        public event EventHandler<McpMessageEventArgs> MessageReceived;
        public event EventHandler<McpMessageEventArgs> StderrLine;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            IsStarted = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendAsync(string json, CancellationToken cancellationToken)
        {
            Sent.Add(json);

            var request = JObject.Parse(json);
            var idToken = request["id"];
            if (idToken == null)
                return Task.CompletedTask;

            if (Errors.Count > 0)
            {
                var error = Errors.Dequeue();
                Raise(new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = idToken,
                    ["error"] = new JObject { ["code"] = error.Code, ["message"] = error.Message }
                });
            }
            else if (Results.Count > 0)
            {
                Raise(new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = idToken,
                    ["result"] = Results.Dequeue()
                });
            }

            return Task.CompletedTask;
        }

        public void Raise(JObject message)
        {
            MessageReceived?.Invoke(this, new McpMessageEventArgs { Json = message.ToString(Formatting.None) });
        }

        public void Dispose() => Disposed = true;
    }
}
