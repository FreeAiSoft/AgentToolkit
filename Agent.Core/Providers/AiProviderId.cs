using System;

namespace Agent.Core.Providers
{
    /// <summary>
    /// Canonical provider identifiers (Cline-style) plus alias normalization.
    /// </summary>
    public static class AiProviderId
    {
        public const string Anthropic = "anthropic";
        public const string OpenAi = "openai";
        public const string AzureOpenAi = "azure-openai";
        public const string Ollama = "ollama";
        public const string LmStudio = "lmstudio";
        public const string OpenRouter = "openrouter";
        public const string DeepSeek = "deepseek";
        public const string Groq = "groq";
        public const string XAi = "xai";

        /// <summary>Generic user-supplied OpenAI-compatible endpoint (fallback).</summary>
        public const string OpenAiCompatible = "openai-compatible";

        /// <summary>Provider id used when none is configured (backward compatible with pre-provider settings).</summary>
        public const string Default = OpenAi;

        /// <summary>
        /// Maps user-entered aliases to canonical ids and falls back to <see cref="Default"/>
        /// when the value is empty.
        /// </summary>
        public static string Normalize(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                return Default;

            var id = providerId.Trim().ToLowerInvariant();
            switch (id)
            {
                case "openai":
                case "openai-native":
                    return OpenAi;
                case "azure":
                case "azure-openai":
                case "azure_openai":
                    return AzureOpenAi;
                case "claude":
                case "anthropic":
                    return Anthropic;
                case "ollama":
                    return Ollama;
                case "lmstudio":
                case "lm-studio":
                case "lm_studio":
                    return LmStudio;
                case "openrouter":
                    return OpenRouter;
                case "deepseek":
                    return DeepSeek;
                case "groq":
                    return Groq;
                case "xai":
                case "x.ai":
                    return XAi;
                default:
                    return id;
            }
        }
    }
}
