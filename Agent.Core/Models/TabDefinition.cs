using System;
using System.Collections;
using System.Collections.Generic;

namespace Agent.Core.Models
{
    /// <summary>
    /// UI-free descriptor of a tab in the main tab control. It carries only data: a stable
    /// <see cref="Key"/>, display text and string names of <c>MaterialIconKind</c> icons; the
    /// view (<c>AgentTabControl</c>) is responsible for turning this into a rendered header.
    /// </summary>
    public sealed class TabDefinition
    {
        /// <summary>
        /// Creates an immutable tab descriptor. <paramref name="key"/> and
        /// <paramref name="header"/> must be non-null and non-empty.
        /// </summary>
        /// <param name="key">Stable tab key (e.g. <c>"Chat"</c>).</param>
        /// <param name="header">Display text of the tab.</param>
        /// <param name="toolTip">Optional hover tooltip.</param>
        /// <param name="outlineIconKey">Optional <c>MaterialIconKind</c> name for the outline icon.</param>
        /// <param name="filledIconKey">Optional <c>MaterialIconKind</c> name for the filled icon.</param>
        public TabDefinition(
            string key,
            string header,
            string toolTip = null,
            string outlineIconKey = null,
            string filledIconKey = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key must not be null or empty.", nameof(key));
            if (string.IsNullOrEmpty(header))
                throw new ArgumentException("Header must not be null or empty.", nameof(header));

            Key = key;
            Header = header;
            ToolTip = toolTip ?? "";
            OutlineIconKey = outlineIconKey ?? "";
            FilledIconKey = filledIconKey ?? "";
        }

        /// <summary>Stable tab key, unique within a tab collection.</summary>
        public string Key { get; }

        /// <summary>Display text of the tab.</summary>
        public string Header { get; }

        /// <summary>Hover tooltip.</summary>
        public string ToolTip { get; }

        /// <summary>String name of the <c>MaterialIconKind</c> used for the outline (unselected) icon.</summary>
        public string OutlineIconKey { get; }

        /// <summary>String name of the <c>MaterialIconKind</c> used for the filled (selected) icon.</summary>
        public string FilledIconKey { get; }
    }

    /// <summary>
    /// The tabs owned by the core toolkit: <see cref="Chat"/> and <see cref="Settings"/>.
    /// Domain-specific tabs are registered by the host at runtime via
    /// <c>AgentTabControl.AddHostTab(...)</c>.
    /// </summary>
    public static class CoreTabs
    {
        /// <summary>The built-in chat tab.</summary>
        public static TabDefinition Chat { get; } =
            new TabDefinition("Chat", "Chat", "Chat", "MessageOutline", "Message");

        /// <summary>The built-in settings tab.</summary>
        public static TabDefinition Settings { get; } =
            new TabDefinition("Settings", "Settings", "Settings", "CogOutline", "Cog");

        /// <summary>All core-owned tabs in display order: Chat, Settings.</summary>
        public static IReadOnlyList<TabDefinition> All { get; } = new[] { Chat, Settings };
    }

    /// <summary>
    /// An ordered, read-only registry of tab descriptors keyed by <see cref="TabDefinition.Key"/>.
    /// Duplicate keys are rejected.
    /// </summary>
    public sealed class TabDefinitionCollection : IReadOnlyList<TabDefinition>
    {
        private readonly List<TabDefinition> _items = new List<TabDefinition>();

        /// <inheritdoc />
        public int Count => _items.Count;

        /// <inheritdoc />
        public TabDefinition this[int index] => _items[index];

        /// <summary>
        /// Appends a tab descriptor. Throws <see cref="ArgumentException"/> when
        /// <paramref name="definition"/> is <c>null</c> or its key is already registered.
        /// </summary>
        public void Add(TabDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (ContainsKey(definition.Key))
                throw new ArgumentException($"A tab with key '{definition.Key}' is already registered.", nameof(definition));

            _items.Add(definition);
        }

        /// <summary>Returns the tab with the given key, or <c>false</c> when absent.</summary>
        public bool TryGet(string key, out TabDefinition definition)
        {
            var index = IndexOf(key);
            if (index < 0)
            {
                definition = null;
                return false;
            }

            definition = _items[index];
            return true;
        }

        /// <summary>True when a tab with the given key is registered.</summary>
        public bool ContainsKey(string key) => IndexOf(key) >= 0;

        /// <summary>Index of the tab with the given key, or <c>-1</c> when absent.</summary>
        public int IndexOf(string key)
        {
            if (key == null)
                return -1;

            for (var i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Key, key, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        /// <inheritdoc />
        public IEnumerator<TabDefinition> GetEnumerator() => _items.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
