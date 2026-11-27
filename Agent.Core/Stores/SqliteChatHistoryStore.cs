using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Agent.Core.Contracts;
using Agent.Core.Models;
using Agent.Core.Services;

namespace Agent.Core.Stores
{
    /// <summary>
    /// Default SQLite-backed <see cref="IChatHistoryStore"/> for the toolkit. Conversations live
    /// in the same <c>toolkit.db</c> as the settings store, scoped per workspace through
    /// <see cref="WorkspaceId"/> (empty = global scope). The store is self-contained: it creates
    /// the database, schema and directories on first use and swallows IO failures like the other
    /// toolkit stores.
    /// </summary>
    public sealed class SqliteChatHistoryStore : IChatHistoryStore
    {
        private readonly string _appDataDirectory;
        private readonly IAgentLogger _logger;

        public SqliteChatHistoryStore(string appDataDirectory, IAgentLogger logger = null)
        {
            if (appDataDirectory == null)
                throw new ArgumentNullException(nameof(appDataDirectory));

            _appDataDirectory = appDataDirectory;
            _logger = logger ?? NoOpLogger.Instance;
        }

        /// <inheritdoc />
        public string WorkspaceId { get; set; } = "";

        private string DatabasePath =>
            ToolkitStoragePaths.SettingsDatabase(_appDataDirectory, WorkspaceId ?? "");

        /// <inheritdoc />
        public void Initialize()
        {
            _logger.Info("[history-store] Initialize: databasePath=" + DatabasePath);
            try
            {
                EnsureDatabase(out _);
                _logger.Info("[history-store] Initialize: ok");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SqliteChatHistoryStore.Initialize failed.");
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<ChatConversation> ListConversations(int limit = 50)
        {
            try
            {
                EnsureDatabase(out var databasePath);
                _logger.Info("[history-store] ListConversations: databasePath=" + databasePath + " limit=" + limit);

                using (var connection = OpenConnection(databasePath))
                {
                    var result = new List<ChatConversation>();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
SELECT id, title, created_at_utc, updated_at_utc, is_important
FROM conversations
ORDER BY updated_at_utc DESC
LIMIT $limit;";
                        command.Parameters.AddWithValue("$limit", Math.Max(0, limit));
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Add(ReadMetadata(reader));
                            }
                        }
                    }

                    _logger.Info("[history-store] ListConversations: count=" + result.Count);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SqliteChatHistoryStore.ListConversations failed.");
                return new List<ChatConversation>();
            }
        }

        /// <inheritdoc />
        public ChatConversation LoadConversation(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            _logger.Info("[history-store] LoadConversation: id=" + id);
            try
            {
                EnsureDatabase(out var databasePath);

                using (var connection = OpenConnection(databasePath))
                {
                    var conversation = ReadConversationMetadata(connection, id);
                    if (conversation == null)
                        return null;

                    ApplyConversationExtras(connection, conversation, id);
                    conversation.Messages = ReadMessages(connection, id);
                    _logger.Info("[history-store] LoadConversation: loaded id=" + id + " messages=" + conversation.Messages.Count);
                    return conversation;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SqliteChatHistoryStore.LoadConversation failed for id=" + id);
                return null;
            }
        }

        /// <inheritdoc />
        public void SaveConversation(ChatConversation conversation)
        {
            if (conversation == null || string.IsNullOrWhiteSpace(conversation.Id))
                return;

            _logger.Info("[history-store] SaveConversation: id=" + conversation.Id + " title='" + (conversation.Title ?? "") + "' messages=" + (conversation.Messages?.Count ?? 0));
            try
            {
                EnsureDatabase(out var databasePath);

                using (var connection = OpenConnection(databasePath))
                using (var transaction = connection.BeginTransaction())
                {
                    var updatedAtUtc = conversation.UpdatedAtUtc == default(DateTime)
                        ? DateTime.UtcNow
                        : conversation.UpdatedAtUtc;

                    UpsertConversation(connection, transaction, conversation, updatedAtUtc);

                    using (var delete = connection.CreateCommand())
                    {
                        delete.Transaction = transaction;
                        delete.CommandText = "DELETE FROM messages WHERE conversation_id = $id;";
                        delete.Parameters.AddWithValue("$id", conversation.Id);
                        delete.ExecuteNonQuery();
                    }

                    var messages = conversation.Messages ?? new List<ChatHistoryMessage>();
                    for (var seq = 0; seq < messages.Count; seq++)
                    {
                        InsertMessage(connection, transaction, conversation.Id, seq, messages[seq]);
                    }

                    _logger.Info("[history-store] SaveConversation: committed id=" + conversation.Id + " messages=" + messages.Count);
                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SqliteChatHistoryStore.SaveConversation failed for id=" + conversation.Id);
            }
        }

        /// <inheritdoc />
        public void SetConversationImportant(string id, bool isImportant)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            _logger.Info("[history-store] SetConversationImportant: id=" + id + " isImportant=" + isImportant);
            try
            {
                EnsureDatabase(out var databasePath);

                using (var connection = OpenConnection(databasePath))
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE conversations SET is_important = $value WHERE id = $id;";
                    command.Parameters.AddWithValue("$value", isImportant ? 1L : 0L);
                    command.Parameters.AddWithValue("$id", id);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SqliteChatHistoryStore.SetConversationImportant failed for id=" + id);
            }
        }

        /// <inheritdoc />
        public void DeleteConversation(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            _logger.Info("[history-store] DeleteConversation: id=" + id);
            try
            {
                EnsureDatabase(out var databasePath);

                using (var connection = OpenConnection(databasePath))
                using (var transaction = connection.BeginTransaction())
                {
                    ExecuteNonQuery(connection, transaction,
                        "DELETE FROM messages WHERE conversation_id = $id;",
                        ("$id", id));

                    ExecuteNonQuery(connection, transaction,
                        "DELETE FROM conversations WHERE id = $id;",
                        ("$id", id));

                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SqliteChatHistoryStore.DeleteConversation failed for id=" + id);
            }
        }

        /// <inheritdoc />
        public void DeleteAllConversations()
        {
            _logger.Info("[history-store] DeleteAllConversations");
            try
            {
                EnsureDatabase(out var databasePath);

                using (var connection = OpenConnection(databasePath))
                using (var transaction = connection.BeginTransaction())
                {
                    using (var deleteMessages = connection.CreateCommand())
                    {
                        deleteMessages.Transaction = transaction;
                        deleteMessages.CommandText = "DELETE FROM messages;";
                        deleteMessages.ExecuteNonQuery();
                    }

                    using (var deleteConversations = connection.CreateCommand())
                    {
                        deleteConversations.Transaction = transaction;
                        deleteConversations.CommandText = "DELETE FROM conversations;";
                        deleteConversations.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SqliteChatHistoryStore.DeleteAllConversations failed.");
            }
        }

        private void EnsureDatabase(out string databasePath)
        {
            databasePath = DatabasePath;
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath));

            using (var connection = OpenConnection(databasePath))
            {
                EnsureSchema(connection);
            }
        }

        private static SqliteConnection OpenConnection(string databasePath)
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath
            }.ToString();

            var connection = new SqliteConnection(connectionString);
            connection.Open();
            return connection;
        }

        private static void EnsureSchema(SqliteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
CREATE TABLE IF NOT EXISTS conversations (
    id                     TEXT PRIMARY KEY,
    title                  TEXT NOT NULL DEFAULT '',
    created_at_utc         TEXT NOT NULL,
    updated_at_utc         TEXT NOT NULL,
    is_important           INTEGER NOT NULL DEFAULT 0,
    provider_transcript_json TEXT,
    usage_json             TEXT
);

CREATE TABLE IF NOT EXISTS messages (
    conversation_id TEXT NOT NULL,
    seq             INTEGER NOT NULL,
    kind            TEXT NOT NULL,
    text            TEXT NOT NULL DEFAULT '',
    reasoning       TEXT NOT NULL DEFAULT '',
    mode            TEXT NOT NULL DEFAULT '',
    tool_name       TEXT NOT NULL DEFAULT '',
    options_json    TEXT,
    checkpoint_hash TEXT NOT NULL DEFAULT '',
    timestamp_utc   TEXT NOT NULL,
    PRIMARY KEY (conversation_id, seq)
);";
                command.ExecuteNonQuery();
            }
        }

        private static ChatConversation ReadConversationMetadata(SqliteConnection connection, string id)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT id, title, created_at_utc, updated_at_utc, is_important
FROM conversations
WHERE id = $id
LIMIT 1;";
                command.Parameters.AddWithValue("$id", id);
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? ReadMetadata(reader) : null;
                }
            }
        }

        private static ChatConversation ReadMetadata(SqliteDataReader reader)
        {
            return new ChatConversation
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                CreatedAtUtc = ParseUtc(reader.GetString(2)),
                UpdatedAtUtc = ParseUtc(reader.GetString(3)),
                IsImportant = reader.GetInt64(4) != 0
            };
        }

        private static void ApplyConversationExtras(SqliteConnection connection, ChatConversation conversation, string id)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT provider_transcript_json, usage_json FROM conversations WHERE id = $id LIMIT 1;";
                command.Parameters.AddWithValue("$id", id);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        conversation.ProviderTranscriptJson = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        ApplyUsage(conversation, reader.IsDBNull(1) ? null : reader.GetString(1));
                    }
                }
            }
        }

        private static List<ChatHistoryMessage> ReadMessages(SqliteConnection connection, string id)
        {
            var result = new List<ChatHistoryMessage>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT kind, text, reasoning, mode, tool_name, options_json, checkpoint_hash, timestamp_utc
FROM messages
WHERE conversation_id = $id
ORDER BY seq;";
                command.Parameters.AddWithValue("$id", id);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new ChatHistoryMessage
                        {
                            Kind = ParseKind(reader.GetString(0)),
                            Text = reader.GetString(1),
                            Reasoning = reader.GetString(2),
                            Mode = reader.GetString(3),
                            ToolName = reader.GetString(4),
                            Options = DeserializeOptions(reader.IsDBNull(5) ? null : reader.GetString(5)),
                            CheckpointHash = reader.GetString(6),
                            TimestampUtc = ParseUtc(reader.GetString(7))
                        });
                    }
                }
            }

            return result;
        }

        private static void UpsertConversation(
            SqliteConnection connection,
            SqliteTransaction transaction,
            ChatConversation conversation,
            DateTime updatedAtUtc)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO conversations (id, title, created_at_utc, updated_at_utc, is_important, provider_transcript_json, usage_json)
VALUES ($id, $title, $created, $updated, $important, $transcript, $usage)
ON CONFLICT(id) DO UPDATE SET
    title = excluded.title,
    created_at_utc = excluded.created_at_utc,
    updated_at_utc = excluded.updated_at_utc,
    is_important = excluded.is_important,
    provider_transcript_json = excluded.provider_transcript_json,
    usage_json = excluded.usage_json;";

                var createdAtUtc = conversation.CreatedAtUtc == default(DateTime)
                    ? updatedAtUtc
                    : conversation.CreatedAtUtc;

                command.Parameters.AddWithValue("$id", conversation.Id);
                command.Parameters.AddWithValue("$title", conversation.Title ?? "");
                command.Parameters.AddWithValue("$created", ToUtc(createdAtUtc));
                command.Parameters.AddWithValue("$updated", ToUtc(updatedAtUtc));
                command.Parameters.AddWithValue("$important", conversation.IsImportant ? 1L : 0L);
                command.Parameters.AddWithValue("$transcript", (object)(conversation.ProviderTranscriptJson ?? "") ?? DBNull.Value);
                command.Parameters.AddWithValue("$usage", SerializeUsage(conversation.Usage));
                command.ExecuteNonQuery();
            }
        }

        private static void InsertMessage(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string conversationId,
            int seq,
            ChatHistoryMessage message)
        {
            if (message == null)
                return;

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO messages (conversation_id, seq, kind, text, reasoning, mode, tool_name, options_json, checkpoint_hash, timestamp_utc)
VALUES ($conversationId, $seq, $kind, $text, $reasoning, $mode, $toolName, $options, $checkpointHash, $timestamp);";

                var timestamp = message.TimestampUtc == default(DateTime)
                    ? DateTime.UtcNow
                    : message.TimestampUtc;

                command.Parameters.AddWithValue("$conversationId", conversationId);
                command.Parameters.AddWithValue("$seq", (long)seq);
                command.Parameters.AddWithValue("$kind", message.Kind.ToString());
                command.Parameters.AddWithValue("$text", message.Text ?? "");
                command.Parameters.AddWithValue("$reasoning", message.Reasoning ?? "");
                command.Parameters.AddWithValue("$mode", message.Mode ?? "");
                command.Parameters.AddWithValue("$toolName", message.ToolName ?? "");
                command.Parameters.AddWithValue("$options", SerializeOptions(message.Options));
                command.Parameters.AddWithValue("$checkpointHash", message.CheckpointHash ?? "");
                command.Parameters.AddWithValue("$timestamp", ToUtc(timestamp));
                command.ExecuteNonQuery();
            }
        }

        private static void ExecuteNonQuery(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string sql,
            params (string Name, string Value)[] parameters)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                foreach (var parameter in parameters)
                    command.Parameters.AddWithValue(parameter.Name, parameter.Value);
                command.ExecuteNonQuery();
            }
        }

        private static string ToUtc(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);

        private static DateTime ParseUtc(string value)
            => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : default(DateTime);

        private static ChatMessageKind ParseKind(string value)
            => Enum.TryParse(value, out ChatMessageKind kind) ? kind : ChatMessageKind.SystemNotice;

        private static string SerializeOptions(List<string> options)
            => JsonConvert.SerializeObject(options ?? new List<string>());

        private static List<string> DeserializeOptions(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<string>();

            try
            {
                return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static string SerializeUsage(TokenUsage usage)
        {
            if (usage == null)
                return null;

            return JsonConvert.SerializeObject(usage);
        }

        private static void ApplyUsage(ChatConversation conversation, string json)
        {
            if (string.IsNullOrWhiteSpace(json) || conversation == null)
                return;

            try
            {
                var usage = JsonConvert.DeserializeObject<TokenUsage>(json);
                if (usage == null)
                    return;

                conversation.Usage.PromptTokens = usage.PromptTokens;
                conversation.Usage.CompletionTokens = usage.CompletionTokens;
                conversation.Usage.TotalTokens = usage.TotalTokens;
                conversation.Usage.CacheHitTokens = usage.CacheHitTokens;
                conversation.Usage.CacheMissTokens = usage.CacheMissTokens;
                conversation.Usage.EstimatedCostUsd = usage.EstimatedCostUsd;
            }
            catch
            {
                // Usage is a runtime convenience; a broken payload must not break history load.
            }
        }
    }
}
