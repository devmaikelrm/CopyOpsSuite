using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using CopyOpsSuite.Core.Models;

namespace CopyOpsSuite.Storage
{
    public sealed class Repositories
    {
        public SettingsRepository Settings { get; }
        public ProfileRepository Profiles { get; }
        public JobsRepository Jobs { get; }
        public TargetsRepository Targets { get; }
        public ItemsRepository Items { get; }
        public ValidationRepository Validations { get; }
        public ErrorRepository Errors { get; }
        public RamStatsRepository RamStats { get; }
        public SalesRepository Sales { get; }
        public CashRegisterRepository CashRegister { get; }
        public EventsRepository Events { get; }
        public SessionsRepository Sessions { get; }
        public DeviceProtectionRepository DeviceProtections { get; }
        public ExclusionsRepository Exclusions { get; }
        public AlertsRepository Alerts { get; }
        public PricingTiersRepository PricingTiers { get; }

        public Repositories(SqliteDb db)
        {
            Settings = new SettingsRepository(db);
            Profiles = new ProfileRepository(db);
            Jobs = new JobsRepository(db);
            Targets = new TargetsRepository(db);
            Items = new ItemsRepository(db);
            Validations = new ValidationRepository(db);
            Errors = new ErrorRepository(db);
            RamStats = new RamStatsRepository(db);
            Sales = new SalesRepository(db);
            CashRegister = new CashRegisterRepository(db);
            Events = new EventsRepository(db);
            Sessions = new SessionsRepository(db);
            DeviceProtections = new DeviceProtectionRepository(db);
            Exclusions = new ExclusionsRepository(db);
            Alerts = new AlertsRepository(db);
            PricingTiers = new PricingTiersRepository(db);
        }
    }

    public sealed class SettingsRepository
    {
        private readonly SqliteDb _db;

        public SettingsRepository(SqliteDb db) => _db = db;

        public async Task<string?> GetAsync(string key)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM settings WHERE key = @key LIMIT 1;";
            command.Parameters.AddWithValue("@key", key);
            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            return result as string;
        }

        public async Task SetAsync(string key, string value)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO settings(key, value) VALUES(@key, @value);";
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", value);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public sealed class DeviceProtectionRepository
    {
        private readonly SqliteDb _db;

        public DeviceProtectionRepository(SqliteDb db) => _db = db;

        public async Task<HashSet<string>> GetProtectedIdsAsync()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT deviceId FROM devices_protected;";
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                {
                    set.Add(reader.GetString(0));
                }
            }

            return set;
        }
    }

    public sealed class ExclusionsRepository
    {
        private readonly SqliteDb _db;

        public ExclusionsRepository(SqliteDb db) => _db = db;

        public async Task<IEnumerable<string>> GetAllAsync()
        {
            var list = new List<string>();
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pattern FROM exclusions;";
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                {
                    list.Add(reader.GetString(0));
                }
            }

            return list;
        }
    }

    public sealed class ProfileRepository
    {
        private readonly SqliteDb _db;

        public ProfileRepository(SqliteDb db) => _db = db;

        public async Task<IEnumerable<CopyProfile>> GetAllAsync()
        {
            var list = new List<CopyProfile>();
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT json FROM profiles;";
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var json = reader.GetString(0);
                try
                {
                    var profile = JsonSerializer.Deserialize<CopyProfile>(json);
                    if (profile != null)
                    {
                        list.Add(profile);
                    }
                }
                catch
                {
                    // ignore invalid payloads
                }
            }

            return list;
        }

        public async Task<CopyProfile?> GetByIdAsync(string profileId)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT json FROM profiles WHERE profileId = @profileId LIMIT 1;";
            command.Parameters.AddWithValue("@profileId", profileId);
            var value = await command.ExecuteScalarAsync().ConfigureAwait(false) as string;
            if (value is null)
            {
                return null;
            }

            return JsonSerializer.Deserialize<CopyProfile>(value);
        }

        public async Task SaveAsync(CopyProfile profile)
        {
            var json = JsonSerializer.Serialize(profile);
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO profiles(profileId, json) VALUES(@profileId, @json);";
            command.Parameters.AddWithValue("@profileId", profile.ProfileId);
            command.Parameters.AddWithValue("@json", json);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task DeleteAsync(string profileId)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM profiles WHERE profileId = @profileId;";
            command.Parameters.AddWithValue("@profileId", profileId);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public sealed class JobsRepository
    {
        private readonly SqliteDb _db;

        public JobsRepository(SqliteDb db) => _db = db;

        public async Task UpsertAsync(TransferJob job)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT OR REPLACE INTO transfer_jobs(
    jobId, startedAt, endedAt, operatorName, sourcePath, profileId, status, bytesPlanned, bytesOk, bytesFailed
) VALUES(
    @jobId, @startedAt, @endedAt, @operatorName, @sourcePath, @profileId, @status, @bytesPlanned, @bytesOk, @bytesFailed
);";
            command.Parameters.AddWithValue("@jobId", job.JobId.ToString());
            command.Parameters.AddWithValue("@startedAt", job.StartedAt.ToString("o"));
            command.Parameters.AddWithValue("@endedAt", job.EndedAt?.ToString("o"));
            command.Parameters.AddWithValue("@operatorName", job.OperatorName);
            command.Parameters.AddWithValue("@sourcePath", job.SourcePath);
            command.Parameters.AddWithValue("@profileId", job.ProfileId);
            command.Parameters.AddWithValue("@status", job.Status.ToString());
            command.Parameters.AddWithValue("@bytesPlanned", job.BytesPlanned);
            command.Parameters.AddWithValue("@bytesOk", job.BytesOk);
            command.Parameters.AddWithValue("@bytesFailed", job.BytesFailed);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<TransferJob>> GetAllAsync()
        {
            var list = new List<TransferJob>();
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT jobId, startedAt, endedAt, operatorName, sourcePath, profileId, status, bytesPlanned, bytesOk, bytesFailed FROM transfer_jobs;";
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var job = new TransferJob
                {
                    JobId = Guid.Parse(reader.GetString(0)),
                    StartedAt = DateTime.Parse(reader.GetString(1)),
                    EndedAt = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)),
                    OperatorName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    SourcePath = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    ProfileId = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Status = Enum.TryParse<JobStatus>(reader.GetString(6), out var status) ? status : JobStatus.Pending,
                    BytesPlanned = reader.IsDBNull(7) ? 0 : reader.GetInt64(7),
                    BytesOk = reader.IsDBNull(8) ? 0 : reader.GetInt64(8),
                    BytesFailed = reader.IsDBNull(9) ? 0 : reader.GetInt64(9)
                };
                list.Add(job);
            }

            return list;
        }

        public async Task<TransferJob?> GetByIdAsync(Guid jobId)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT startedAt, endedAt, operatorName, sourcePath, profileId, status, bytesPlanned, bytesOk, bytesFailed FROM transfer_jobs WHERE jobId = @jobId LIMIT 1;";
            command.Parameters.AddWithValue("@jobId", jobId.ToString());
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return new TransferJob
            {
                JobId = jobId,
                StartedAt = DateTime.Parse(reader.GetString(0)),
                EndedAt = reader.IsDBNull(1) ? null : DateTime.Parse(reader.GetString(1)),
                OperatorName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                SourcePath = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                ProfileId = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Status = Enum.TryParse<JobStatus>(reader.GetString(5), out var status) ? status : JobStatus.Pending,
                BytesPlanned = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                BytesOk = reader.IsDBNull(7) ? 0 : reader.GetInt64(7),
                BytesFailed = reader.IsDBNull(8) ? 0 : reader.GetInt64(8)
            };
        }
    }

    public sealed class TargetsRepository
    {
        private readonly SqliteDb _db;

        public TargetsRepository(SqliteDb db) => _db = db;

        public async Task UpsertAsync(TransferTarget target)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT OR REPLACE INTO transfer_targets(
    jobId, deviceId, targetRootPath, status, bytesOk, bytesFailed, currentMBps, maxMBps, avgMBps, queueCount, elapsedMs, etaMs, miniSimple, miniOpacity, miniDockSide, miniTopMargin
) VALUES(
    @jobId, @deviceId, @targetRootPath, @status, @bytesOk, @bytesFailed, @currentMBps, @maxMBps, @avgMBps, @queueCount, @elapsedMs, @etaMs, @miniSimple, @miniOpacity, @miniDockSide, @miniTopMargin
);";
            command.Parameters.AddWithValue("@jobId", target.JobId.ToString());
            command.Parameters.AddWithValue("@deviceId", target.DeviceId);
            command.Parameters.AddWithValue("@targetRootPath", target.TargetRootPath);
            command.Parameters.AddWithValue("@status", target.Status.ToString());
            command.Parameters.AddWithValue("@bytesOk", target.BytesOk);
            command.Parameters.AddWithValue("@bytesFailed", target.BytesFailed);
            command.Parameters.AddWithValue("@currentMBps", target.CurrentMBps);
            command.Parameters.AddWithValue("@maxMBps", target.MaxMBps);
            command.Parameters.AddWithValue("@avgMBps", target.AvgMBps);
            command.Parameters.AddWithValue("@queueCount", target.QueueCount);
            command.Parameters.AddWithValue("@elapsedMs", target.Elapsed.Ticks / TimeSpan.TicksPerMillisecond);
            command.Parameters.AddWithValue("@etaMs", target.ETA?.Ticks / TimeSpan.TicksPerMillisecond);
            command.Parameters.AddWithValue("@miniSimple", target.MiniSimpleMode ? 1 : 0);
            command.Parameters.AddWithValue("@miniOpacity", target.MiniOpacityPercent);
            command.Parameters.AddWithValue("@miniDockSide", target.MiniDockSide);
            command.Parameters.AddWithValue("@miniTopMargin", target.MiniTopMargin);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<TransferTarget>> GetByJobAsync(Guid jobId)
        {
            var list = new List<TransferTarget>();
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT deviceId, targetRootPath, status, bytesOk, bytesFailed, currentMBps, maxMBps, avgMBps, queueCount, elapsedMs, etaMs, miniSimple, miniOpacity, miniDockSide, miniTopMargin FROM transfer_targets WHERE jobId = @jobId;";
            command.Parameters.AddWithValue("@jobId", jobId.ToString());
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var target = new TransferTarget
                {
                    JobId = jobId,
                    DeviceId = reader.GetString(0),
                    TargetRootPath = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Status = Enum.TryParse<JobStatus>(reader.GetString(2), out var status) ? status : JobStatus.Pending,
                    BytesOk = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                    BytesFailed = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                    CurrentMBps = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                    MaxMBps = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                    AvgMBps = reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
                    QueueCount = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                    Elapsed = reader.IsDBNull(9) ? TimeSpan.Zero : TimeSpan.FromMilliseconds(reader.GetInt64(9)),
                    ETA = reader.IsDBNull(10) ? null : TimeSpan.FromMilliseconds(reader.GetInt64(10)),
                    MiniSimpleMode = reader.IsDBNull(11) ? false : reader.GetInt32(11) == 1,
                    MiniOpacityPercent = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                    MiniDockSide = reader.IsDBNull(13) ? "Left" : reader.GetString(13),
                    MiniTopMargin = reader.IsDBNull(14) ? 0 : reader.GetInt32(14)
                };
                list.Add(target);
            }

            return list;
        }
    }

    public sealed class ItemsRepository
    {
        private readonly SqliteDb _db;

        public ItemsRepository(SqliteDb db) => _db = db;

        public async Task AddAsync(TransferItemLog item)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO transfer_items(id, jobId, deviceId, action, sourcePath, destinationPath, sizeBytes, extension, status, ts)
VALUES(@id, @jobId, @deviceId, @action, @sourcePath, @destinationPath, @sizeBytes, @extension, @status, @ts);";
            command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("@jobId", item.JobId.ToString());
            command.Parameters.AddWithValue("@deviceId", item.DeviceId);
            command.Parameters.AddWithValue("@action", item.Action);
            command.Parameters.AddWithValue("@sourcePath", item.Source);
            command.Parameters.AddWithValue("@destinationPath", item.Destination);
            command.Parameters.AddWithValue("@sizeBytes", item.SizeBytes);
            command.Parameters.AddWithValue("@extension", item.Extension);
            command.Parameters.AddWithValue("@status", item.Status);
            command.Parameters.AddWithValue("@ts", item.Ts.ToString("o"));
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<TransferItemLog>> GetByJobAsync(Guid jobId)
        {
            var list = new List<TransferItemLog>();
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT deviceId, action, sourcePath, destinationPath, sizeBytes, extension, status, ts FROM transfer_items WHERE jobId = @jobId;";
            command.Parameters.AddWithValue("@jobId", jobId.ToString());
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(new TransferItemLog
                {
                    JobId = jobId,
                    DeviceId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    Action = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Source = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Destination = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    SizeBytes = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                    Extension = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Status = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Ts = reader.IsDBNull(7) ? DateTime.MinValue : DateTime.Parse(reader.GetString(7))
                });
            }

            return list;
        }
    }

    public sealed class ValidationRepository
    {
        private readonly SqliteDb _db;

        public ValidationRepository(SqliteDb db) => _db = db;

        public async Task AddAsync(ValidationLog log)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO validations(id, jobId, rule, result, details, ts)
VALUES(@id, @jobId, @rule, @result, @details, @ts);";
            command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("@jobId", log.JobId.ToString());
            command.Parameters.AddWithValue("@rule", log.Rule);
            command.Parameters.AddWithValue("@result", log.Result);
            command.Parameters.AddWithValue("@details", log.Details);
            command.Parameters.AddWithValue("@ts", log.Ts.ToString("o"));
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<ValidationLog>> GetByJobAsync(Guid jobId)
        {
            var list = new List<ValidationLog>();
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT rule, result, details, ts FROM validations WHERE jobId = @jobId;";
            command.Parameters.AddWithValue("@jobId", jobId.ToString());
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(new ValidationLog
                {
                    JobId = jobId,
                    Rule = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    Result = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Details = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Ts = reader.IsDBNull(3) ? DateTime.MinValue : DateTime.Parse(reader.GetString(3))
                });
            }

            return list;
        }
    }

    public sealed class ErrorRepository
    {
        private readonly SqliteDb _db;

        public ErrorRepository(SqliteDb db) => _db = db;

        public async Task AddAsync(ErrorLog log)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO errors(id, jobId, deviceId, filePath, errorCode, message, ts)
VALUES(@id, @jobId, @deviceId, @filePath, @errorCode, @message, @ts);";
            command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("@jobId", log.JobId.ToString());
            command.Parameters.AddWithValue("@deviceId", log.DeviceId);
            command.Parameters.AddWithValue("@filePath", log.FilePath);
            command.Parameters.AddWithValue("@errorCode", log.ErrorCode);
            command.Parameters.AddWithValue("@message", log.Message);
            command.Parameters.AddWithValue("@ts", log.Ts.ToString("o"));
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<ErrorLog>> GetByJobAsync(Guid jobId)
        {
            var list = new List<ErrorLog>();
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT deviceId, filePath, errorCode, message, ts FROM errors WHERE jobId = @jobId;";
            command.Parameters.AddWithValue("@jobId", jobId.ToString());
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(new ErrorLog
                {
                    JobId = jobId,
                    DeviceId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    FilePath = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    ErrorCode = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Message = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Ts = reader.IsDBNull(4) ? DateTime.MinValue : DateTime.Parse(reader.GetString(4))
                });
            }

            return list;
        }
    }

    public sealed class RamStatsRepository
    {
        private readonly SqliteDb _db;

        public RamStatsRepository(SqliteDb db) => _db = db;

        public async Task AddAsync(RamBufferStats stats)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO ram_stats(id, jobId, bytesBuffered, queueDepth, throughputMBps, ts)
VALUES(@id, @jobId, @bytesBuffered, @queueDepth, @throughputMBps, @ts);";
            command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("@jobId", stats.JobId.ToString());
            command.Parameters.AddWithValue("@bytesBuffered", stats.BytesBuffered);
            command.Parameters.AddWithValue("@queueDepth", stats.QueueDepth);
            command.Parameters.AddWithValue("@throughputMBps", stats.ThroughputMBps);
            command.Parameters.AddWithValue("@ts", stats.Ts.ToString("o"));
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<RamBufferStats>> GetByJobAsync(Guid jobId)
        {
            var list = new List<RamBufferStats>();
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT bytesBuffered, queueDepth, throughputMBps, ts FROM ram_stats WHERE jobId = @jobId;";
            command.Parameters.AddWithValue("@jobId", jobId.ToString());
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(new RamBufferStats
                {
                    JobId = jobId,
                    BytesBuffered = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                    QueueDepth = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    ThroughputMBps = reader.IsDBNull(2) ? 0 : reader.GetDouble(2),
                    Ts = reader.IsDBNull(3) ? DateTime.MinValue : DateTime.Parse(reader.GetString(3))
                });
            }

            return list;
        }
    }

    public sealed class SalesRepository
    {
        private readonly SqliteDb _db;

        public SalesRepository(SqliteDb db) => _db = db;

        public async Task AddAsync(Sale sale)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO sales(saleId, jobId, ts, operatorName, customerName, currency, amountExpected, amountPaid, paymentMethod, status, notes, realGb, billableGb, roundMode)
VALUES(@saleId, @jobId, @ts, @operatorName, @customerName, @currency, @amountExpected, @amountPaid, @paymentMethod, @status, @notes, @realGb, @billableGb, @roundMode);";
            command.Parameters.AddWithValue("@saleId", sale.SaleId.ToString());
            command.Parameters.AddWithValue("@jobId", sale.JobId.ToString());
            command.Parameters.AddWithValue("@ts", sale.Ts.ToString("o"));
            command.Parameters.AddWithValue("@operatorName", sale.OperatorName);
            command.Parameters.AddWithValue("@customerName", sale.CustomerName);
            command.Parameters.AddWithValue("@currency", sale.Currency);
            command.Parameters.AddWithValue("@amountExpected", sale.AmountExpected);
            command.Parameters.AddWithValue("@amountPaid", sale.AmountPaid);
            command.Parameters.AddWithValue("@paymentMethod", sale.PaymentMethod);
            command.Parameters.AddWithValue("@status", sale.Status.ToString());
            command.Parameters.AddWithValue("@notes", sale.Notes);
            command.Parameters.AddWithValue("@realGb", sale.RealGb);
            command.Parameters.AddWithValue("@billableGb", sale.BillableGb);
            command.Parameters.AddWithValue("@roundMode", sale.RoundMode.ToString());
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task UpdateAsync(Sale sale)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT OR REPLACE INTO sales(saleId, jobId, ts, operatorName, customerName, currency, amountExpected, amountPaid, paymentMethod, status, notes, realGb, billableGb, roundMode)
VALUES(@saleId, @jobId, @ts, @operatorName, @customerName, @currency, @amountExpected, @amountPaid, @paymentMethod, @status, @notes, @realGb, @billableGb, @roundMode);";
            command.Parameters.AddWithValue("@saleId", sale.SaleId.ToString());
            command.Parameters.AddWithValue("@jobId", sale.JobId.ToString());
            command.Parameters.AddWithValue("@ts", sale.Ts.ToString("o"));
            command.Parameters.AddWithValue("@operatorName", sale.OperatorName);
            command.Parameters.AddWithValue("@customerName", sale.CustomerName);
            command.Parameters.AddWithValue("@currency", sale.Currency);
            command.Parameters.AddWithValue("@amountExpected", sale.AmountExpected);
            command.Parameters.AddWithValue("@amountPaid", sale.AmountPaid);
            command.Parameters.AddWithValue("@paymentMethod", sale.PaymentMethod);
            command.Parameters.AddWithValue("@status", sale.Status.ToString());
            command.Parameters.AddWithValue("@notes", sale.Notes);
            command.Parameters.AddWithValue("@realGb", sale.RealGb);
            command.Parameters.AddWithValue("@billableGb", sale.BillableGb);
            command.Parameters.AddWithValue("@roundMode", sale.RoundMode.ToString());
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<Sale>> QueryAsync(DateTime from, DateTime to)
        {
            var list = new List<Sale>();
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT saleId, jobId, ts, operatorName, customerName, currency, amountExpected, amountPaid, paymentMethod, status, notes, realGb, billableGb, roundMode FROM sales WHERE ts BETWEEN @from AND @to;";
            command.Parameters.AddWithValue("@from", from.ToString("o"));
            command.Parameters.AddWithValue("@to", to.ToString("o"));
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var sale = new Sale
                {
                    SaleId = Guid.Parse(reader.GetString(0)),
                    JobId = Guid.Parse(reader.GetString(1)),
                    Ts = DateTime.Parse(reader.GetString(2)),
                    OperatorName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    CustomerName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Currency = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    AmountExpected = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                    AmountPaid = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                    PaymentMethod = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    Status = Enum.TryParse<SaleStatus>(reader.GetString(9), out var status) ? status : SaleStatus.Pending,
                    Notes = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    RealGb = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11),
                    BillableGb = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),
                    RoundMode = reader.IsDBNull(13) ? RoundMode.CeilGB : Enum.TryParse<RoundMode>(reader.GetString(13), out var mode) ? mode : RoundMode.CeilGB
                };
                list.Add(sale);
            }

            return list;
        }

        public async Task<Sale?> GetByIdAsync(Guid saleId)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT jobId, ts, operatorName, customerName, currency, amountExpected, amountPaid, paymentMethod, status, notes, realGb, billableGb, roundMode FROM sales WHERE saleId = @saleId LIMIT 1;";
            command.Parameters.AddWithValue("@saleId", saleId.ToString());
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return new Sale
            {
                SaleId = saleId,
                JobId = Guid.Parse(reader.GetString(0)),
                Ts = DateTime.Parse(reader.GetString(1)),
                OperatorName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                CustomerName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Currency = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                AmountExpected = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                AmountPaid = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                PaymentMethod = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                Status = Enum.TryParse<SaleStatus>(reader.GetString(8), out var status) ? status : SaleStatus.Pending,
                Notes = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                RealGb = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10),
                BillableGb = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11),
                RoundMode = reader.IsDBNull(12) ? RoundMode.CeilGB : Enum.TryParse<RoundMode>(reader.GetString(12), out var mode) ? mode : RoundMode.CeilGB
            };
        }

        public async Task<Sale?> GetByJobAsync(Guid jobId)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT saleId, ts, operatorName, customerName, currency, amountExpected, amountPaid, paymentMethod, status, notes, realGb, billableGb, roundMode FROM sales WHERE jobId = @jobId LIMIT 1;";
            command.Parameters.AddWithValue("@jobId", jobId.ToString());
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return new Sale
            {
                SaleId = Guid.Parse(reader.GetString(0)),
                JobId = jobId,
                Ts = DateTime.Parse(reader.GetString(1)),
                OperatorName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                CustomerName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Currency = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                AmountExpected = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                AmountPaid = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                PaymentMethod = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                Status = Enum.TryParse<SaleStatus>(reader.GetString(8), out var status) ? status : SaleStatus.Pending,
                Notes = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                RealGb = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10),
                BillableGb = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11),
                RoundMode = reader.IsDBNull(12) ? RoundMode.CeilGB : Enum.TryParse<RoundMode>(reader.GetString(12), out var mode) ? mode : RoundMode.CeilGB
            };
        }
    }

    public sealed class CashRegisterRepository
    {
        private readonly SqliteDb _db;

        public CashRegisterRepository(SqliteDb db) => _db = db;

        public async Task UpsertAsync(CashRegister register)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT OR REPLACE INTO cash_register(day, openingAmount, closingAmount, countedAmount, expectedAmount, notes)
VALUES(@day, @openingAmount, @closingAmount, @countedAmount, @expectedAmount, @notes);";
            command.Parameters.AddWithValue("@day", register.Day.ToString("o"));
            command.Parameters.AddWithValue("@openingAmount", register.OpeningAmount);
            command.Parameters.AddWithValue("@closingAmount", register.ClosingAmount);
            command.Parameters.AddWithValue("@countedAmount", register.CountedAmount);
            command.Parameters.AddWithValue("@expectedAmount", register.ExpectedAmount);
            command.Parameters.AddWithValue("@notes", register.Notes);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<CashRegister?> GetByDayAsync(DateOnly day)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT openingAmount, closingAmount, countedAmount, expectedAmount, notes FROM cash_register WHERE day = @day LIMIT 1;";
            command.Parameters.AddWithValue("@day", day.ToString("o"));
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return new CashRegister
            {
                Day = day,
                OpeningAmount = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0),
                ClosingAmount = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1),
                CountedAmount = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                ExpectedAmount = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                Notes = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
            };
        }
    }

    public sealed class EventsRepository
    {
        private readonly SqliteDb _db;

        public EventsRepository(SqliteDb db) => _db = db;

        public async Task AddAsync(AppEvent evt)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT OR REPLACE INTO events(eventId, ts, type, severity, message, jobId, deviceId, saleId)
VALUES(@eventId, @ts, @type, @severity, @message, @jobId, @deviceId, @saleId);";
            command.Parameters.AddWithValue("@eventId", evt.EventId.ToString());
            command.Parameters.AddWithValue("@ts", evt.Ts.ToString("o"));
            command.Parameters.AddWithValue("@type", evt.Type);
            command.Parameters.AddWithValue("@severity", evt.Severity.ToString());
            command.Parameters.AddWithValue("@message", evt.Message);
            command.Parameters.AddWithValue("@jobId", evt.JobId?.ToString());
            command.Parameters.AddWithValue("@deviceId", evt.DeviceId);
            command.Parameters.AddWithValue("@saleId", evt.SaleId?.ToString());
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<AppEvent>> QueryAsync(DateTime from, DateTime to, string? type = null, EventSeverity? severity = null)
        {
            var list = new List<AppEvent>();
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            var query = "SELECT eventId, ts, type, severity, message, jobId, deviceId, saleId FROM events WHERE ts BETWEEN @from AND @to";
            if (!string.IsNullOrWhiteSpace(type))
            {
                query += " AND type = @type";
                command.Parameters.AddWithValue("@type", type);
            }

            if (severity != null)
            {
                query += " AND severity = @severity";
                command.Parameters.AddWithValue("@severity", severity.Value.ToString());
            }

            query += " ORDER BY ts DESC;";
            command.CommandText = query;
            command.Parameters.AddWithValue("@from", from.ToString("o"));
            command.Parameters.AddWithValue("@to", to.ToString("o"));
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var eventId = Guid.Parse(reader.GetString(0));
                var evt = new AppEvent(
                    eventId,
                    DateTime.Parse(reader.GetString(1)),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Enum.TryParse<EventSeverity>(reader.GetString(3), out var sev) ? sev : EventSeverity.Info,
                    reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    reader.IsDBNull(5) ? null : Guid.Parse(reader.GetString(5)),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : Guid.Parse(reader.GetString(7)));
                list.Add(evt);
            }

            return list;
        }
    }


    public sealed class AlertsRepository
    {
        private readonly SqliteDb _db;

        public AlertsRepository(SqliteDb db) => _db = db;

        public async Task SaveResolvedAsync(AlertRecord alert)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT OR REPLACE INTO alerts_resolved(alertId, message, severity, jobId, deviceId, raisedAt, resolvedAt)
VALUES(@alertId, @message, @severity, @jobId, @deviceId, @raisedAt, @resolvedAt);";
            command.Parameters.AddWithValue("@alertId", alert.AlertId.ToString());
            command.Parameters.AddWithValue("@message", alert.Message);
            command.Parameters.AddWithValue("@severity", alert.Severity.ToString());
            command.Parameters.AddWithValue("@jobId", alert.JobId?.ToString());
            command.Parameters.AddWithValue("@deviceId", alert.DeviceId);
            command.Parameters.AddWithValue("@raisedAt", alert.RaisedAt.ToString("o"));
            command.Parameters.AddWithValue("@resolvedAt", alert.ResolvedAt?.ToString("o"));
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<AlertRecord>> GetResolvedAsync()
        {
            var list = new List<AlertRecord>();
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT alertId, message, severity, jobId, deviceId, raisedAt, resolvedAt FROM alerts_resolved ORDER BY resolvedAt DESC;";
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var alert = new AlertRecord(
                    Guid.Parse(reader.GetString(0)),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Enum.TryParse<EventSeverity>(reader.GetString(2), out var severity) ? severity : EventSeverity.Info,
                    reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    DateTime.Parse(reader.GetString(5)),
                    reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)));
                list.Add(alert);
            }

            return list;
        }
    }

    public sealed class PricingTiersRepository
    {
        private readonly SqliteDb _db;

        public PricingTiersRepository(SqliteDb db) => _db = db;

        public async Task<IReadOnlyList<PricingTier>> GetAllAsync()
        {
            var list = new List<PricingTier>();
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT tierId, minGb, maxGb, priceCup, isActive, sortOrder FROM pricing_tiers ORDER BY sortOrder ASC, minGb ASC;";
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(new PricingTier
                {
                    TierId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    MinGB = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    MaxGB = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    PriceCUP = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    IsActive = !reader.IsDBNull(4) && reader.GetInt32(4) == 1,
                    Order = reader.IsDBNull(5) ? 0 : reader.GetInt32(5)
                });
            }

            return list;
        }

        public async Task ReplaceAllAsync(IReadOnlyList<PricingTier> tiers)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var tx = await connection.BeginTransactionAsync().ConfigureAwait(false);
            await using (var clear = connection.CreateCommand())
            {
                clear.CommandText = "DELETE FROM pricing_tiers;";
                await clear.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await using var insert = connection.CreateCommand();
            insert.CommandText = @"INSERT INTO pricing_tiers(tierId, minGb, maxGb, priceCup, isActive, sortOrder)
VALUES(@tierId, @minGb, @maxGb, @priceCup, @isActive, @sortOrder);";

            foreach (var tier in tiers)
            {
                insert.Parameters.Clear();
                insert.Parameters.AddWithValue("@tierId", tier.TierId);
                insert.Parameters.AddWithValue("@minGb", tier.MinGB);
                insert.Parameters.AddWithValue("@maxGb", tier.MaxGB);
                insert.Parameters.AddWithValue("@priceCup", tier.PriceCUP);
                insert.Parameters.AddWithValue("@isActive", tier.IsActive ? 1 : 0);
                insert.Parameters.AddWithValue("@sortOrder", tier.Order);
                await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await tx.CommitAsync().ConfigureAwait(false);
        }
    }


    public sealed class SessionsRepository
    {
        private readonly SqliteDb _db;

        public SessionsRepository(SqliteDb db) => _db = db;

        public async Task UpsertAsync(WorkSession session)
        {
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT OR REPLACE INTO sessions(sessionId, name, start, end, operatorName, notes)
VALUES(@sessionId, @name, @start, @end, @operatorName, @notes);";
            command.Parameters.AddWithValue("@sessionId", session.SessionId.ToString());
            command.Parameters.AddWithValue("@name", session.Name);
            command.Parameters.AddWithValue("@start", session.Start.ToString("o"));
            command.Parameters.AddWithValue("@end", session.End?.ToString("o"));
            command.Parameters.AddWithValue("@operatorName", session.OperatorName);
            command.Parameters.AddWithValue("@notes", session.Notes);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<WorkSession>> GetAllAsync()
        {
            var list = new List<WorkSession>();
            await using var connection = await _db.CreateOpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT sessionId, name, start, end, operatorName, notes FROM sessions;";
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(new WorkSession
                {
                    SessionId = Guid.Parse(reader.GetString(0)),
                    Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Start = DateTime.Parse(reader.GetString(2)),
                    End = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                    OperatorName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Notes = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                });
            }

            return list;
        }
    }
}
