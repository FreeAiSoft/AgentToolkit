using System;

namespace Agent.Core.Services
{
    /// <summary>
    /// Shared options for every AI provider handler. Protocol-specific handlers
    /// (<see cref="OpenAiOptions"/>, <c>AnthropicOptions</c>) add their own fields.
    /// </summary>
    public class AiProviderOptions
    {
        /// <summary>Provider endpoint base URL (the protocol-specific default is set by each subclass).</summary>
        public string BaseUrl { get; set; }

        /// <summary>API key for providers that require one; empty for local endpoints (Ollama / LM Studio).</summary>
        public string ApiKey { get; set; }

        public string Model { get; set; }

        /// <summary>Sampling temperature sent to the model (0..1).</summary>
        public double Temperature { get; set; } = 0.2;

        /// <summary>Reasoning/thinking effort (Cline-style): "" = provider default, otherwise none/minimal/low/medium/high/xhigh/max.</summary>
        public string ReasoningEffort { get; set; } = "";

        /// <summary>Base directory of the prompt template files (System/, Tasks/) shipped with the extension.</summary>
        public string PromptsPath { get; set; } = PromptPaths.ResolvePromptsDirectory();

        /// <summary>Model context window size in tokens; used to display token consumption in the UI.</summary>
        public int ContextWindowSize { get; set; } = 409600;

        /// <summary>HTTP request timeout in seconds (per provider request).</summary>
        public int TimeoutSeconds { get; set; } = 300;

        /// <summary>Total budget for retrying a failing request before giving up.</summary>
        public TimeSpan RetryTimeout { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>Initial delay between retries; doubles on each attempt up to RetryMaxDelay.</summary>
        public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>Maximum delay between retries.</summary>
        public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Do not retry 4xx client errors (400/401/403/404/422); fail fast and surface a readable error.</summary>
        public bool NoRetryOn4xx { get; set; } = true;

        /// <summary>Price per 1M uncached input (cache-miss) tokens, in USD.</summary>
        public double InputPricePerMillion { get; set; } = 0.27;

        /// <summary>Price per 1M cache-hit input tokens, in USD.</summary>
        public double CacheHitPricePerMillion { get; set; } = 0.07;

        /// <summary>Price per 1M output tokens, in USD.</summary>
        public double OutputPricePerMillion { get; set; } = 1.10;
    }
}
