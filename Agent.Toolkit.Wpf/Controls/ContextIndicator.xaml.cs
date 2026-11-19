using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Agent.Core.Context;
using Agent.Core.Models;
using Agent.Models;

namespace Agent.Toolkit.Wpf.Controls
{
    /// <summary>
    /// Context-window indicator: a fill bar plus text lines (window fill, cache split, session
    /// statistics). It reads everything from a bound <see cref="ChatModel"/> (the single source of
    /// truth) and re-renders whenever the model or its active conversation changes. Formatting and
    /// severity live in <see cref="ContextUsageCalculator"/>, so this control stays pure presentation.
    /// </summary>
    public partial class ContextIndicator : UserControl
    {
        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
            nameof(Model), typeof(ChatModel), typeof(ContextIndicator),
            new PropertyMetadata(null, OnModelChanged));

        public static readonly DependencyProperty ShowDetailsProperty = DependencyProperty.Register(
            nameof(ShowDetails), typeof(bool), typeof(ContextIndicator),
            new PropertyMetadata(true, OnStateChanged));

        private ChatConversation _subscribedConversation;

        public ContextIndicator()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
            Refresh();
        }

        /// <summary>Chat model the indicator reads (window size, thresholds, active conversation usage).</summary>
        public ChatModel Model
        {
            get => (ChatModel)GetValue(ModelProperty);
            set => SetValue(ModelProperty, value);
        }

        /// <summary>Whether the text lines below the fill bar are visible.</summary>
        public bool ShowDetails
        {
            get => (bool)GetValue(ShowDetailsProperty);
            set => SetValue(ShowDetailsProperty, value);
        }

        private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((ContextIndicator)d).OnModelChanged((ChatModel)e.OldValue, (ChatModel)e.NewValue);

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((ContextIndicator)d).Refresh();

        private void OnModelChanged(ChatModel oldModel, ChatModel newModel)
        {
            UnsubscribeModel(oldModel);
            SubscribeModel(newModel);
            Refresh();
        }

        private void SubscribeModel(ChatModel model)
        {
            if (model == null)
                return;

            model.PropertyChanged += OnModelPropertyChanged;
            SubscribeConversation(model.ActiveChatConversation);
        }

        private void UnsubscribeModel(ChatModel model)
        {
            if (model == null)
                return;

            model.PropertyChanged -= OnModelPropertyChanged;
            UnsubscribeConversation();
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatModel.ActiveChatConversation))
            {
                UnsubscribeConversation();
                SubscribeConversation(Model?.ActiveChatConversation);
                Refresh();
                return;
            }

            if (e.PropertyName == nameof(ChatModel.ContextWindowSize) ||
                e.PropertyName == nameof(ChatModel.CompactThresholdTokens) ||
                e.PropertyName == nameof(ChatModel.MaxInputTokens))
            {
                Refresh();
            }
        }

        private void SubscribeConversation(ChatConversation conversation)
        {
            _subscribedConversation = conversation;
            if (conversation != null)
                conversation.PropertyChanged += OnConversationPropertyChanged;
        }

        private void UnsubscribeConversation()
        {
            if (_subscribedConversation == null)
                return;

            _subscribedConversation.PropertyChanged -= OnConversationPropertyChanged;
            _subscribedConversation = null;
        }

        private void OnConversationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatConversation.UsedTokens) ||
                e.PropertyName == nameof(ChatConversation.LastCacheHitTokens) ||
                e.PropertyName == nameof(ChatConversation.LastCacheMissTokens) ||
                e.PropertyName == nameof(ChatConversation.Usage))
            {
                Refresh();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeModel(Model);
            Unloaded -= OnUnloaded;
        }

        private void Refresh()
        {
            var model = Model;
            var conversation = model?.ActiveChatConversation;

            var info = ContextUsageCalculator.Compute(
                conversation?.UsedTokens ?? 0,
                model?.ContextWindowSize ?? 0,
                model?.CompactThresholdTokens ?? 0,
                model?.MaxInputTokens ?? 0,
                conversation?.Usage,
                conversation?.LastCacheHitTokens ?? 0,
                conversation?.LastCacheMissTokens ?? 0);

            var usedPercent = info.IsAvailable ? info.RatioPercent : 0.0;
            var cachePercent = info.IsAvailable ? info.CacheHitRatioPercent : 0.0;

            CacheHitColumn.Width = new GridLength(cachePercent, GridUnitType.Star);
            MissColumn.Width = new GridLength(Math.Max(0.0, usedPercent - cachePercent), GridUnitType.Star);
            RemainderColumn.Width = new GridLength(Math.Max(0.0, 100.0 - usedPercent), GridUnitType.Star);
            CacheHitSegment.SetResourceReference(Border.BackgroundProperty, "Theme.ContextCacheHit");
            MissSegment.SetResourceReference(Border.BackgroundProperty, BrushKeyFor(info.Status));

            FillText.Text = info.FillText;
            FillText.Visibility = ShowDetails ? Visibility.Visible : Visibility.Collapsed;

            var cache = info.CacheText;
            CacheText.Text = cache;
            CacheText.Visibility = ShowDetails && !string.IsNullOrEmpty(cache)
                ? Visibility.Visible
                : Visibility.Collapsed;

            var stats = info.StatsText;
            StatsText.Text = stats;
            StatsText.Visibility = ShowDetails && !string.IsNullOrEmpty(stats)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private static string BrushKeyFor(ContextUsageStatus status)
        {
            switch (status)
            {
                case ContextUsageStatus.Overflow:
                    return "Theme.ContextOverflow";
                case ContextUsageStatus.Warning:
                    return "Theme.ContextWarning";
                case ContextUsageStatus.Normal:
                    return "Theme.ContextNormal";
                default:
                    return "Theme.ContextMiss";
            }
        }
    }
}

