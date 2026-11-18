using System;

namespace Agent.Core.Providers
{
    /// <summary>Capabilities a provider (or one of its models) may support.</summary>
    [Flags]
    public enum AiProviderCapabilities
    {
        None = 0,
        /// <summary>Extended thinking / reasoning output.</summary>
        Reasoning = 1,
        /// <summary>Prompt caching (Anthropic <c>cache_control</c>, OpenAI cached_tokens, DeepSeek cache).</summary>
        PromptCache = 2,
        /// <summary>Streaming responses (SSE).</summary>
        Streaming = 4,
        /// <summary>Native tool / function calling.</summary>
        Tools = 8,
        /// <summary>Image inputs.</summary>
        Vision = 16
    }
}
