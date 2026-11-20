using NUnit.Framework;
using Agent.Core.Providers;

namespace Agent.Tests
{
    [TestFixture]
    public class AiModelInfoTests
    {
        [Test]
        public void Defaults()
        {
            var model = new AiModelInfo();

            Assert.That(model.Id, Is.EqualTo(""));
            Assert.That(model.Name, Is.EqualTo(""));
            Assert.That(model.ContextWindowSize, Is.EqualTo(0));
            Assert.That(model.MaxOutputTokens, Is.EqualTo(0));
            Assert.That(model.InputPricePerMillion, Is.EqualTo(0));
            Assert.That(model.OutputPricePerMillion, Is.EqualTo(0));
            Assert.That(model.Capabilities, Is.EqualTo(AiProviderCapabilities.None));
        }
    }

    [TestFixture]
    public class AiProviderSpecTests
    {
        [Test]
        public void Defaults()
        {
            var spec = new AiProviderSpec();

            Assert.That(spec.Id, Is.EqualTo(""));
            Assert.That(spec.DisplayName, Is.EqualTo(""));
            Assert.That(spec.Protocol, Is.EqualTo(AiProviderProtocol.OpenAiCompatible));
            Assert.That(spec.DefaultBaseUrl, Is.EqualTo(""));
            Assert.That(spec.RequiresApiKey, Is.True);
            Assert.That(spec.DefaultModel, Is.EqualTo(""));
            Assert.That(spec.DefaultAzureApiVersion, Is.EqualTo(""));
            Assert.That(spec.Settings, Is.EqualTo(AiProviderSettingFlags.ApiKey));
            Assert.That(spec.Capabilities, Is.EqualTo(AiProviderCapabilities.None));
            Assert.That(spec.ContextWindowSize, Is.EqualTo(409600));
            Assert.That(spec.DocsUrl, Is.EqualTo(""));
        }

        [Test]
        public void HasSetting_DetectsFlags()
        {
            var spec = new AiProviderSpec
            {
                Settings = AiProviderSettingFlags.ApiKey | AiProviderSettingFlags.AzureApiVersion
            };

            Assert.That(spec.HasSetting(AiProviderSettingFlags.ApiKey), Is.True);
            Assert.That(spec.HasSetting(AiProviderSettingFlags.AzureApiVersion), Is.True);
            Assert.That(spec.HasSetting(AiProviderSettingFlags.AzureDeploymentName), Is.False);
        }
    }

    [TestFixture]
    public class AiProviderEnumTests
    {
        [Test]
        public void Capabilities_AreFlags()
        {
            Assert.That(AiProviderCapabilities.None, Is.EqualTo((AiProviderCapabilities)0));
            Assert.That(
                (AiProviderCapabilities.Reasoning | AiProviderCapabilities.Vision).HasFlag(AiProviderCapabilities.Reasoning),
                Is.True);
        }

        [Test]
        public void Protocol_Values()
        {
            Assert.That((int)AiProviderProtocol.OpenAiCompatible, Is.EqualTo(0));
            Assert.That((int)AiProviderProtocol.Anthropic, Is.EqualTo(1));
        }

        [Test]
        public void SettingFlags_Values()
        {
            Assert.That((int)AiProviderSettingFlags.None, Is.EqualTo(0));
            Assert.That((int)AiProviderSettingFlags.ApiKey, Is.EqualTo(1));
            Assert.That((int)AiProviderSettingFlags.AzureApiVersion, Is.EqualTo(2));
            Assert.That((int)AiProviderSettingFlags.AzureDeploymentName, Is.EqualTo(4));
        }
    }
}
