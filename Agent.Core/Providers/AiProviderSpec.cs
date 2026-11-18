namespace Agent.Core.Providers
{
    /// <summary>
    /// Static metadata for a built-in provider (id, protocol, default endpoint/model,
    /// capabilities). Mirrors Cline's <c>BUILTIN_SPECS</c>.
    /// </summary>
    public class AiProviderSpec
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";

        /// <summary>Wire protocol; selects the handler built by the factory.</summary>
        public AiProviderProtocol Protocol { get; set; } = AiProviderProtocol.OpenAiCompatible;

        /// <summary>Default base URL; empty for providers whose endpoint is always user-supplied.</summary>
        public string DefaultBaseUrl { get; set; } = "";

        /// <summary>True when the provider rejects requests without an API key (drives the stub fallback).</summary>
        public bool RequiresApiKey { get; set; } = true;

        public string DefaultModel { get; set; } = "";

        public AiProviderCapabilities Capabilities { get; set; } = AiProviderCapabilities.None;

        /// <summary>Typical context window (tokens) for the default model; used as a fallback.</summary>
        public int ContextWindowSize { get; set; } = 409600;

        public string DocsUrl { get; set; } = "";
    }
}
