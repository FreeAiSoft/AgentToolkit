using System;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Core.Providers;
using Agent.Core.Services;

namespace Agent.Core.Services
{
    /// <summary>Creates an AI provider from settings, routing by provider id (Cline-style registry).</summary>
    public static class AiProviderFactory
    {
        public static IAiProvider Create(SettingsData settings)
        {
            var providerId = AiProviderId.Normalize(settings?.ProviderId);
            var spec = AiProviderCatalog.Find(providerId) ?? AiProviderCatalog.Find(AiProviderId.OpenAi);
            var apiKey = (settings?.ApiKey ?? "").Trim();

            if (spec != null && spec.Protocol == AiProviderProtocol.Anthropic)
            {
                if (string.IsNullOrWhiteSpace(apiKey) && spec.RequiresApiKey)
                    return new StubAiProvider();
                return CreateAnthropic(settings, spec, apiKey);
            }

            // OpenAI-compatible family: OpenAI, Azure, Ollama, LM Studio, OpenRouter, DeepSeek, Groq, xAI, custom.
            if (string.IsNullOrWhiteSpace(apiKey) && spec != null && spec.RequiresApiKey)
                return new StubAiProvider();

            return CreateOpenAiCompatible(settings, spec);
        }

        private static IAiProvider CreateAnthropic(SettingsData settings, AiProviderSpec spec, string apiKey)
        {
            return new AnthropicProvider(new AnthropicOptions
            {
                BaseUrl = ResolveBaseUrl(settings, spec),
                ApiKey = apiKey,
                Model = ResolveModel(settings, spec),
                Temperature = Math.Max(0.0, Math.Min(1.0, settings.Temperature)),
                ReasoningEffort = ReasoningEffortMap.Normalize(settings.ReasoningEffort),
                ContextWindowSize = settings.ContextWindowSize > 0 ? settings.ContextWindowSize : (spec?.ContextWindowSize ?? 409600),
                TimeoutSeconds = settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 300,
                RetryTimeout = TimeSpan.FromMinutes(settings.RetryTimeoutMinutes > 0 ? settings.RetryTimeoutMinutes : 30),
                RetryBaseDelay = TimeSpan.FromSeconds(settings.RetryBaseDelaySeconds > 0 ? settings.RetryBaseDelaySeconds : 1),
                RetryMaxDelay = TimeSpan.FromSeconds(settings.RetryMaxDelaySeconds > 0 ? settings.RetryMaxDelaySeconds : 30),
                NoRetryOn4xx = settings.NoRetryOn4xx,
                InputPricePerMillion = settings.InputPricePerMillion >= 0 ? settings.InputPricePerMillion : 0.27,
                CacheHitPricePerMillion = settings.CacheHitPricePerMillion >= 0 ? settings.CacheHitPricePerMillion : 0.07,
                OutputPricePerMillion = settings.OutputPricePerMillion >= 0 ? settings.OutputPricePerMillion : 1.10
            });
        }

        private static IAiProvider CreateOpenAiCompatible(SettingsData settings, AiProviderSpec spec)
        {
            var toolCallingMode = string.Equals(settings?.ToolCallingMode, "Native", StringComparison.OrdinalIgnoreCase)
                ? ToolCallingMode.Native
                : ToolCallingMode.JsonChat;

            return new OpenAiCompatibleProvider(new OpenAiOptions
            {
                BaseUrl = ResolveBaseUrl(settings, spec),
                ApiKey = (settings?.ApiKey ?? "").Trim(),
                Model = ResolveModel(settings, spec),
                RulesPath = PromptPaths.ResolveRulesDirectory(),
                ToolCallingMode = toolCallingMode,
                ContextWindowSize = settings.ContextWindowSize > 0 ? settings.ContextWindowSize : (spec?.ContextWindowSize ?? 409600),
                TimeoutSeconds = settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 300,
                RetryTimeout = TimeSpan.FromMinutes(settings.RetryTimeoutMinutes > 0 ? settings.RetryTimeoutMinutes : 30),
                RetryBaseDelay = TimeSpan.FromSeconds(settings.RetryBaseDelaySeconds > 0 ? settings.RetryBaseDelaySeconds : 1),
                RetryMaxDelay = TimeSpan.FromSeconds(settings.RetryMaxDelaySeconds > 0 ? settings.RetryMaxDelaySeconds : 30),
                NoRetryOn4xx = settings.NoRetryOn4xx,
                InputPricePerMillion = settings.InputPricePerMillion >= 0 ? settings.InputPricePerMillion : 0.27,
                CacheHitPricePerMillion = settings.CacheHitPricePerMillion >= 0 ? settings.CacheHitPricePerMillion : 0.07,
                OutputPricePerMillion = settings.OutputPricePerMillion >= 0 ? settings.OutputPricePerMillion : 1.10,
                Temperature = Math.Max(0.0, Math.Min(1.0, settings.Temperature)),
                ReasoningEffort = ReasoningEffortMap.Normalize(settings.ReasoningEffort)
            });
        }

        private static string ResolveBaseUrl(SettingsData settings, AiProviderSpec spec)
        {
            if (!string.IsNullOrWhiteSpace(settings?.BaseUrl))
                return settings.BaseUrl.Trim();
            return !string.IsNullOrWhiteSpace(spec?.DefaultBaseUrl) ? spec.DefaultBaseUrl : "https://api.openai.com/v1";
        }

        private static string ResolveModel(SettingsData settings, AiProviderSpec spec)
        {
            if (!string.IsNullOrWhiteSpace(settings?.Model))
                return settings.Model.Trim();
            return !string.IsNullOrWhiteSpace(spec?.DefaultModel) ? spec.DefaultModel : "gpt-4o-mini";
        }
    }
}

