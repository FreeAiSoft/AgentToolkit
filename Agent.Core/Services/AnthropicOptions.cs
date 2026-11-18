namespace Agent.Core.Services
{
    /// <summary>Options for the Anthropic (Claude) Messages API handler.</summary>
    public class AnthropicOptions : AiProviderOptions
    {
        public AnthropicOptions()
        {
            BaseUrl = "https://api.anthropic.com";
            Model = "claude-sonnet-4-5";
        }

        /// <summary>Maximum tokens to generate. Anthropic requires this on every request.</summary>
        public int MaxTokens { get; set; } = 8192;

        /// <summary>Extended-thinking budget in tokens; 0 disables thinking (no <c>thinking</c> block is sent).</summary>
        public int ThinkingBudgetTokens { get; set; } = 0;
    }
}
