using System;
using System.Collections.Generic;

namespace Agent.Core.Providers
{
    /// <summary>Registry of built-in provider metadata; single source of truth for the factory and settings UI.</summary>
    public static class AiProviderCatalog
    {
        private static readonly AiProviderSpec[] _all =
        {
            new AiProviderSpec
            {
                Id = AiProviderId.Anthropic,
                DisplayName = "Anthropic (Claude)",
                Protocol = AiProviderProtocol.Anthropic,
                DefaultBaseUrl = "https://api.anthropic.com",
                RequiresApiKey = true,
                DefaultModel = "claude-sonnet-4-5",
                Capabilities = AiProviderCapabilities.Reasoning | AiProviderCapabilities.PromptCache | AiProviderCapabilities.Streaming | AiProviderCapabilities.Tools | AiProviderCapabilities.Vision,
                ContextWindowSize = 200000,
                DocsUrl = "https://docs.anthropic.com/en/api/getting-started"
            },
            new AiProviderSpec
            {
                Id = AiProviderId.OpenAi,
                DisplayName = "OpenAI",
                Protocol = AiProviderProtocol.OpenAiCompatible,
                DefaultBaseUrl = "https://api.openai.com/v1",
                RequiresApiKey = true,
                DefaultModel = "gpt-4o-mini",
                Capabilities = AiProviderCapabilities.Reasoning | AiProviderCapabilities.Streaming | AiProviderCapabilities.Tools | AiProviderCapabilities.Vision,
                ContextWindowSize = 128000,
                DocsUrl = "https://platform.openai.com/docs"
            },
            new AiProviderSpec
            {
                Id = AiProviderId.AzureOpenAi,
                DisplayName = "Azure OpenAI",
                Protocol = AiProviderProtocol.OpenAiCompatible,
                DefaultBaseUrl = "",
                RequiresApiKey = true,
                DefaultModel = "gpt-4o-mini",
                DefaultAzureApiVersion = "2024-06-01",
                Settings = AiProviderSettingFlags.ApiKey | AiProviderSettingFlags.AzureApiVersion | AiProviderSettingFlags.AzureDeploymentName,
                Capabilities = AiProviderCapabilities.Reasoning | AiProviderCapabilities.Streaming | AiProviderCapabilities.Tools | AiProviderCapabilities.Vision,
                ContextWindowSize = 128000,
                DocsUrl = "https://learn.microsoft.com/azure/ai-services/openai"
            },
            new AiProviderSpec
            {
                Id = AiProviderId.Ollama,
                DisplayName = "Ollama (local)",
                Protocol = AiProviderProtocol.OpenAiCompatible,
                DefaultBaseUrl = "http://localhost:11434/v1",
                RequiresApiKey = false,
                Settings = AiProviderSettingFlags.None,
                DefaultModel = "llama3.2",
                Capabilities = AiProviderCapabilities.Reasoning | AiProviderCapabilities.Streaming | AiProviderCapabilities.Tools | AiProviderCapabilities.Vision,
                ContextWindowSize = 8192,
                DocsUrl = "https://github.com/ollama/ollama"
            },
            new AiProviderSpec
            {
                Id = AiProviderId.LmStudio,
                DisplayName = "LM Studio (local)",
                Protocol = AiProviderProtocol.OpenAiCompatible,
                DefaultBaseUrl = "http://localhost:1234/v1",
                RequiresApiKey = false,
                Settings = AiProviderSettingFlags.None,
                DefaultModel = "local-model",
                Capabilities = AiProviderCapabilities.Streaming | AiProviderCapabilities.Tools | AiProviderCapabilities.Vision,
                ContextWindowSize = 32768,
                DocsUrl = "https://lmstudio.ai"
            },
            new AiProviderSpec
            {
                Id = AiProviderId.OpenRouter,
                DisplayName = "OpenRouter",
                Protocol = AiProviderProtocol.OpenAiCompatible,
                DefaultBaseUrl = "https://openrouter.ai/api/v1",
                RequiresApiKey = true,
                DefaultModel = "openai/gpt-4o-mini",
                Capabilities = AiProviderCapabilities.Reasoning | AiProviderCapabilities.Streaming | AiProviderCapabilities.Tools | AiProviderCapabilities.Vision,
                ContextWindowSize = 128000,
                DocsUrl = "https://openrouter.ai/docs"
            },
            new AiProviderSpec
            {
                Id = AiProviderId.DeepSeek,
                DisplayName = "DeepSeek",
                Protocol = AiProviderProtocol.OpenAiCompatible,
                DefaultBaseUrl = "https://api.deepseek.com/v1",
                RequiresApiKey = true,
                DefaultModel = "deepseek-chat",
                Capabilities = AiProviderCapabilities.Reasoning | AiProviderCapabilities.PromptCache | AiProviderCapabilities.Streaming | AiProviderCapabilities.Tools,
                ContextWindowSize = 65536,
                DocsUrl = "https://api-docs.deepseek.com"
            },
            new AiProviderSpec
            {
                Id = AiProviderId.Groq,
                DisplayName = "Groq",
                Protocol = AiProviderProtocol.OpenAiCompatible,
                DefaultBaseUrl = "https://api.groq.com/openai/v1",
                RequiresApiKey = true,
                DefaultModel = "llama-3.3-70b-versatile",
                Capabilities = AiProviderCapabilities.Streaming | AiProviderCapabilities.Tools | AiProviderCapabilities.Vision,
                ContextWindowSize = 131072,
                DocsUrl = "https://console.groq.com/docs"
            },
            new AiProviderSpec
            {
                Id = AiProviderId.XAi,
                DisplayName = "xAI (Grok)",
                Protocol = AiProviderProtocol.OpenAiCompatible,
                DefaultBaseUrl = "https://api.x.ai/v1",
                RequiresApiKey = true,
                DefaultModel = "grok-2-latest",
                Capabilities = AiProviderCapabilities.Streaming | AiProviderCapabilities.Tools | AiProviderCapabilities.Vision,
                ContextWindowSize = 131072,
                DocsUrl = "https://docs.x.ai"
            },
            new AiProviderSpec
            {
                Id = AiProviderId.OpenAiCompatible,
                DisplayName = "OpenAI-compatible (custom)",
                Protocol = AiProviderProtocol.OpenAiCompatible,
                DefaultBaseUrl = "",
                RequiresApiKey = true,
                DefaultModel = "gpt-4o-mini",
                Capabilities = AiProviderCapabilities.Reasoning | AiProviderCapabilities.Streaming | AiProviderCapabilities.Tools | AiProviderCapabilities.Vision,
                ContextWindowSize = 409600,
                DocsUrl = ""
            }
        };

        public static IReadOnlyList<AiProviderSpec> All => _all;

        /// <summary>Returns the spec for a normalized provider id, or null when unknown.</summary>
        public static AiProviderSpec Find(string providerId)
        {
            var id = AiProviderId.Normalize(providerId);
            foreach (var spec in _all)
            {
                if (string.Equals(spec.Id, id, StringComparison.OrdinalIgnoreCase))
                    return spec;
            }
            return null;
        }
    }
}
