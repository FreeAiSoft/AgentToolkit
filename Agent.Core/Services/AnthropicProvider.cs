using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Agent.Core.Context;
using Agent.Core.Contracts;
using Agent.Core.Models;

namespace Agent.Core.Services
{
    /// <summary>
    /// Anthropic (Claude) provider: POST {BaseUrl}/v1/messages with the Messages API.
    /// Supports native tool calling, prompt caching (system block) and extended thinking.
    /// </summary>
    public class AnthropicProvider : IAiProvider, IStreamingAiProvider, ITokenUsageProvider, IMessageNotifier, IPromptNotifier, IRetryNotifier, IPromptReloadable
    {
        private const string AnthropicVersion = "2023-06-01";

        private readonly HttpClient _http;
        private readonly AnthropicOptions _options;
        private PromptSet _prompts;
        private readonly TokenUsage _totalUsage = new TokenUsage();
        private readonly object _usageLock = new object();

        public event EventHandler<RetryAttemptEventArgs> RetryAttempt;
        public event EventHandler<TokenUsage> TokenUsageUpdated;
        public event EventHandler<AiMessageEventArgs> MessageSent;
        public event EventHandler<PromptAssembledEventArgs> PromptAssembled;

        private static readonly Random _random = new Random();

        static AnthropicProvider()
        {
            // On .NET Framework the default security protocol can still negotiate TLS 1.0/1.1,
            // which modern Anthropic endpoints reject by forcibly closing the connection.
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch
            {
                // Best-effort only.
            }
        }

        public AnthropicProvider(AnthropicOptions options) : this(options, new HttpClient()) { }

        public AnthropicProvider(AnthropicOptions options, HttpClient http)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _prompts = PromptSet.Load(_options.PromptsPath);
            if (_options.TimeoutSeconds > 0)
                _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }

        public void ReloadPromptsFromDisk()
        {
            _prompts = PromptSet.Load(_options.PromptsPath);
        }

        public TokenUsage TotalTokenUsage
        {
            get
            {
                lock (_usageLock)
                {
                    return new TokenUsage
                    {
                        PromptTokens = _totalUsage.PromptTokens,
                        CompletionTokens = _totalUsage.CompletionTokens,
                        TotalTokens = _totalUsage.TotalTokens,
                        CacheHitTokens = _totalUsage.CacheHitTokens,
                        CacheMissTokens = _totalUsage.CacheMissTokens,
                        EstimatedCostUsd = _totalUsage.EstimatedCostUsd
                    };
                }
            }
        }

        public int ContextWindowSize => _options.ContextWindowSize > 0 ? _options.ContextWindowSize : 409600;

        private void RaiseMessageSent(string role, string content)
        {
            MessageSent?.Invoke(this, new AiMessageEventArgs { Role = role, Content = content ?? "" });
        }

        private void RaisePromptAssembled(string systemPrompt, IReadOnlyList<ChatMessageData> messages)
        {
            PromptAssembled?.Invoke(this, new PromptAssembledEventArgs
            {
                SystemPrompt = systemPrompt ?? "",
                Messages = messages ?? new List<ChatMessageData>(),
                EstimatedInputTokens = new TokenEstimator().Estimate(systemPrompt, messages)
            });
        }

        public async Task<ChatResponse> ChatAsync(string message, CancellationToken cancellationToken = default)
        {
            RaiseMessageSent("user", message);
            var messages = new List<ChatMessageData> { new ChatMessageData { Role = "user", Content = message } };
            RaisePromptAssembled(_prompts.ChatSystemPrompt, messages);
            var obj = await SendAsync(() => BuildRequest(messages, _prompts.ChatSystemPrompt, null, stream: false), cancellationToken).ConfigureAwait(false);
            RecordUsage(obj["usage"]);
            return ParseChatResponse(obj);
        }

        public Task<ChatResponse> ChatAsync(IReadOnlyList<ChatMessageData> messages, string systemPrompt, CancellationToken cancellationToken = default)
        {
            return ChatAsync(messages, systemPrompt, null, cancellationToken);
        }

        public async Task<ChatResponse> ChatAsync(IReadOnlyList<ChatMessageData> messages, string systemPrompt, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken = default)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            foreach (var m in messages)
                RaiseMessageSent(string.IsNullOrWhiteSpace(m?.Role) ? "user" : m.Role, m?.Content ?? "");

            RaisePromptAssembled(systemPrompt, messages);
            var obj = await SendAsync(() => BuildRequest(messages, systemPrompt, tools, stream: false), cancellationToken).ConfigureAwait(false);
            RecordUsage(obj["usage"]);
            return ParseChatResponse(obj);
        }

        public Task<ChatResponse> ChatStreamingAsync(
            IReadOnlyList<ChatMessageData> messages,
            string systemPrompt,
            IProgress<ChatStreamDelta> progress,
            CancellationToken cancellationToken = default)
        {
            return ChatStreamingAsync(messages, systemPrompt, null, progress, cancellationToken);
        }

        public async Task<ChatResponse> ChatStreamingAsync(
            IReadOnlyList<ChatMessageData> messages,
            string systemPrompt,
            IReadOnlyList<ToolDefinition> tools,
            IProgress<ChatStreamDelta> progress,
            CancellationToken cancellationToken = default)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            foreach (var m in messages)
                RaiseMessageSent(string.IsNullOrWhiteSpace(m?.Role) ? "user" : m.Role, m?.Content ?? "");

            RaisePromptAssembled(systemPrompt, messages);
            var reasoning = new StringBuilder();
            var content = new StringBuilder();
            var finishReason = new StringBuilder();
            var toolCalls = new List<NativeToolCall>();

            var startedAt = DateTime.UtcNow;
            var deadline = startedAt + _options.RetryTimeout;
            var attempt = 0;
            int? lastStatus = null;
            var lastReason = "";
            var lastBody = "";

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                HttpResponseMessage response = null;
                Exception sendError = null;
                try
                {
                    response = await _http.SendAsync(
                        BuildRequest(messages, systemPrompt, tools, stream: true),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    sendError = new TimeoutException(
                        "Timed out waiting for a response from the AI provider (" + _options.TimeoutSeconds + " s).");
                }
                catch (HttpRequestException ex)
                {
                    sendError = ex;
                }

                if (response != null)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            await ReadSseStreamAsync(response, reasoning, content, finishReason, toolCalls, progress, cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            response.Dispose();
                        }
                        return new ChatResponse { Answer = content.ToString(), Reasoning = reasoning.ToString(), FinishReason = finishReason.ToString(), ToolCalls = toolCalls };
                    }

                    var retryAfter = ReadRetryAfter(response);
                    var body = "";
                    try { body = await response.Content.ReadAsStringAsync().ConfigureAwait(false); } catch { body = ""; }
                    lastStatus = (int)response.StatusCode;
                    lastReason = response.ReasonPhrase ?? "";
                    lastBody = body;
                    response.Dispose();

                    attempt++;
                    if (_options.NoRetryOn4xx && IsNonRetryable4xx(lastStatus.Value))
                        break;

                    var wait = retryAfter ?? ComputeBackoff(attempt);
                    var elapsed = DateTime.UtcNow - startedAt;
                    var remaining = deadline - DateTime.UtcNow;
                    var willRetry = remaining > TimeSpan.Zero;
                    if (willRetry && wait > remaining)
                        wait = remaining;

                    RaiseRetryAttempt(attempt, lastStatus, lastReason, lastBody, wait, elapsed, remaining, willRetry);

                    if (!willRetry) break;
                    if (wait > TimeSpan.Zero)
                        await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    lastStatus = null;
                    lastReason = sendError?.GetType().Name ?? "network error";
                    lastBody = sendError?.Message ?? "";

                    attempt++;
                    var wait = ComputeBackoff(attempt);
                    var elapsed = DateTime.UtcNow - startedAt;
                    var remaining = deadline - DateTime.UtcNow;
                    var willRetry = remaining > TimeSpan.Zero;
                    if (willRetry && wait > remaining)
                        wait = remaining;

                    RaiseRetryAttempt(attempt, lastStatus, lastReason, lastBody, wait, elapsed, remaining, willRetry);

                    if (!willRetry) break;
                    if (wait > TimeSpan.Zero)
                        await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                }
            }

            throw BuildProviderErrorException(attempt, DateTime.UtcNow - startedAt, lastStatus, lastReason, lastBody);
        }


        private async Task<JObject> SendAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
        {
            var startedAt = DateTime.UtcNow;
            var deadline = startedAt + _options.RetryTimeout;
            var attempt = 0;
            int? lastStatus = null;
            var lastReason = "";
            var lastBody = "";

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                HttpResponseMessage response = null;
                Exception sendError = null;
                try
                {
                    response = await _http.SendAsync(requestFactory(), cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    sendError = new TimeoutException(
                        "Timed out waiting for a response from the AI provider (" + _options.TimeoutSeconds + " s).");
                }
                catch (HttpRequestException ex)
                {
                    sendError = ex;
                }

                if (response != null)
                {
                    var retryAfter = ReadRetryAfter(response);
                    var body = "";
                    try { body = await response.Content.ReadAsStringAsync().ConfigureAwait(false); } catch { body = ""; }

                    if (response.IsSuccessStatusCode)
                    {
                        var obj = JObject.Parse(body);
                        response.Dispose();
                        return obj;
                    }

                    lastStatus = (int)response.StatusCode;
                    lastReason = response.ReasonPhrase ?? "";
                    lastBody = body;
                    response.Dispose();

                    attempt++;
                    if (_options.NoRetryOn4xx && IsNonRetryable4xx(lastStatus.Value))
                        break;

                    var wait = retryAfter ?? ComputeBackoff(attempt);
                    var elapsed = DateTime.UtcNow - startedAt;
                    var remaining = deadline - DateTime.UtcNow;
                    var willRetry = remaining > TimeSpan.Zero;
                    if (willRetry && wait > remaining)
                        wait = remaining;

                    RaiseRetryAttempt(attempt, lastStatus, lastReason, lastBody, wait, elapsed, remaining, willRetry);

                    if (!willRetry) break;
                    if (wait > TimeSpan.Zero)
                        await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    lastStatus = null;
                    lastReason = sendError?.GetType().Name ?? "network error";
                    lastBody = sendError?.Message ?? "";

                    attempt++;
                    var wait = ComputeBackoff(attempt);
                    var elapsed = DateTime.UtcNow - startedAt;
                    var remaining = deadline - DateTime.UtcNow;
                    var willRetry = remaining > TimeSpan.Zero;
                    if (willRetry && wait > remaining)
                        wait = remaining;

                    RaiseRetryAttempt(attempt, lastStatus, lastReason, lastBody, wait, elapsed, remaining, willRetry);

                    if (!willRetry) break;
                    if (wait > TimeSpan.Zero)
                        await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                }
            }

            throw BuildProviderErrorException(attempt, DateTime.UtcNow - startedAt, lastStatus, lastReason, lastBody);
        }


        /// <summary>
        /// Reads an Anthropic SSE stream (<c>event:</c> + <c>data:</c> lines) and reports
        /// reasoning/content deltas through <paramref name="progress"/>.
        /// </summary>
        private async Task ReadSseStreamAsync(
            HttpResponseMessage response,
            StringBuilder reasoning,
            StringBuilder content,
            StringBuilder finishReason,
            List<NativeToolCall> toolCalls,
            IProgress<ChatStreamDelta> progress,
            CancellationToken cancellationToken)
        {
            var toolBlocks = new Dictionary<int, AnthropicToolBlock>();
            var promptTokens = 0;
            var cacheReadTokens = 0;
            var completionTokens = 0;

            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) break;

                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith(":", StringComparison.Ordinal)) continue;
                    if (!trimmed.StartsWith("data:", StringComparison.Ordinal)) continue;

                    var data = trimmed.Substring(5).TrimStart();
                    if (data == "[DONE]") break;

                    JObject evt;
                    try { evt = JObject.Parse(data); }
                    catch { continue; }

                    switch (evt["type"]?.Value<string>())
                    {
                        case "message_start":
                            var startUsage = evt["message"]?["usage"];
                            promptTokens = ReadTokenCount(startUsage, "input_tokens");
                            cacheReadTokens = ReadTokenCount(startUsage, "cache_read_input_tokens");
                            break;

                        case "content_block_start":
                            var startIndex = evt["index"]?.Value<int>() ?? 0;
                            var block = evt["content_block"];
                            if (block?["type"]?.Value<string>() == "tool_use")
                            {
                                toolBlocks[startIndex] = new AnthropicToolBlock
                                {
                                    Id = block["id"]?.Value<string>(),
                                    Name = block["name"]?.Value<string>(),
                                    Json = new StringBuilder()
                                };
                            }
                            break;

                        case "content_block_delta":
                            var deltaIndex = evt["index"]?.Value<int>() ?? 0;
                            var delta = evt["delta"];
                            switch (delta?["type"]?.Value<string>())
                            {
                                case "text_delta":
                                    var text = delta["text"]?.Value<string>();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        content.Append(text);
                                        progress?.Report(new ChatStreamDelta { ContentDelta = text });
                                    }
                                    break;
                                case "thinking_delta":
                                    var thinking = delta["thinking"]?.Value<string>();
                                    if (!string.IsNullOrEmpty(thinking))
                                    {
                                        reasoning.Append(thinking);
                                        progress?.Report(new ChatStreamDelta { ReasoningDelta = thinking });
                                    }
                                    break;
                                case "input_json_delta":
                                    var partial = delta["partial_json"]?.Value<string>();
                                    if (toolBlocks.TryGetValue(deltaIndex, out var tb) && !string.IsNullOrEmpty(partial))
                                        tb.Json.Append(partial);
                                    break;
                            }
                            break;

                        case "message_delta":
                            var fr = evt["delta"]?["stop_reason"]?.Value<string>();
                            if (!string.IsNullOrWhiteSpace(fr))
                            {
                                finishReason.Clear();
                                finishReason.Append(fr);
                            }
                            completionTokens = ReadTokenCount(evt["usage"], "output_tokens");
                            break;
                    }
                }
            }

            foreach (var kv in toolBlocks.OrderBy(kv => kv.Key))
            {
                toolCalls.Add(new NativeToolCall
                {
                    Id = kv.Value.Id,
                    Name = kv.Value.Name,
                    Arguments = ParseArguments(kv.Value.Json?.ToString())
                });
            }

            if (promptTokens > 0 || completionTokens > 0)
                RecordUsage(promptTokens, completionTokens, cacheReadTokens, Math.Max(0, promptTokens - cacheReadTokens));
        }


        private ChatResponse ParseChatResponse(JObject obj)
        {
            var reasoning = new StringBuilder();
            var content = new StringBuilder();
            var toolCalls = new List<NativeToolCall>();

            if (obj["content"] is JArray contentArr)
            {
                foreach (var block in contentArr)
                {
                    switch (block["type"]?.Value<string>())
                    {
                        case "text":
                            content.Append(block["text"]?.Value<string>());
                            break;
                        case "thinking":
                            reasoning.Append(block["thinking"]?.Value<string>());
                            break;
                        case "tool_use":
                            toolCalls.Add(new NativeToolCall
                            {
                                Id = block["id"]?.Value<string>(),
                                Name = block["name"]?.Value<string>(),
                                Arguments = block["input"] as JObject ?? new JObject()
                            });
                            break;
                    }
                }
            }

            return new ChatResponse
            {
                Answer = content.ToString(),
                Reasoning = reasoning.ToString(),
                FinishReason = obj["stop_reason"]?.Value<string>(),
                ToolCalls = toolCalls
            };
        }


        internal HttpRequestMessage BuildRequest(IReadOnlyList<ChatMessageData> messages, string systemPrompt, IReadOnlyList<ToolDefinition> tools, bool stream)
        {
            var url = (_options.BaseUrl ?? "").TrimEnd('/') + "/v1/messages";
            var request = new HttpRequestMessage(HttpMethod.Post, url);

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
            request.Headers.TryAddWithoutValidation("anthropic-version", AnthropicVersion);

            var payload = new JObject
            {
                ["model"] = _options.Model ?? "",
                ["max_tokens"] = _options.MaxTokens,
                ["stream"] = stream,
                ["temperature"] = _options.Temperature
            };

            if (_options.ThinkingBudgetTokens > 0)
            {
                payload["thinking"] = new JObject
                {
                    ["type"] = "enabled",
                    ["budget_tokens"] = _options.ThinkingBudgetTokens
                };
                if (_options.MaxTokens <= _options.ThinkingBudgetTokens)
                    payload["max_tokens"] = _options.ThinkingBudgetTokens + 1024;
            }

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                // Cache the system block so subsequent turns hit the prompt cache.
                payload["system"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = systemPrompt,
                        ["cache_control"] = new JObject { ["type"] = "ephemeral" }
                    }
                };
            }

            var msgArray = new JArray();
            foreach (var m in messages ?? new List<ChatMessageData>())
            {
                var role = string.IsNullOrWhiteSpace(m?.Role) ? "user" : m.Role;

                if (string.Equals(role, "system", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.Equals(role, "tool", StringComparison.OrdinalIgnoreCase))
                {
                    msgArray.Add(new JObject
                    {
                        ["role"] = "user",
                        ["content"] = new JArray
                        {
                            new JObject
                            {
                                ["type"] = "tool_result",
                                ["tool_use_id"] = m.ToolCallId ?? "",
                                ["content"] = m.Content ?? ""
                            }
                        }
                    });
                    continue;
                }

                if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) && m.ToolCalls != null && m.ToolCalls.Count > 0)
                {
                    var assistantBlocks = new JArray();
                    if (!string.IsNullOrEmpty(m.Content))
                        assistantBlocks.Add(new JObject { ["type"] = "text", ["text"] = m.Content });
                    foreach (var tc in m.ToolCalls)
                    {
                        assistantBlocks.Add(new JObject
                        {
                            ["type"] = "tool_use",
                            ["id"] = tc.Id ?? "",
                            ["name"] = tc.Name ?? "",
                            ["input"] = tc.Arguments ?? new JObject()
                        });
                    }
                    msgArray.Add(new JObject { ["role"] = "assistant", ["content"] = assistantBlocks });
                    continue;
                }

                var hasImages = m?.Images != null && m.Images.Count > 0;
                if (!hasImages)
                {
                    msgArray.Add(new JObject { ["role"] = role, ["content"] = m?.Content ?? "" });
                }
                else
                {
                    var blocks = new JArray();
                    if (!string.IsNullOrEmpty(m?.Content))
                        blocks.Add(new JObject { ["type"] = "text", ["text"] = m.Content });
                    foreach (var image in m.Images)
                    {
                        blocks.Add(new JObject
                        {
                            ["type"] = "image",
                            ["source"] = new JObject
                            {
                                ["type"] = "base64",
                                ["media_type"] = string.IsNullOrEmpty(image?.MediaType) ? "image/png" : image.MediaType,
                                ["data"] = image?.DataBase64 ?? ""
                            }
                        });
                    }
                    msgArray.Add(new JObject { ["role"] = role, ["content"] = blocks });
                }
            }
            payload["messages"] = msgArray;

            if (tools != null && tools.Count > 0)
            {
                var toolsArr = new JArray();
                foreach (var t in tools)
                {
                    toolsArr.Add(new JObject
                    {
                        ["name"] = t.Name ?? "",
                        ["description"] = t.Description ?? "",
                        ["input_schema"] = t.Parameters ?? new JObject()
                    });
                }
                payload["tools"] = toolsArr;
            }

            request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
            return request;
        }


        private void RecordUsage(JToken usage)
        {
            var prompt = ReadTokenCount(usage, "input_tokens");
            var completion = ReadTokenCount(usage, "output_tokens");
            var cacheRead = ReadTokenCount(usage, "cache_read_input_tokens");
            if (prompt == 0 && completion == 0) return;
            RecordUsage(prompt, completion, cacheRead, Math.Max(0, prompt - cacheRead));
        }

        private void RecordUsage(int promptTokens, int completionTokens, int cacheHitTokens, int cacheMissTokens)
        {
            var total = promptTokens + completionTokens;
            if (total == 0) return;

            var cost = CalculateCost(cacheHitTokens, cacheMissTokens, completionTokens);
            var requestUsage = new TokenUsage
            {
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = total,
                CacheHitTokens = cacheHitTokens,
                CacheMissTokens = cacheMissTokens,
                EstimatedCostUsd = cost
            };

            lock (_usageLock)
            {
                _totalUsage.PromptTokens += promptTokens;
                _totalUsage.CompletionTokens += completionTokens;
                _totalUsage.TotalTokens += total;
                _totalUsage.CacheHitTokens += cacheHitTokens;
                _totalUsage.CacheMissTokens += cacheMissTokens;
                _totalUsage.EstimatedCostUsd += cost;
            }

            TokenUsageUpdated?.Invoke(this, requestUsage);
        }

        private double CalculateCost(int cacheHitTokens, int cacheMissTokens, int completionTokens)
        {
            var inputCost = cacheMissTokens / 1000000.0 * _options.InputPricePerMillion
                          + cacheHitTokens / 1000000.0 * _options.CacheHitPricePerMillion;
            var outputCost = completionTokens / 1000000.0 * _options.OutputPricePerMillion;
            return inputCost + outputCost;
        }

        private static int ReadTokenCount(JToken usage, string name)
        {
            if (!(usage is JObject obj)) return 0;
            var token = obj[name];
            if (token == null) return 0;
            if (token.Type == JTokenType.Integer) return token.Value<int>();
            if (token.Type == JTokenType.String && int.TryParse(token.Value<string>(), out var parsed)) return parsed;
            return 0;
        }

        private static JObject ParseArguments(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new JObject();
            try { return JObject.Parse(json) ?? new JObject(); }
            catch { return new JObject(); }
        }


        private static bool IsNonRetryable4xx(int statusCode) =>
            statusCode == 400 || statusCode == 401 || statusCode == 403 ||
            statusCode == 404 || statusCode == 422;

        private TimeSpan ComputeBackoff(int attempt)
        {
            var baseSeconds = Math.Max(0, _options.RetryBaseDelay.TotalSeconds);
            var maxSeconds = Math.Max(baseSeconds, _options.RetryMaxDelay.TotalSeconds);
            var exponent = Math.Min(Math.Max(attempt - 1, 0), 1023);
            var seconds = Math.Min(maxSeconds, baseSeconds * Math.Pow(2.0, exponent));
            var jitter = seconds * 0.2 * _random.NextDouble();
            return TimeSpan.FromSeconds(seconds + jitter);
        }

        private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
        {
            if (response?.Headers == null) return null;
            if (!response.Headers.TryGetValues("Retry-After", out var values)) return null;
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (int.TryParse(value.Trim(), out var seconds))
                    return TimeSpan.FromSeconds(Math.Max(0, seconds));
                if (DateTimeOffset.TryParse(value.Trim(), out var date))
                {
                    var delta = date - DateTimeOffset.UtcNow;
                    if (delta > TimeSpan.Zero) return delta;
                }
            }
            return null;
        }

        private void RaiseRetryAttempt(int attempt, int? statusCode, string reason, string body, TimeSpan wait, TimeSpan elapsed, TimeSpan remaining, bool willRetry)
        {
            var handler = RetryAttempt;
            if (handler == null) return;
            handler(this, new RetryAttemptEventArgs
            {
                Attempt = attempt,
                StatusCode = statusCode,
                ReasonPhrase = reason ?? "",
                Body = TruncateForLog(body ?? "", 500),
                WaitBeforeRetry = wait,
                Elapsed = elapsed,
                Remaining = remaining,
                WillRetry = willRetry
            });
        }

        private static ProviderErrorException BuildProviderErrorException(int attempt, TimeSpan elapsed, int? statusCode, string reason, string body)
        {
            var kind = ClassifyProviderError(statusCode, body);
            var status = statusCode.HasValue
                ? statusCode.Value + " (" + reason + ")"
                : "no response (" + reason + ")";
            var message = "AI provider unavailable after " + attempt + " attempt(s) over " + FormatDuration(elapsed) +
                          ". Last status: " + status + ". Response: " + TruncateForLog(body);
            return new ProviderErrorException(kind, message, statusCode, body);
        }

        private static ProviderErrorKind ClassifyProviderError(int? statusCode, string body)
        {
            if (statusCode == 401 || statusCode == 403) return ProviderErrorKind.Auth;
            if (statusCode == 429) return ProviderErrorKind.RateLimit;
            if (statusCode >= 500) return ProviderErrorKind.Transient;
            if (ContainsContextOverflowKeywords(body)) return ProviderErrorKind.ContextWindowExceeded;
            if (statusCode == 400 && ContainsContentFilterKeywords(body)) return ProviderErrorKind.ContentFilter;
            return ProviderErrorKind.Other;
        }

        private static bool ContainsContextOverflowKeywords(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;
            var b = body.ToLowerInvariant();
            return b.Contains("context length") ||
                   b.Contains("context window") ||
                   b.Contains("too many tokens") ||
                   b.Contains("maximum context") ||
                   b.Contains("max tokens") ||
                   b.Contains("token limit") ||
                   b.Contains("context_length_exceeded") ||
                   b.Contains("prompt is too long") ||
                   b.Contains("input is too long");
        }

        private static bool ContainsContentFilterKeywords(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;
            var b = body.ToLowerInvariant();
            return b.Contains("content filter") ||
                   b.Contains("content_filter") ||
                   b.Contains("safety");
        }

        private static string FormatDuration(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            if (t.TotalMinutes >= 1) return (int)t.TotalMinutes + "m " + t.Seconds + "s";
            if (t.TotalSeconds >= 1) return (int)t.TotalSeconds + "s";
            return (int)t.TotalMilliseconds + "ms";
        }

        private static string TruncateForLog(string text, int max = 2000)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text;
            return text.Substring(0, max) + "\u2026(truncated)";
        }

        private sealed class AnthropicToolBlock
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public StringBuilder Json { get; set; }
        }
    }
}

