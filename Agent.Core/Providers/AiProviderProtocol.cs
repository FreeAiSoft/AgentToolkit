namespace Agent.Core.Providers
{
    /// <summary>Wire protocol a provider speaks; selects which handler the factory builds.</summary>
    public enum AiProviderProtocol
    {
        /// <summary>OpenAI-style <c>/chat/completions</c> (OpenAI, Azure, Ollama, LM Studio, OpenRouter, DeepSeek, Groq, xAI, vLLM...).</summary>
        OpenAiCompatible,

        /// <summary>Anthropic Messages API (<c>/v1/messages</c>).</summary>
        Anthropic
    }
}
