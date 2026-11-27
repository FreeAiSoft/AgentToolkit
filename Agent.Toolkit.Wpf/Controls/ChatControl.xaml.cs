using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Agent.Core.Context;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Models;
using Agent.Toolkit.Wpf.Theming;
using Agent.Toolkit.Wpf.Diagnostics;
using Agent.Toolkit.Wpf.Markdown;
using Microsoft.Win32;

namespace Agent.Toolkit.Wpf.Controls
{
    /// <summary>
    /// Generic agent chat view: toolbar, history, message list, input box, Permissions/Docs
    /// overlays and Cancel/Resume status. RPA/VS/Git-specific behaviour is delegated to
    /// <see cref="IChatHost"/>; the host injects its toasts, commit editor and create-robot
    /// panel into the named <see cref="ContentControl"/> slots.
    /// </summary>
    public partial class ChatControl : UserControl
    {
        public const double PinnedExpandedHeight = 240.0;

        private const string MemoryUpdatePrompt =
            "Обнови memory bank: прочитай все файлы memory-bank/*.md (projectbrief.md, productContext.md, activeContext.md, systemPatterns.md, techContext.md, progress.md) и обнови activeContext.md и progress.md, чтобы отразить текущее состояние проекта.";

        private bool _isRecording;
        private bool _isTranscribing;
        private bool _pinnedExpanded;
        private bool _restoringUiState;
        private bool _suppressPermissionEvents;
        private readonly object _syncRoot = new object();
        private IMicrophoneRecorder _mic;
        private ISpeechToTextService _stt;
        private ToolCallProgressTracker _toolTracker;
        private ToolkitSettingsStore _defaultSettingsStore;
        private IChatHost _host;

        public ChatControl()
        {
            InitializeComponent();
            AgentThemeManager.EnsureInitialized();
            DataContextChanged += OnDataContextChanged;
            ChatMessages.CopyMessageRequested += OnCopyMessageRequested;
        }

        /// <summary>Host seam: agent loop, commit, build errors, robot creation, workspace scoping.</summary>
        public IChatHost Host
        {
            get => _host;
            set
            {
                _host = value;
                AttachPromptSink();
            }
        }

        /// <summary>Settings used by the permissions overlay and voice input.</summary>
        public SettingsData Settings { get; set; }

        /// <summary>
        /// Settings model shared with the settings tab. When set, the permissions overlay reads
        /// and writes this model instead of the legacy <see cref="Settings"/> snapshot, so chat
        /// permissions and the settings tab share a single source of truth.
        /// </summary>
        public SettingsModel SettingsModel { get; set; }

        /// <summary>
        /// Optional persistence seam for the settings the permissions overlay edits. When null, the
        /// permissions overlay falls back to the shared static <see cref="Agent.Core.Services.SettingsStore"/>.
        /// Hosts that store settings elsewhere (e.g. VibeCoder under its own %AppData% folder)
        /// inject their own <see cref="Agent.Core.Contracts.ISettingsStore"/> here.
        /// </summary>
        public ISettingsStore SettingsPersistence { get; set; }

        /// <summary>
        /// Host's app-data directory. When set (and <see cref="SettingsPersistence"/> is null), the
        /// permissions overlay persists to a global SQLite store under this directory instead
        /// of the legacy static <see cref="Agent.Core.Services.SettingsStore"/>.
        /// </summary>
        public string AppDataDirectory { get; set; }

        /// <summary>Optional logger for settings persistence diagnostics.</summary>
        public IAgentLogger Logger { get; set; }

        /// <summary>The chat model bound as this control's DataContext.</summary>
        public ChatModel Model => DataContext as ChatModel;

        /// <summary>The rendered message list (host wires agent-loop events and appends agent messages).</summary>
        public ChatMessagesControl MessagesList => ChatMessages;

        /// <summary>The pending-message queue (host feeds and steers it).</summary>
        public MessageQueueControl Queue => MessageQueue;

        /// <summary>The docs content slot (host renders RPA/VS documentation into it).</summary>
        public ContentControl DocsContentControl => DocsContent;

        /// <summary>The docs overlay title text block (host sets the rendered doc title).</summary>
        public TextBlock DocsTitleTextControl => DocsTitleText;

        // Host slot accessors (the host injects its toasts, commit editor and create-robot panel
        // into these ContentControl slots; they are exposed with clear "Host" names because the
        // x:Name-generated fields keep the "Slot" suffix).

        /// <summary>Slot for the host's voice-error toast.</summary>
        public ContentControl VoiceErrorToastHost => VoiceErrorToastSlot;

        /// <summary>Slot for the host's options-warning toast.</summary>
        public ContentControl OptionsWarningToastHost => OptionsWarningToastSlot;

        /// <summary>Slot for the host's commit-result bar.</summary>
        public ContentControl CommitResultBarHost => CommitResultBarSlot;

        /// <summary>Slot for the host's checkpoint-restore error toast.</summary>
        public ContentControl CheckpointRestoreErrorToastHost => CheckpointRestoreErrorToastSlot;
        public ContentControl PromptCacheLossToastHost => PromptCacheLossToastSlot;

        /// <summary>Slot for the host's commit editor control.</summary>
        public ContentControl CommitEditorHost => CommitEditorSlot;

        /// <summary>Slot for the host's create-robot panel.</summary>
        public ContentControl CreateRobotHost => CreateRobotPanelSlot;

        /// <summary>Suppresses mode-change persistence while the host restores the saved UI state.</summary>
        public bool RestoringUiState
        {
            get => _restoringUiState;
            set => _restoringUiState = value;
        }

        /// <summary>Raised when voice input fails; the host shows its error toast in the slot.</summary>
        public event EventHandler<string> VoiceError;

        /// <summary>Re-raises <see cref="ChatModel.PromptCacheLost"/> so the host can show a toast.</summary>
        public event EventHandler<PromptCacheLossInfo> PromptCacheLost;

        /// <summary>Injects the host-provided voice services (recorder + speech-to-text).</summary>
        public void AttachServices(IMicrophoneRecorder mic, ISpeechToTextService stt)
        {
            if (_mic != null) _mic.LevelChanged -= OnMicLevelChanged;
            _mic = mic;
            _stt = stt;
            if (_mic != null) _mic.LevelChanged += OnMicLevelChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ChatModel oldModel)
            {
                _toolTracker?.Detach();
                _toolTracker = null;
                oldModel.PropertyChanged -= OnModelPropertyChanged;
                oldModel.PromptCacheLost -= OnPromptCacheLost;
            }

            var model = e.NewValue as ChatModel;
            if (model == null)
                return;

            model.PropertyChanged += OnModelPropertyChanged;
            model.PromptCacheLost += OnPromptCacheLost;

            // Tool-call cards are owned by the toolkit: the tracker listens to the tool
            // lifecycle bus and creates/updates the running-indicator cards in the model.
            _toolTracker?.Detach();
            _toolTracker = new ToolCallProgressTracker(
                () => model.Messages,
                action => Dispatcher.BeginInvoke(action));
            _toolTracker.Attach();

            // Share the model-owned collections with the list controls.
            ChatMessages.Messages = model.Messages;
            MessageQueue.Messages = model.Pending;
            ChatMessages.Filter = message => !ReferenceEquals(message, model.PinnedMessage);

            // Register cross-thread synchronization BEFORE the views are created, so the
            // CollectionView built by ItemsSource allows background mutations.
            BindingOperations.EnableCollectionSynchronization(model.Attachments, _syncRoot);
            BindingOperations.EnableCollectionSynchronization(model.History, _syncRoot);
            AttachmentsList.ItemsSource = model.Attachments;
            HistoryList.ItemsSource = model.History;

            RefreshPinnedMessage();
            AttachPromptSink();
        }

        private void AttachPromptSink()
        {
            var model = Model;
            if (model == null)
                return;

            model.SetChatPromptSink(Host == null ? null : new HostPromptSink(Host));
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatModel.PinnedMessage))
                RefreshPinnedMessage();
        }

        private void OnPromptCacheLost(object sender, PromptCacheLossInfo info)
        {
            PromptCacheLost?.Invoke(this, info);
        }

        private void RefreshPinnedMessage()
        {
            PinnedHeaderPanel.DataContext = Model?.PinnedMessage;
            PinnedHeaderPanel.Visibility =
                Model?.PinnedMessage != null && Model.IsConversationView
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            _pinnedExpanded = false;
            UpdatePinnedExpandState();
            ChatMessages.RefreshFilter();
        }

        // ------------------------------------------------------------------
        // Public API for the host (message appends + view switching)
        // ------------------------------------------------------------------

        public void AppendChat(string text)
            => AppendChatMessage(new ChatMessage { Kind = ChatMessageKind.SystemNotice, Text = text });

        public void AppendSystemMessage(string text) => AppendChat(text);

        public void AppendExtensionError(string text)
            => AppendChatMessage(new ChatMessage { Kind = ChatMessageKind.Error, Text = text });

        public void AppendCompaction(string text)
            => AppendChatMessage(new ChatMessage { Kind = ChatMessageKind.Compaction, Text = text });

        public void AppendToolCall(string toolName, string result, bool blocked)
            => AppendChatMessage(new ChatMessage
            {
                Kind = blocked ? ChatMessageKind.ToolBlocked : ChatMessageKind.ToolCall,
                Payload = new ToolCallPayload { ToolName = toolName ?? "", Result = result ?? "", Blocked = blocked }
            });

        public void AppendChatMessage(ChatMessage message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => AppendChatMessage(message)));
                return;
            }
            ChatMessages.Append(message);
        }

        public void ShowConversationView()
        {
            Model?.ShowConversationView();
            ApplyChatViewMode();
        }

        public void ShowHistoryView()
        {
            Model?.ShowHistoryView();
            ApplyChatViewMode();
        }

        public void ApplyChatViewMode()
        {
            var conversation = Model == null || Model.IsConversationView;
            ChatToolbarPanel.Visibility = conversation ? Visibility.Visible : Visibility.Collapsed;
            MessagesHostPanel.Visibility = conversation ? Visibility.Visible : Visibility.Collapsed;
            ChatSplitter.Visibility = conversation ? Visibility.Visible : Visibility.Collapsed;
            ChatInputPanel.Visibility = conversation ? Visibility.Visible : Visibility.Collapsed;
            PinnedHeaderPanel.Visibility = conversation && Model?.PinnedMessage != null ? Visibility.Visible : Visibility.Collapsed;
            HistoryViewPanel.Visibility = conversation ? Visibility.Collapsed : Visibility.Visible;
        }

        public void EnsureChatReadyToReceive()
        {
            Model?.EnsureActiveConversation();
            ShowConversationView();
        }

        public void RefreshHistory() => Model?.RefreshHistory();

        public void SetInputText(string text)
        {
            InputBox.Text = text;
            InputBox.Focus();
        }

        /// <summary>Appends text to the input box (used by "insert selection/identifier to input").</summary>
        public void AppendInputText(string text)
        {
            var existing = InputBox.Text;
            InputBox.Text = string.IsNullOrWhiteSpace(existing)
                ? text
                : existing.TrimEnd() + Environment.NewLine + text;
            InputBox.CaretIndex = InputBox.Text.Length;
            InputBox.Focus();
        }

        /// <summary>Renders the documentation index into the Docs overlay using the shipped markdown docs.</summary>
        public void ShowDocsIndex()
        {
            DocsTitleTextControl.Text = "Documentation";
            var markdown = HelpDocumentation.ReadAll();
            DocsContentControl.Content = string.IsNullOrWhiteSpace(markdown)
                ? new TextBlock
                {
                    Text = "Documentation is not available.",
                    Foreground = (Brush)FindResource("Theme.TextMuted")
                }
                : MarkdownRenderer.Render(markdown);
            DocsScroll.ScrollToHome();
        }

        /// <summary>Places the memory-bank update prompt into the chat input box.</summary>
        public void RequestMemoryUpdate()
        {
            SetInputText(MemoryUpdatePrompt);
        }

        /// <summary>Shows or hides the blinking red dot on the "Compact now" button (agent compaction state).</summary>
        public void SetCompactIndicator(bool compacting)
        {
            if (CompactIndicator == null)
                return;

            if (compacting)
            {
                CompactIndicator.Visibility = Visibility.Visible;
                var animation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(500))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                CompactIndicator.BeginAnimation(OpacityProperty, animation);
            }
            else
            {
                CompactIndicator.BeginAnimation(OpacityProperty, null);
                CompactIndicator.Opacity = 1.0;
                CompactIndicator.Visibility = Visibility.Collapsed;
            }
        }

        // ------------------------------------------------------------------
        // Send / input
        // ------------------------------------------------------------------

        private async void OnSend(object sender, RoutedEventArgs e)
        {
            var text = InputBox.Text.Trim();
            var model = Model;
            if (string.IsNullOrWhiteSpace(text) || Host == null || model == null)
                return;

            InputBox.Clear();
            var attachments = model.Attachments.ToList();
            model.Attachments.Clear();
            UpdateAttachmentsVisibility();

            await Host.SendChatAsync(text, attachments);
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            // Enter sends; Ctrl+Enter inserts a newline (handled by the TextBox itself).
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                OnSend(sender, e);
            }
        }

        private void OnClearInput(object sender, RoutedEventArgs e)
        {
            InputBox.Clear();
            InputBox.Focus();
        }

        private async void OnDoItClick(object sender, RoutedEventArgs e)
        {
            var text = InputBox.Text.Trim();
            InputBox.Clear();
            if (Model != null)
                await Model.DoItCommand(text);
        }

        private void AppendSelectionToInput(string text)
        {
            var selectionStart = InputBox.SelectionStart;
            InputBox.Text = InputBox.Text.Insert(selectionStart, text);
            InputBox.SelectionStart = selectionStart + text.Length;
            InputBox.Focus();
        }

        // ------------------------------------------------------------------
        // Attachments
        // ------------------------------------------------------------------

        private void OnAttach(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Attach image or text file",
                Multiselect = true,
                CheckFileExists = true
            };
            if (dlg.ShowDialog() != true)
                return;

            foreach (var fileName in dlg.FileNames)
                TryAddAttachment(fileName);
        }

        private void TryAddAttachment(string fileName)
        {
            var model = Model;
            if (model == null)
                return;

            try
            {
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                var bytes = File.ReadAllBytes(fileName);

                if (IsImageExtension(extension))
                {
                    if (bytes.Length > 10 * 1024 * 1024)
                    {
                        AppendChat("Skipped " + Path.GetFileName(fileName) + ": image is larger than 10 MB.");
                        return;
                    }

                    model.Attachments.Add(new ChatAttachment
                    {
                        FileName = Path.GetFileName(fileName),
                        IsImage = true,
                        MediaType = GetImageMediaType(extension),
                        DataBase64 = Convert.ToBase64String(bytes)
                    });
                    return;
                }

                if (IsProbablyBinary(bytes))
                {
                    AppendChat("Skipped " + Path.GetFileName(fileName) + ": binary files cannot be attached as text.");
                    return;
                }

                model.Attachments.Add(new ChatAttachment
                {
                    FileName = Path.GetFileName(fileName),
                    IsImage = false,
                    TextContent = DecodeText(bytes)
                });
            }
            catch (Exception ex)
            {
                AppendChat("Could not attach " + fileName + ": " + ex.Message);
            }
            finally
            {
                UpdateAttachmentsVisibility();
            }
        }

        private void OnRemoveAttachment(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: ChatAttachment attachment })
            {
                Model?.Attachments.Remove(attachment);
                UpdateAttachmentsVisibility();
            }
        }

        private void UpdateAttachmentsVisibility()
        {
            AttachmentsList.Visibility = Model != null && Model.Attachments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool IsImageExtension(string extension)
        {
            switch (extension)
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".gif":
                case ".bmp":
                case ".webp":
                    return true;
                default:
                    return false;
            }
        }

        private static string GetImageMediaType(string extension)
        {
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".gif":
                    return "image/gif";
                case ".bmp":
                    return "image/bmp";
                case ".webp":
                    return "image/webp";
                default:
                    return "image/png";
            }
        }

        private static bool IsProbablyBinary(byte[] bytes)
        {
            var length = Math.Min(bytes.Length, 8000);
            for (var i = 0; i < length; i++)
            {
                if (bytes[i] == 0)
                    return true;
            }
            return false;
        }

        private static string DecodeText(byte[] bytes)
        {
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(bytes);
            }
        }

        // ------------------------------------------------------------------
        // Toolbar host actions
        // ------------------------------------------------------------------

        private void OnNewChat(object sender, RoutedEventArgs e)
        {
            // The host cancels any run, resets its agent/checkpoint state and resets the model
            // transcript (workspace re-scoping included); the view only switches back.
            Host?.StartNewChat();
            ShowConversationView();
        }

        private void OnCommit(object sender, RoutedEventArgs e)
        {
            Host?.OpenCommitControl();
        }

        private void OnSendErrors(object sender, RoutedEventArgs e)
        {
            Host?.SendBuildErrors();
        }

        private void OnCancelSend(object sender, RoutedEventArgs e)
        {
            Host?.CancelAgentRun();
        }

        private async void OnResumeTask(object sender, RoutedEventArgs e)
        {
            if (Host != null)
                await Host.ResumeAgentRunAsync();
        }

        private async void OnCompactNow(object sender, RoutedEventArgs e)
        {
            if (Host != null)
                await Host.CompactNowAsync();
        }

        private void OnUpdateMemory(object sender, RoutedEventArgs e)
        {
            RequestMemoryUpdate();
        }

        // ------------------------------------------------------------------
        // Mode / permissions / docs
        // ------------------------------------------------------------------

        private void OnModeChanged(object sender, RoutedEventArgs e)
        {
            if (_restoringUiState)
                return;
            Model?.PersistUiState(MainTab.Chat);
        }

        private void OnPermissionsClick(object sender, RoutedEventArgs e)
        {
            var settingsModel = SettingsModel;
            var settings = Settings;
            if (settingsModel != null || settings != null)
            {
                _suppressPermissionEvents = true;
                try
                {
                    AllowReadFilesCheckBox.IsChecked = settingsModel != null ? settingsModel.AllowReadFiles : settings.AllowReadFiles;
                    AllowEditFilesCheckBox.IsChecked = settingsModel != null ? settingsModel.AllowEditFiles : settings.AllowEditFiles;
                    AllowExecuteCommandCheckBox.IsChecked = settingsModel != null ? settingsModel.AllowExecuteCommand : settings.AllowExecuteCommand;
                    AllowWebFetchCheckBox.IsChecked = settingsModel != null ? settingsModel.AllowWebFetch : settings.AllowWebFetch;
                    AllowMcpCheckBox.IsChecked = settingsModel != null ? settingsModel.AllowMcp : settings.AllowMcp;
                    AskBeforeEachToolCheckBox.IsChecked = settingsModel != null ? settingsModel.AskBeforeEachTool : settings.AskBeforeEachTool;
                }
                finally
                {
                    _suppressPermissionEvents = false;
                }
            }

            PermissionsOverlay.Visibility = Visibility.Visible;
        }

        private void OnPermissionsClose(object sender, RoutedEventArgs e)
        {
            PermissionsOverlay.Visibility = Visibility.Collapsed;
        }

        private void OnPermissionChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressPermissionEvents)
                return;

            if (SettingsModel != null)
            {
                SettingsModel.AllowReadFiles = AllowReadFilesCheckBox?.IsChecked ?? true;
                SettingsModel.AllowEditFiles = AllowEditFilesCheckBox?.IsChecked ?? true;
                SettingsModel.AllowExecuteCommand = AllowExecuteCommandCheckBox?.IsChecked ?? false;
                SettingsModel.AllowWebFetch = AllowWebFetchCheckBox?.IsChecked ?? false;
                SettingsModel.AllowMcp = AllowMcpCheckBox?.IsChecked ?? false;
                SettingsModel.AskBeforeEachTool = AskBeforeEachToolCheckBox?.IsChecked ?? false;
                SettingsModel.SaveSettingsCommand();
                Host?.RebuildAgentTools();
                return;
            }

            if (Settings == null)
                return;

            Settings.AllowReadFiles = AllowReadFilesCheckBox?.IsChecked ?? true;
            Settings.AllowEditFiles = AllowEditFilesCheckBox?.IsChecked ?? true;
            Settings.AllowExecuteCommand = AllowExecuteCommandCheckBox?.IsChecked ?? false;
            Settings.AllowWebFetch = AllowWebFetchCheckBox?.IsChecked ?? false;
            Settings.AllowMcp = AllowMcpCheckBox?.IsChecked ?? false;
            Settings.AskBeforeEachTool = AskBeforeEachToolCheckBox?.IsChecked ?? false;
            if (SettingsPersistence != null)
                SettingsPersistence.Save(Settings);
            else
                SaveSettings();
            Host?.RebuildAgentTools();
        }

        private void SaveSettings()
        {
            if (!string.IsNullOrWhiteSpace(AppDataDirectory))
            {
                if (_defaultSettingsStore == null)
                {
                    _defaultSettingsStore = new ToolkitSettingsStore(AppDataDirectory, logger: Logger);
                    WpfLog.Info("ChatControl.SaveSettings: created ToolkitSettingsStore for AppDataDirectory=" + AppDataDirectory);
                }
                WpfLog.Info("ChatControl.SaveSettings: saving via ToolkitSettingsStore.");
                _defaultSettingsStore.Save(Settings);
                return;
            }

            WpfLog.Info("ChatControl.SaveSettings: no AppDataDirectory, saving via legacy SettingsStore.");
            SettingsStore.Save(Settings, Logger);
        }

        private void OnDocsClick(object sender, RoutedEventArgs e)
        {
            ShowDocsIndex();
            DocsOverlay.Visibility = Visibility.Visible;
        }

        private void OnDocsHome(object sender, RoutedEventArgs e)
        {
            ShowDocsIndex();
        }

        private void OnDocsClose(object sender, RoutedEventArgs e)
        {
            DocsOverlay.Visibility = Visibility.Collapsed;
        }

        // ------------------------------------------------------------------
        // History view
        // ------------------------------------------------------------------

        private void OnShowHistory(object sender, RoutedEventArgs e)
        {
            Model?.SaveHistoryNow();
            ShowHistoryView();
        }

        private void OnHistoryCardClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: ChatConversation conversation })
            {
                Host?.OpenConversation(conversation.Id);
                ShowConversationView();
                ChatMessages.ScrollToEnd();
            }
        }

        private void OnToggleImportant(object sender, RoutedEventArgs e)
        {
            CloseHistoryCardMenu(sender);
            if (sender is FrameworkElement { DataContext: ChatConversation conversation }
                && Model != null && !Model.ToggleImportant(conversation.Id))
            {
                AppendExtensionError(Model.LastError);
            }
        }

        private void OnDeleteHistoryCard(object sender, RoutedEventArgs e)
        {
            CloseHistoryCardMenu(sender);
            if (!(sender is FrameworkElement { DataContext: ChatConversation conversation }))
                return;

            if (MessageBox.Show(
                    "Delete this chat?\n\n" + conversation.CardTitle,
                    "Delete chat", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            var deletedActive = Model != null
                && string.Equals(conversation.Id, Model.ActiveConversationId, StringComparison.Ordinal);

            if (Model == null || !Model.DeleteConversation(conversation.Id))
            {
                AppendExtensionError(Model?.LastError);
                return;
            }

            if (deletedActive)
                Host?.OnConversationRemoved();
        }

        private static void CloseHistoryCardMenu(object sender)
        {
            if (!(sender is DependencyObject element))
                return;

            var parent = LogicalTreeHelper.GetParent(element);
            while (parent != null && !(parent is Popup))
            {
                parent = LogicalTreeHelper.GetParent(parent);
            }

            if (parent is Popup popup)
            {
                popup.IsOpen = false;
            }
        }

        private void OnClearHistory(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                    "Delete all chats? This cannot be undone.",
                    "Clear history", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            if (Model == null || !Model.ClearHistory())
            {
                AppendExtensionError(Model?.LastError);
                return;
            }

            Host?.OnConversationRemoved();
        }

        private void OnCloseChat(object sender, RoutedEventArgs e)
        {
            Host?.CancelAgentRun();
            Model?.CloseActiveChat();
            ApplyChatViewMode();
            Host?.OnConversationRemoved();
        }

        // ------------------------------------------------------------------
        // Pinned message / clipboard
        // ------------------------------------------------------------------

        private void OnPinnedExpandToggle(object sender, RoutedEventArgs e)
        {
            _pinnedExpanded = !_pinnedExpanded;
            UpdatePinnedExpandState();
        }

        private void UpdatePinnedExpandState()
        {
            PinnedSummaryText.Visibility = _pinnedExpanded ? Visibility.Collapsed : Visibility.Visible;
            PinnedTextScroll.Visibility = _pinnedExpanded ? Visibility.Visible : Visibility.Collapsed;
            PinnedTextScroll.MaxHeight = PinnedExpandedHeight;
            PinnedTextScroll.VerticalScrollBarVisibility = _pinnedExpanded ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
            PinnedExpandGlyph.Points = PointCollection.Parse(_pinnedExpanded ? "6,9 12,15 18,9" : "9,18 15,12 9,6");
            ChatContextIndicator.ShowDetails = _pinnedExpanded;
        }

        private void OnCopyMessageRequested(object sender, ChatMessage message)
        {
            if (message == null) return;
            try { Clipboard.SetText(FormatMessageForCopy(message)); }
            catch { /* clipboard busy — ignore */ }
        }

        private void OnCopyPinnedMessage(object sender, RoutedEventArgs e)
        {
            if (Model?.PinnedMessage == null) return;
            try { Clipboard.SetText(FormatMessageForCopy(Model.PinnedMessage)); }
            catch { /* clipboard busy — ignore */ }
        }

        private void OnCopyConversation(object sender, RoutedEventArgs e)
        {
            if (Model == null) return;
            var sb = new StringBuilder();
            foreach (var m in Model.Messages)
            {
                if (sb.Length > 0) sb.AppendLine().AppendLine();
                sb.Append(FormatMessageForCopy(m));
            }
            if (sb.Length == 0) return;
            try { Clipboard.SetText(sb.ToString()); }
            catch { /* clipboard busy — ignore */ }
        }

        private static string FormatMessageForCopy(ChatMessage message)
            => ChatMessageCopyFormatter.Format(message);

        // ------------------------------------------------------------------
        // Mic
        // ------------------------------------------------------------------

        private async void OnMicClick(object sender, RoutedEventArgs e)
        {
            if (_isTranscribing) return;

            if (!_isRecording)
            {
                try
                {
                    _mic?.Start();
                }
                catch (Exception ex)
                {
                    ShowVoiceError("Microphone unavailable: " + ex.Message);
                    return;
                }
                _isRecording = true;
                SetMicState(recording: true);
            }
            else
            {
                byte[] audio;
                try
                {
                    audio = _mic?.Stop();
                }
                catch (Exception ex)
                {
                    _isRecording = false;
                    SetMicState(recording: false);
                    ShowVoiceError("Failed to stop recording: " + ex.Message);
                    return;
                }

                _isRecording = false;
                SetMicState(recording: false);

                if (audio == null || audio.Length == 0)
                {
                    AppendSystemMessage("No speech detected.");
                    return;
                }

                _isTranscribing = true;
                SetMicState(recording: false, transcribing: true);
                try
                {
                    var language = (Settings?.SpeechToTextLanguage ?? "ru").Trim();
                    var text = _stt != null
                        ? await _stt.TranscribeAsync(audio, language, CancellationToken.None)
                        : "";

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (Settings?.SpeechAutoSend == true && string.IsNullOrWhiteSpace(InputBox.Text))
                        {
                            InputBox.Text = text;
                            OnSend(sender, e);
                        }
                        else
                        {
                            AppendSelectionToInput(text);
                        }
                    }
                    else
                    {
                        AppendSystemMessage("No speech recognized.");
                    }
                }
                catch (Exception ex)
                {
                    ShowVoiceError("Speech recognition failed: " + ex.Message);
                }
                finally
                {
                    _isTranscribing = false;
                    SetMicState(recording: false);
                }
            }
        }

        private void OnMicLevelChanged(object sender, double level)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnMicLevelChanged(sender, level)));
                return;
            }

            MicButton.Opacity = _isRecording ? 0.6 + level * 0.4 : 1.0;
        }

        private void SetMicState(bool recording, bool transcribing = false)
        {
            MicButton.ToolTip = transcribing ? "Transcribing…" : (recording ? "Stop recording" : "Start voice input");
            MicButton.Opacity = 1.0;
            MicIcon.Fill = transcribing
                ? Brushes.Orange
                : recording
                    ? Brushes.Red
                    : (Brush)FindResource("Theme.Text");
        }

        private void ShowVoiceError(string text)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => ShowVoiceError(text)));
                return;
            }
            VoiceError?.Invoke(this, text ?? "");
        }

        private sealed class HostPromptSink : IChatPromptSink
        {
            private readonly IChatHost _host;

            public HostPromptSink(IChatHost host) => _host = host;

            public Task SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
                => _host.SendChatAsync(prompt, null);
        }
    }
}





