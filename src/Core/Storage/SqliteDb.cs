using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using CopyOpsSuite.Core.Models;

namespace CopyOpsSuite.Storage
{
    public sealed class SqliteDb
    {
        private readonly string _connectionString;
        private const string Ddl = @"CREATE TABLE IF NOT EXISTS settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS devices_protected (
    deviceId TEXT PRIMARY KEY
);
CREATE TABLE IF NOT EXISTS exclusions (
    pattern TEXT PRIMARY KEY
);
CREATE TABLE IF NOT EXISTS profiles (
    profileId TEXT PRIMARY KEY,
    json TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS transfer_jobs (
    jobId TEXT PRIMARY KEY,
    startedAt TEXT NOT NULL,
    endedAt TEXT,
    operatorName TEXT,
    sourcePath TEXT,
    profileId TEXT,
    status TEXT,
    bytesPlanned INTEGER,
    bytesOk INTEGER,
    bytesFailed INTEGER
);
CREATE TABLE IF NOT EXISTS transfer_targets (
    jobId TEXT,
    deviceId TEXT,
    targetRootPath TEXT,
    status TEXT,
    bytesOk INTEGER,
    bytesFailed INTEGER,
    currentMBps REAL,
    maxMBps REAL,
    avgMBps REAL,
    queueCount INTEGER,
    elapsedMs INTEGER,
    etaMs INTEGER,
    miniSimple INTEGER,
    miniOpacity INTEGER,
    miniDockSide TEXT,
    miniTopMargin INTEGER,
    PRIMARY KEY(jobId, deviceId)
);
CREATE TABLE IF NOT EXISTS transfer_items (
    id TEXT PRIMARY KEY,
    jobId TEXT,
    deviceId TEXT,
    action TEXT,
    sourcePath TEXT,
    destinationPath TEXT,
    sizeBytes INTEGER,
    extension TEXT,
    status TEXT,
    ts TEXT
);
CREATE TABLE IF NOT EXISTS validations (
    id TEXT PRIMARY KEY,
    jobId TEXT,
    rule TEXT,
    result TEXT,
    details TEXT,
    ts TEXT
);
CREATE TABLE IF NOT EXISTS errors (
    id TEXT PRIMARY KEY,
    jobId TEXT,
    deviceId TEXT,
    filePath TEXT,
    errorCode TEXT,
    message TEXT,
    ts TEXT
);
CREATE TABLE IF NOT EXISTS ram_stats (
    id TEXT PRIMARY KEY,
    jobId TEXT,
    bytesBuffered INTEGER,
    queueDepth INTEGER,
    throughputMBps REAL,
    ts TEXT
);
CREATE TABLE IF NOT EXISTS sales (
    saleId TEXT PRIMARY KEY,
    jobId TEXT,
    ts TEXT,
    operatorName TEXT,
    customerName TEXT,
    currency TEXT,
    amountExpected REAL,
    amountPaid REAL,
    paymentMethod TEXT,
    status TEXT,
    notes TEXT,
    realGb REAL,
    billableGb REAL,
    roundMode TEXT
);
CREATE TABLE IF NOT EXISTS cash_register (
    day TEXT PRIMARY KEY,
    openingAmount REAL,
    closingAmount REAL,
    countedAmount REAL,
    expectedAmount REAL,
    notes TEXT
);
CREATE TABLE IF NOT EXISTS events (
    eventId TEXT PRIMARY KEY,
    ts TEXT,
    type TEXT,
    severity TEXT,
    message TEXT,
    jobId TEXT,
    deviceId TEXT,
    saleId TEXT
);
CREATE TABLE IF NOT EXISTS sessions (
    sessionId TEXT PRIMARY KEY,
    name TEXT,
    start TEXT,
    end TEXT,
    operatorName TEXT,
    notes TEXT
);
CREATE TABLE IF NOT EXISTS alerts_resolved (
    alertId TEXT PRIMARY KEY,
    message TEXT NOT NULL,
    severity TEXT NOT NULL,
    jobId TEXT,
    deviceId TEXT,
    raisedAt TEXT NOT NULL,
    resolvedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS pricing_tiers (
    tierId TEXT PRIMARY KEY,
    minGb INTEGER NOT NULL,
    maxGb INTEGER NOT NULL,
    priceCup INTEGER NOT NULL,
    isActive INTEGER NOT NULL,
    sortOrder INTEGER NOT NULL
);
";

        public SqliteDb()
        {
            var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopyOpsSuite");
            Directory.CreateDirectory(basePath);
            FilePath = Path.Combine(basePath, "app.db");
            var builder = new SqliteConnectionStringBuilder { DataSource = FilePath, Mode = SqliteOpenMode.ReadWriteCreate };
            _connectionString = builder.ToString();
            InitializeAsync().GetAwaiter().GetResult();
        }

        public string FilePath { get; }

        public string ConnectionString => _connectionString;

        public SqliteConnection CreateConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        public async Task<SqliteConnection> CreateOpenConnectionAsync()
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            return connection;
        }

        private async Task InitializeAsync()
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = Ddl;
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            await SeedDefaultsAsync(connection).ConfigureAwait(false);
        }

        private static async Task SeedDefaultsAsync(SqliteConnection connection)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO exclusions(pattern) VALUES (@pattern);";
            foreach (var pattern in new[] { "thumbs.db", "desktop.ini", "*.tmp" })
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@pattern", pattern);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            var defaultProfile = new CopyProfile
            {
                ProfileId = "PROFILE_DEFAULT",
                Name = "Default",
                PricingMode = PricingMode.Mixed,
                PricePerGB = 1m,
                Currency = "CUP",
                RoundMode = RoundMode.CeilGB,
                DefaultExclusions = new List<string> { "thumbs.db", "desktop.ini", "*.tmp" }
            };
            var json = JsonSerializer.Serialize(defaultProfile);
            await using var profileCommand = connection.CreateCommand();
            profileCommand.CommandText = "INSERT OR IGNORE INTO profiles(profileId, json) VALUES (@profileId, @json);";
            profileCommand.Parameters.AddWithValue("@profileId", defaultProfile.ProfileId);
            profileCommand.Parameters.AddWithValue("@json", json);
            await profileCommand.ExecuteNonQueryAsync().ConfigureAwait(false);

            var defaultSettings = new Dictionary<string, string>
            {
                ["mini_auto_open"] = "1",
                ["mini_auto_close_no_errors"] = "1",
                ["mini_auto_clean_no_errors"] = "1",
                ["mini_dock_side"] = "Left",
                ["mini_top_margin"] = "80",
                ["mini_opacity_percent"] = "90",
                ["mini_simple_mode_default"] = "0",
                ["mini_topmost_pin"] = "0",
                ["max_parallel_targets"] = "3",
                ["max_files_parallel_per_target"] = "1",
                ["buffer_enable"] = "1",
                ["buffer_chunk_size_mb"] = "4",
                ["buffer_max_chunks"] = "32",
                ["max_writer_workers_per_target"] = "1",
                ["access_operator_mode"] = "1",
                ["admin_pin_hash"] = "5bdb2623cb7db457735b0bbc050e75ab7f91d38de2bdafb4a3601bf3a7162c33",
                ["admin_pin_salt"] = "default",
                ["simulation_usb2_mb"] = "20",
                ["simulation_usb3_mb"] = "90",
                ["simulation_unknown_mb"] = "50",
                ["billing_mode"] = "PerJob",
                ["app_theme"] = "Default"
            };

            await using var settingCommand = connection.CreateCommand();
            settingCommand.CommandText = "INSERT OR IGNORE INTO settings(key, value) VALUES (@key, @value);";
            foreach (var kvp in defaultSettings)
            {
                settingCommand.Parameters.Clear();
                settingCommand.Parameters.AddWithValue("@key", kvp.Key);
                settingCommand.Parameters.AddWithValue("@value", kvp.Value);
                await settingCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
    }
}
