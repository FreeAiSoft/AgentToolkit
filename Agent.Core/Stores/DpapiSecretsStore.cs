using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Agent.Core.Contracts;
using Agent.Core.Services;

namespace Agent.Core.Stores
{
    /// <summary>
    /// File-backed <see cref="ISecretsStore"/> that encrypts the API key with Windows DPAPI
    /// (<see cref="ProtectedData"/>, current-user scope). The key never lives in the settings JSON
    /// and is never duplicated per workspace; the concrete file path is supplied by the host.
    /// </summary>
    /// <remarks>
    /// DPAPI is Windows-only. This store is intended for Windows hosts (VSIX/WPF); tests and
    /// smoke hosts that must not touch the OS key store should use
    /// <see cref="InMemorySecretsStore"/> instead.
    /// </remarks>
    public class DpapiSecretsStore : ISecretsStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AgentToolkit.Secrets.v1");

        private readonly string _filePath;
        private readonly IAgentLogger _logger;

        public DpapiSecretsStore(string filePath, IAgentLogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A file path is required.", nameof(filePath));

            _filePath = filePath;
            _logger = logger ?? NoOpLogger.Instance;
        }

        /// <inheritdoc />
        public string LoadApiKey()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return "";

                var cipher = File.ReadAllBytes(_filePath);
                var plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "DpapiSecretsStore.LoadApiKey failed; returning empty.");
                return "";
            }
        }

        /// <inheritdoc />
        public void SaveApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                ClearApiKey();
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var cipher = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(apiKey),
                    Entropy,
                    DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_filePath, cipher);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "DpapiSecretsStore.SaveApiKey failed.");
            }
        }

        /// <inheritdoc />
        public void ClearApiKey()
        {
            try
            {
                if (File.Exists(_filePath))
                    File.Delete(_filePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "DpapiSecretsStore.ClearApiKey failed.");
            }
        }
    }
}
