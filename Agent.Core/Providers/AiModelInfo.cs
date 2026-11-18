namespace Agent.Core.Providers
{
    /// <summary>Metadata for a single model offered by a provider (static catalog or runtime discovery).</summary>
    public class AiModelInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int ContextWindowSize { get; set; }
        public int MaxOutputTokens { get; set; }
        public double InputPricePerMillion { get; set; }
        public double OutputPricePerMillion { get; set; }
        public AiProviderCapabilities Capabilities { get; set; } = AiProviderCapabilities.None;
    }
}
