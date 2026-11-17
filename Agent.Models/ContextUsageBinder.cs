using System;
using System.Threading;
using Agent.Core.Context;
using Agent.Core.Contracts;
using Agent.Core.Models;

namespace Agent.Models
{
    /// <summary>
    /// Host helper that wires an <see cref="ITokenUsageProvider"/> into a <see cref="ChatModel"/>:
    /// it mirrors the provider's context window (and the budget thresholds) into the model and
    /// forwards each per-request <see cref="TokenUsage"/> to <see cref="ChatModel.RecordTokenUsage"/>.
    /// UI-free by design: the optional <see cref="SynchronizationContext"/> is used to marshal
    /// provider events back to the UI thread, exactly like the hosts' <c>Dispatcher.BeginInvoke</c>
    /// used to do. Without a context the callback runs on the calling thread (headless tests).
    /// </summary>
    public sealed class ContextUsageBinder
    {
        private readonly ChatModel _model;
        private readonly SynchronizationContext _uiContext;
        private ITokenUsageProvider _provider;

        public ContextUsageBinder(ChatModel model, SynchronizationContext uiContext = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _uiContext = uiContext;
        }

        /// <summary>
        /// Attaches to a provider (replacing any previous one), pushes the context-window numbers
        /// into the model and starts forwarding usage events.
        /// </summary>
        public void Attach(ITokenUsageProvider provider, ContextBudget budget)
        {
            Detach();

            _provider = provider;

            if (provider != null && budget == null)
                throw new ArgumentNullException(nameof(budget));

            _model.SetContextWindow(
                provider?.ContextWindowSize ?? 0,
                provider != null ? budget.CompactThresholdTokens : 0,
                provider != null ? budget.MaxInputTokens : 0);

            if (provider != null)
                provider.TokenUsageUpdated += OnTokenUsageUpdated;
        }

        /// <summary>Detaches from the current provider; safe to call multiple times.</summary>
        public void Detach()
        {
            if (_provider == null)
                return;

            _provider.TokenUsageUpdated -= OnTokenUsageUpdated;
            _provider = null;
        }

        private void OnTokenUsageUpdated(object sender, TokenUsage usage)
        {
            if (_uiContext != null)
            {
                _uiContext.Post(_ => Record(usage), null);
            }
            else
            {
                Record(usage);
            }
        }

        private void Record(TokenUsage usage)
        {
            _model.RecordTokenUsage(usage);
        }
    }
}
