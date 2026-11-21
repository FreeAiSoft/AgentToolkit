using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Agent.Core.Contracts;

namespace Agent.Core.Services
{
    /// <summary>
    /// Default SQLite-backed settings store for the toolkit. Settings live in a single row
    /// (<c>key='settings'</c>) inside the global <c>toolkit.db</c> under the host's app-data
    /// folder. Settings are deliberately global (not per-workspace): the workspace-scoped
    /// <see cref="IWorkspaceProvider"/> parameter is accepted for forward compatibility with the
    /// upcoming per-workspace chat-history store, but it does not change the settings location.
    /// A legacy JSON file is imported once into an empty database.
    /// </summary>
    public sealed class ToolkitSettingsStore : ISettingsStore
    {
        private const string SettingsKey = "settings";

        private readonly string _appDataDirectory;
        private readonly string _legacySettingsFile;

        public ToolkitSettingsStore(
            string appDataDirectory,
            IWorkspaceProvider workspaceProvider = null,
            string legacySettingsFile = null)
        {
            if (appDataDirectory == null)
                throw new ArgumentNullException(nameof(appDataDirectory));

            _appDataDirectory = appDataDirectory;
            _legacySettingsFile = legacySettingsFile;

            // workspaceProvider is intentionally unused: settings must survive a restart even
            // when the open workspace changes. Per-workspace storage is reserved for history.
        }

        private string DatabasePath =>
            ToolkitStoragePaths.SettingsDatabase(_appDataDirectory, "");

        /// <inheritdoc />
        public SettingsData Load()
        {
            try
            {
                var databasePath = DatabasePath;
                Directory.CreateDirectory(Path.GetDirectoryName(databasePath));

                using (var connection = OpenConnection(databasePath))
                {
                    EnsureSchema(connection);

                    var valueJson = ReadSettingsValue(connection);
                    if (valueJson == null)
                    {
                        var legacy = TryLoadLegacy();
                        if (legacy != null)
                        {
                            valueJson = JsonConvert.SerializeObject(legacy);
                            UpsertSettings(connection, valueJson);
                        }
                    }

                    if (valueJson != null)
                    {
                        var settings = JsonConvert.DeserializeObject<SettingsData>(valueJson) ?? new SettingsData();
                        SettingsData.Normalize(settings);
                        return settings;
                    }
                }
            }
            catch
            {
                // Ignore storage errors and fall back to defaults, matching the JSON store.
            }

            var defaults = new SettingsData();
            SettingsData.Normalize(defaults);
            return defaults;
        }

        /// <inheritdoc />
        public void Save(SettingsData settings)
        {
            if (settings == null)
                return;

            try
            {
                SettingsData.Normalize(settings);

                var databasePath = DatabasePath;
                Directory.CreateDirectory(Path.GetDirectoryName(databasePath));

                using (var connection = OpenConnection(databasePath))
                {
                    EnsureSchema(connection);
                    UpsertSettings(connection, JsonConvert.SerializeObject(settings));
                }
            }
            catch
            {
                // Ignore write errors, matching the JSON store.
            }
        }

        private SettingsData TryLoadLegacy()
        {
            if (string.IsNullOrWhiteSpace(_legacySettingsFile))
                return null;

            try
            {
                if (!File.Exists(_legacySettingsFile))
                    return null;

                var settings = JsonConvert.DeserializeObject<SettingsData>(File.ReadAllText(_legacySettingsFile));
                if (settings == null)
                    return null;

                SettingsData.Normalize(settings);
                return settings;
            }
            catch
            {
                return null;
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
            ExecuteNonQuery(connection, @"
CREATE TABLE IF NOT EXISTS settings (
    key TEXT PRIMARY KEY,
    value_json TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);");
            ExecuteNonQuery(connection, "PRAGMA user_version = 1;");
        }

        private static string ReadSettingsValue(SqliteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT value_json FROM settings WHERE key = $key LIMIT 1;";
                command.Parameters.AddWithValue("$key", SettingsKey);
                return command.ExecuteScalar() as string;
            }
        }

        private static void UpsertSettings(SqliteConnection connection, string valueJson)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO settings (key, value_json, updated_at_utc)
VALUES ($key, $value, $updated)
ON CONFLICT(key) DO UPDATE SET
    value_json = excluded.value_json,
    updated_at_utc = excluded.updated_at_utc;";
                command.Parameters.AddWithValue("$key", SettingsKey);
                command.Parameters.AddWithValue("$value", valueJson);
                command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
                command.ExecuteNonQuery();
            }
        }

        private static void ExecuteNonQuery(SqliteConnection connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}
