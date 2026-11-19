using System;

namespace Agent.Core.Providers
{
    /// <summary>Provider-specific settings fields the API settings tab should reveal.</summary>
    [Flags]
    public enum AiProviderSettingFlags
    {
        None = 0,

        /// <summary>API key field (hidden for keyless local providers).</summary>
        ApiKey = 1,

        /// <summary>Azure OpenAI <c>api-version</c> query parameter.</summary>
        AzureApiVersion = 2,

        /// <summary>Azure OpenAI deployment name (URL segment <c>deployments/{name}</c>).</summary>
        AzureDeploymentName = 4
    }
}
