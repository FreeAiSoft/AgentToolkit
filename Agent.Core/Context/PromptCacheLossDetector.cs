using System;
using Agent.Core.Models;

namespace Agent.Core.Context
{
    /// <summary>Kind of a detected prompt-cache loss.</summary>
    public enum PromptCacheLossKind
    {
        /// <summary>No loss detected.</summary>
        None,
        /// <summary>The whole cache-hit input was lost (hit dropped to zero).</summary>
        Full,
        /// <summary>Only part of the cache-hit input was lost.</summary>
        Partial
    }

    /// <summary>Payload describing a detected prompt-cache loss.</summary>
    public sealed class PromptCacheLossInfo
    {
        public PromptCacheLossKind Kind { get; set; }
        public int PreviousCacheHitTokens { get; set; }
        public int CurrentCacheHitTokens { get; set; }
        public int CurrentPromptTokens { get; set; }
        public int CurrentCacheMissTokens { get; set; }
        public int LostTokens { get; set; }
        public int LostPercent { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Pure detection logic for prompt-cache losses: compares the cache-hit input of two
    /// consecutive requests and reports a loss when the drop exceeds the tolerable ratio.
    /// Stateless and I/O-free by contract.
    /// </summary>
    public static class PromptCacheLossDetector
    {
        /// <summary>
        /// Detects a prompt-cache loss for <paramref name="usage"/> relative to the previous
        /// request. Returns <c>null</c> when no loss is detected.
        /// </summary>
        /// <param name="usage">The current request's token usage (never null).</param>
        /// <param name="previousCacheHitTokens">Cache-hit tokens of the previous request.</param>
        /// <param name="previousPromptTokens">Prompt tokens of the previous request (state parity; reserved).</param>
        /// <param name="dropRatioPercent">Tolerable cache-hit drop, in percent (0..100).</param>
        public static PromptCacheLossInfo Detect(
            TokenUsage usage,
            int previousCacheHitTokens,
            int previousPromptTokens,
            double dropRatioPercent)
        {
            if (usage == null)
                return null;

            var prev = Math.Max(0, previousCacheHitTokens);
            var cur = Math.Max(0, usage.CacheHitTokens);

            // A fresh conversation (no previous hit) or a provider that never reports cache
            // metrics has nothing to lose, so no false positives are raised.
            if (prev <= 0 || usage.PromptTokens <= 0)
                return null;

            // A warm cache that stayed or grew is not a loss.
            if (cur >= prev)
                return null;

            var lost = prev - cur;
            var dropPercent = 100.0 * lost / prev;
            if (dropPercent <= dropRatioPercent)
                return null;

            var kind = cur == 0 ? PromptCacheLossKind.Full : PromptCacheLossKind.Partial;
            var pct = (int)Math.Round(100.0 * lost / prev, MidpointRounding.AwayFromZero);

            string message;
            if (kind == PromptCacheLossKind.Full)
                message = $"Prompt cache lost: cache-hit input dropped from {prev:N0} to 0 tokens (lost {lost:N0} tokens, 100%).";
            else
                message = $"Prompt cache degraded: cache-hit input dropped from {prev:N0} to {cur:N0} tokens (lost {lost:N0} tokens, {pct:0}%).";

            return new PromptCacheLossInfo
            {
                Kind = kind,
                PreviousCacheHitTokens = prev,
                CurrentCacheHitTokens = cur,
                CurrentPromptTokens = usage.PromptTokens,
                CurrentCacheMissTokens = Math.Max(0, usage.CacheMissTokens),
                LostTokens = lost,
                LostPercent = pct,
                Message = message
            };
        }
    }
}
