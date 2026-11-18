using System.Collections.Generic;
using System.Threading.Tasks;
using Agent.Core.Models;

namespace Agent.Toolkit.Wpf.Controls
{
    /// <summary>
    /// Host capabilities that <see cref="ChatControl"/> needs to run the generic agent chat UI.
    /// The WPF host (e.g. RPAWriter2's <c>MainWindowControl</c>) implements this interface to wire
    /// the chat view to RPA/VS/Git-specific services: the agent loop, the commit control, build
    /// errors, robot creation and workspace scoping.
    /// </summary>
    /// <remarks>
    /// Everything here is a deliberate "seam": <see cref="ChatControl"/> owns the chat view and its
    /// generic logic (input, attachments, history, permissions/docs overlays), and delegates to
    /// <see cref="IChatHost"/> only for the operations that touch host services. The host never
    /// leaks DTE, Git, Roslyn or robot services into the toolkit.
    /// </remarks>
    public interface IChatHost
    {
        /// <summary>
        /// Sends the given text (and optional attachments) to the agent, then drains the pending
        /// message queue. Called by the chat view when the user sends a message.
        /// </summary>
        Task SendChatAsync(string text, IReadOnlyList<ChatAttachment> attachments);

        /// <summary>
        /// Cancels the currently running agent turn and remembers that it is resumable, so the
        /// chat can offer "Resume task" once the run has stopped.
        /// </summary>
        void CancelAgentRun();

        /// <summary>Resumes the last interrupted agent run.</summary>
        Task ResumeAgentRunAsync();

        /// <summary>Whether an agent turn is currently running (drives the Cancel/Resume status UI).</summary>
        bool IsAgentRunning { get; }

        /// <summary>Opens the commit control (Git + VS document save).</summary>
        void OpenCommitControl();

        /// <summary>Feeds the current VS build/compilation errors into the chat as agent context.</summary>
        void SendBuildErrors();

        /// <summary>Opens the create-robot dialog.</summary>
        void OpenCreateRobot();

        /// <summary>Rebuilds the agent tool registry from the current settings and permissions.</summary>
        void RebuildAgentTools();

        /// <summary>
        /// Starts a fresh chat conversation. The host cancels any running turn, resets its agent,
        /// checkpoint and workspace-scoping state and resets the model transcript (see
        /// <c>ChatModel.StartNewChat</c>); the chat view switches back to the conversation view.
        /// </summary>
        void StartNewChat();

        /// <summary>
        /// Opens a saved conversation by id. The host cancels any running turn, opens the
        /// conversation in the model, rehydrates checkpoint/diff metadata and restores the
        /// provider transcript; the chat view then switches back to the conversation view.
        /// </summary>
        void OpenConversation(string id);

        /// <summary>
        /// Drops host-side state (resume snapshot, checkpoint baseline, save timer) that belonged
        /// to a conversation that was just closed, deleted or cleared.
        /// </summary>
        void OnConversationRemoved();

        /// <summary>Renders the documentation index into the chat's Docs overlay.</summary>
        void ShowDocsIndex();

        /// <summary>Runs context compaction through the host agent runner and applies the condensed transcript.</summary>
        Task CompactNowAsync();

        /// <summary>Places the memory-bank update prompt into the chat input box.</summary>
        void RequestMemoryUpdate();
    }
}
