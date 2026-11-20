using System;
using Agent.Core.Models;

namespace Agent.Core.Context
{
    /// <summary>Diagnostic reason a prompt-cache hit dropped between two requests.</summary>
    public enum CacheLossCauseKind
    {
        /// <summary>No cache loss was detected, so there is nothing to explain.</summary>
        None,
        /// <summary>The agent mode changed (e.g. Plan → Act), which changed the system prompt and invalidated the cached prefix.</summary>
        ModeSwitch,
        /// <summary>A cancelled turn left an unanswered user message, shifting the cached transcript prefix.</summary>
        OrphanedUser,
        /// <summary>The mode and transcript were unchanged, but the provider cache still did not hit (request may have been interrupted before the cache was primed).</summary>
        ProviderColdCache
    }

    /// <summary>Diagnostic payload describing why a prompt-cache hit dropped.</summary>
    public sealed class CacheLossCauseInfo
    {
        public CacheLossCauseKind Kind { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Pure diagnostic classifier for prompt-cache losses. Given the previous/current mode,
    /// whether the transcript ended with an unanswered user message, and the cache-hit tokens
    /// of two consecutive requests, it names the most likely cause. Stateless and I/O-free by
    /// contract, so it is unit-testable without a provider or a host.
    /// </summary>
    public static class CacheLossCauseClassifier
    {
        public static CacheLossCauseInfo Classify(
            AgentMode previousMode,
            AgentMode currentMode,
            bool hasOrphanedUser,
            int previousCacheHitTokens,
            int currentCacheHitTokens,
            int currentPromptTokens)
        {
            var prev = Math.Max(0, previousCacheHitTokens);
            var cur = Math.Max(0, currentCacheHitTokens);
            var prompt = Math.Max(0, currentPromptTokens);

            // A fresh conversation (no previous hit), a provider that never reports cache
            // metrics, or a warm cache that stayed/grew is not a loss.
            if (prev <= 0 || prompt <= 0 || cur >= prev)
            {
                return new CacheLossCauseInfo
                {
                    Kind = CacheLossCauseKind.None,
                    Message = "no cache loss detected"
                };
            }

            if (previousMode != currentMode)
            {
                return new CacheLossCauseInfo
                {
                    Kind = CacheLossCauseKind.ModeSwitch,
                    Message = $"mode switch {previousMode} -> {currentMode} changed the system prompt, invalidating the prompt cache"
                };
            }

            if (hasOrphanedUser)
            {
                return new CacheLossCauseInfo
                {
                    Kind = CacheLossCauseKind.OrphanedUser,
                    Message = "a cancelled turn left an unanswered user message in the transcript, shifting the cached prefix"
                };
            }

            return new CacheLossCauseInfo
            {
                Kind = CacheLossCauseKind.ProviderColdCache,
                Message = "provider cache did not hit despite an unchanged mode/transcript (request may have been interrupted before the cache was primed)"
            };
        }
    }
}
