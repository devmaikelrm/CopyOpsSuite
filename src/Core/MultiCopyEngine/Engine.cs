using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CopyOpsSuite.AuditOps;
using CopyOpsSuite.Core.Models;
using CopyOpsSuite.Storage;
using CopyOpsSuite.System;
using System.IO.Compression;

namespace CopyOpsSuite.MultiCopyEngine
{
    internal sealed record BufferChunk(byte[] Data, int Count, long FileOffset, bool IsLastChunk);
    internal sealed record TransferFile(string SourcePath, string RelativePath, long SizeBytes);
    internal sealed record TargetFile(string SourcePath, string DestinationPath, string RelativePath, long SizeBytes);

    public enum TransferState
    {
        Idle,
        Reading,
        Writing,
        Encrypting,
        Compressing,
        Verifying,
        BufferWait,
        Paused,
        DoneOk,
        DoneError
    }

    public enum TransferHealth
    {
        Ok,
        Warn,
        Critical
    }

    public sealed record TargetSnapshot(
        string DeviceId,
        string DeviceLabel,
        string FileSystem,
        string BusHint,
        double CurrentMBps,
        double MaxMBps,
        double AvgMBps,
        int QueueCount,
        int ActiveWorkers,
        int WorkerSlots,
        double Progress,
        long BytesOk,
        long BytesPlanned,
        TimeSpan? Eta,
        TransferState State,
        TransferHealth Health,
        bool QueueWarning);

    public sealed record EngineProgress(Guid JobId, IReadOnlyList<TargetSnapshot> Targets);
    public sealed record AddFilesResult(int FileCount, long BytesAdded, IReadOnlyDictionary<string, long> BytesAddedByDevice);

    public sealed class Engine
    {
        private readonly Repositories _repositories;
        private readonly SettingsService _settings;
        private readonly AuditService _audit;
        private readonly AlertService _alerts;
        private readonly Verifier _verifier;
        private readonly Encryptor _encryptor;
        private readonly ManualResetEventSlim _pauseGate = new(true);
        private CancellationTokenSource? _internalCts;
        private readonly Dictionary<Guid, List<TargetContext>> _activeJobs = new();
        private readonly object _activeJobsLock = new();
        private readonly Dictionary<string, DateTime> _lastTargetPersisted = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastRamPersisted = new(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<EngineProgress>? ProgressUpdated;

        public Engine(Repositories repositories, SettingsService settings, AuditService audit, AlertService alerts, Verifier verifier, Encryptor encryptor)
        {
            _repositories = repositories ?? throw new ArgumentNullException(nameof(repositories));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
            _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
            _encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
        }

        public bool IsPaused => !_pauseGate.IsSet;

        public void SetPaused(bool paused)
        {
            if (paused)
            {
                _pauseGate.Reset();
            }
            else
            {
                _pauseGate.Set();
            }
        }

        public async Task RunAsync(TransferJob job, IReadOnlyList<DeviceInfo> devices, CancellationToken cancellationToken = default)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            if (devices == null || devices.Count == 0) throw new InvalidOperationException("Debe haber al menos un destino.");

            var files = await GatherFilesAsync(job, cancellationToken).ConfigureAwait(false);
            if (files.Count == 0)
            {
                job.Status = JobStatus.Completed;
                job.EndedAt = DateTime.UtcNow;
                await _repositories.Jobs.UpsertAsync(job).ConfigureAwait(false);
                return;
            }

            var contexts = devices.Select(device => new TargetContext(device, job.JobId, _settings.BufferSettings, _pauseGate)).ToList();
            foreach (var context in contexts)
            {
                context.EnqueueFiles(files, _settings.EncryptionSettings.EncryptionEnabled);
            }

            job.StartedAt = DateTime.UtcNow;
            job.Status = JobStatus.Running;
            job.BytesPlanned = contexts.Sum(c => c.BytesPlanned);
            job.BytesOk = 0;
            job.BytesFailed = 0;
            await _repositories.Jobs.UpsertAsync(job).ConfigureAwait(false);
            _audit.RecordEvent("TRANSFER", $"Job {job.JobId} iniciado", EventSeverity.Info, job.JobId);
            _audit.RecordEvent(
                _settings.BufferSettings.EnableBuffering ? "BUFFER_ENABLED" : "BUFFER_DISABLED",
                _settings.BufferSettings.EnableBuffering ? "Buffering habilitado" : "Buffering deshabilitado",
                EventSeverity.Info,
                job.JobId);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _internalCts = linkedCts;
            lock (_activeJobsLock)
            {
                _activeJobs[job.JobId] = contexts;
            }
            var tasks = contexts.Select(ctx => ctx.RunAsync(CopyFileAsync, linkedCts.Token, ReportTargetProgress)).ToArray();
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
                job.Status = contexts.Any(ctx => ctx.HasErrors) ? JobStatus.Error : JobStatus.Completed;
            }
            catch (OperationCanceledException)
            {
                job.Status = JobStatus.Canceled;
                _audit.RecordEvent("TRANSFER", $"Job {job.JobId} cancelado", EventSeverity.Warn, job.JobId);
            }
            finally
            {
                _internalCts = null;
                lock (_activeJobsLock)
                {
                    _activeJobs.Remove(job.JobId);
                }
            }

            job.BytesOk = contexts.Sum(c => c.Target.BytesOk);
            job.BytesFailed = contexts.Sum(c => c.Target.BytesFailed);
            job.EndedAt = DateTime.UtcNow;
            await _repositories.Jobs.UpsertAsync(job).ConfigureAwait(false);
            await PersistTargetsAsync(contexts).ConfigureAwait(false);
            RaiseProgress(contexts, job.JobId);
        }

        public async Task<AddFilesResult> AddFilesToJobAsync(Guid jobId, IReadOnlyList<string> paths, IReadOnlyList<string> targetDeviceIds, CancellationToken cancellationToken = default)
        {
            List<TargetContext>? contexts;
            lock (_activeJobsLock)
            {
                _activeJobs.TryGetValue(jobId, out contexts);
            }

            if (contexts == null)
            {
                return new AddFilesResult(0, 0, new Dictionary<string, long>());
            }

            var job = await _repositories.Jobs.GetByIdAsync(jobId).ConfigureAwait(false);
            if (job == null)
            {
                return new AddFilesResult(0, 0, new Dictionary<string, long>());
            }

            var root = Path.GetFullPath(job.SourcePath);
            var files = await BuildTransferFilesAsync(root, paths, jobId, cancellationToken).ConfigureAwait(false);
            if (files.Count == 0)
            {
                return new AddFilesResult(0, 0, new Dictionary<string, long>());
            }

            var targetSet = new HashSet<string>(targetDeviceIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var targets = contexts
                .Where(ctx => targetSet.Contains(ctx.DeviceId))
                .ToList();

            if (targets.Count == 0)
            {
                return new AddFilesResult(0, 0, new Dictionary<string, long>());
            }

            var bytesByDevice = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var encryptionEnabled = _settings.EncryptionSettings.EncryptionEnabled;
            foreach (var target in targets)
            {
                var (count, bytes) = target.EnqueueFiles(files, encryptionEnabled);
                if (count > 0)
                {
                    bytesByDevice[target.DeviceId] = bytes;
                }
            }

            var bytesAdded = bytesByDevice.Values.Sum();
            if (bytesAdded > 0)
            {
                job.BytesPlanned += bytesAdded;
                await _repositories.Jobs.UpsertAsync(job).ConfigureAwait(false);
                await PersistTargetsAsync(targets).ConfigureAwait(false);
                RaiseProgress(targets, job.JobId);
                _audit.RecordEvent("FILES_ADDED_TO_JOB", $"Se agregaron {files.Count} archivos ({bytesAdded:N0} bytes).", EventSeverity.Info, job.JobId);
            }

            return new AddFilesResult(files.Count, bytesAdded, bytesByDevice);
        }

        private async Task<List<TransferFile>> GatherFilesAsync(TransferJob job, CancellationToken cancellationToken)
        {
            var root = Path.GetFullPath(job.SourcePath);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Carpeta origen no encontrada.");

            var regexes = await BuildExclusionRegexesAsync().ConfigureAwait(false);

            var files = new List<TransferFile>();
            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(root, path);
                if (MatchesPattern(relative, regexes))
                {
                    await _repositories.Validations.AddAsync(new ValidationLog
                    {
                        JobId = job.JobId,
                        Rule = "Exclusion",
                        Result = "SKIPPED",
                        Details = relative,
                        Ts = DateTime.UtcNow
                    }).ConfigureAwait(false);
                    continue;
                }

                var info = new FileInfo(path);
                files.Add(new TransferFile(path, relative, info.Length));
            }

            return files;
        }

        private async Task<List<TransferFile>> BuildTransferFilesAsync(string root, IReadOnlyList<string> paths, Guid jobId, CancellationToken cancellationToken)
        {
            var regexes = await BuildExclusionRegexesAsync().ConfigureAwait(false);
            var files = new List<TransferFile>();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var expanded = await Task.Run(() => ExpandPaths(paths).ToList(), cancellationToken).ConfigureAwait(false);

            foreach (var path in expanded)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fullPath = Path.GetFullPath(path);
                if (!unique.Add(fullPath))
                {
                    continue;
                }

                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(root, fullPath);
                if (MatchesPattern(relative, regexes))
                {
                    await _repositories.Validations.AddAsync(new ValidationLog
                    {
                        JobId = jobId,
                        Rule = "Exclusion",
                        Result = "SKIPPED",
                        Details = relative,
                        Ts = DateTime.UtcNow
                    }).ConfigureAwait(false);
                    continue;
                }

                var info = new FileInfo(fullPath);
                files.Add(new TransferFile(fullPath, relative, info.Length));
            }

            return files;
        }

        private IEnumerable<string> ExpandPaths(IReadOnlyList<string> paths)
        {
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (Directory.Exists(path))
                {
                    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        yield return file;
                    }
                }
                else if (File.Exists(path))
                {
                    yield return path;
                }
            }
        }

        private static bool MatchesPattern(string relative, Regex[] regexes)
        {
            foreach (var regex in regexes)
            {
                if (regex.IsMatch(relative))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<Regex[]> BuildExclusionRegexesAsync()
        {
            var patterns = await _repositories.Exclusions.GetAllAsync().ConfigureAwait(false);
            return patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(pattern => new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                .ToArray();
        }

        private async Task<bool> CopyFileAsync(TargetContext context, TargetFile file, CancellationToken cancellationToken)
        {
            try
            {
                // Encryption Path
                if (_settings.EncryptionSettings.EncryptionEnabled)
                {
                    if (string.IsNullOrEmpty(_settings.UnlockedPin))
                    {
                        throw new InvalidOperationException("El cifrado está habilitado, pero el PIN de administrador no está desbloqueado.");
                    }
                    
                    context.SetState(TransferState.Encrypting);
                    using var sourceStream = new FileStream(file.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                    using var destinationStream = new FileStream(file.DestinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                    await _encryptor.EncryptStreamAsync(sourceStream, destinationStream, _settings.UnlockedPin).ConfigureAwait(false);
                    
                    // TODO: Implement verification for encrypted files. This would require decrypting the file to a temp stream.
                }
                // Standard Copy Path
                else
                {
                    if (_settings.BufferSettings.EnableBuffering)
                    {
                        var chunkSize = Math.Max(1 << 20, _settings.BufferSettings.ChunkSizeMb * 1024 * 1024);
                        var channel = Channel.CreateBounded<BufferChunk>(new BoundedChannelOptions(_settings.BufferSettings.MaxChunks)
                        {
                            AllowSynchronousContinuations = false,
                            SingleReader = true,
                            SingleWriter = true,
                            FullMode = BoundedChannelFullMode.Wait
                        });

                        var writerTask = WriteChunksAsync(context, file, channel.Reader, cancellationToken);
                        await ReadChunksAsync(context, file, chunkSize, channel.Writer, cancellationToken).ConfigureAwait(false);
                        channel.Writer.Complete();
                        await writerTask.ConfigureAwait(false);
                    }
                    else
                    {
                        await CopyWithoutBufferAsync(context, file, cancellationToken).ConfigureAwait(false);
                    }

                    context.SetState(TransferState.Verifying);
                    var verified = await _verifier.VerifyFileAsync(file.SourcePath, file.DestinationPath).ConfigureAwait(false);
                    if (!verified)
                    {
                        throw new IOException("La verificación del archivo falló (checksum no coincide).");
                    }
                }

                await _repositories.Items.AddAsync(new TransferItemLog
                {
                    JobId = context.JobId,
                    DeviceId = context.DeviceId,
                    Action = _settings.EncryptionSettings.EncryptionEnabled ? "ENCRYPT" : "COPY",
                    Source = file.SourcePath,
                    Destination = file.DestinationPath,
                    SizeBytes = file.SizeBytes,
                    Extension = Path.GetExtension(file.SourcePath) ?? string.Empty,
                    Status = "OK",
                    Ts = DateTime.UtcNow
                }).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                var firstFailure = context.MarkErrorOnce();
                await _repositories.Errors.AddAsync(new ErrorLog
                {
                    JobId = context.JobId,
                    DeviceId = context.DeviceId,
                    FilePath = file.SourcePath,
                    ErrorCode = ex.GetType().Name,
                    Message = ex.Message,
                    Ts = DateTime.UtcNow
                }).ConfigureAwait(false);

                await _repositories.Items.AddAsync(new TransferItemLog
                {
                    JobId = context.JobId,
                    DeviceId = context.DeviceId,
                    Action = "COPY",
                    Source = file.SourcePath,
                    Destination = file.DestinationPath,
                    SizeBytes = file.SizeBytes,
                    Extension = Path.GetExtension(file.SourcePath) ?? string.Empty,
                    Status = "FAILED",
                    Ts = DateTime.UtcNow
                }).ConfigureAwait(false);

                if (firstFailure)
                {
                    var message = $"Fallo en destino {context.DeviceId}: {ex.Message}";
                    _audit.RecordEvent("TARGET_FAILED", message, EventSeverity.Critical, context.JobId, context.DeviceId);
                    await _alerts.RaiseAlertAsync(message, EventSeverity.Critical, context.JobId, context.DeviceId).ConfigureAwait(false);
                }

                return false;
            }
        }

        private async Task ReadChunksAsync(TargetContext context, TargetFile file, int chunkSize, ChannelWriter<BufferChunk> writer, CancellationToken cancellationToken)
        {
            using var source = new FileStream(file.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize, useAsync: true);
            var offset = 0L;
            context.SetState(TransferState.Reading);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _pauseGate.Wait(cancellationToken);
                var buffer = new byte[chunkSize];
                var read = await source.ReadAsync(buffer, 0, chunkSize, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                offset += read;
                context.BufferReported(read, 1);
                await writer.WriteAsync(new BufferChunk(buffer, read, offset, read < chunkSize), cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task WriteChunksAsync(TargetContext context, TargetFile file, ChannelReader<BufferChunk> reader, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file.DestinationPath) ?? string.Empty);
            context.SetState(TransferState.Writing);

            using var destination = new FileStream(file.DestinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.TryRead(out var chunk))
                {
                    context.SetState(TransferState.BufferWait);
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                _pauseGate.Wait(cancellationToken);
                await destination.WriteAsync(chunk.Data, 0, chunk.Count, cancellationToken).ConfigureAwait(false);
                context.BufferReported(-chunk.Count, -1);
                context.UpdateSpeeds(chunk.Count);
            }
        }

        private async Task CopyWithoutBufferAsync(TargetContext context, TargetFile file, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file.DestinationPath) ?? string.Empty);
            context.SetState(TransferState.Reading);

            var buffer = new byte[1024 * 1024];
            await using var source = new FileStream(file.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, useAsync: true);
            await using var destination = new FileStream(file.DestinationPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true);

            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _pauseGate.Wait(cancellationToken);
                context.SetState(TransferState.Writing);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                context.UpdateSpeeds(read);
            }
        }

        private void ReportTargetProgress(TargetContext context)
        {
            var snapshot = context.BuildSnapshot();
            ProgressUpdated?.Invoke(this, new EngineProgress(context.JobId, new[] { snapshot }));
            if (snapshot.QueueWarning && !context.Usb2Alerted)
            {
                var message = $"Destino {snapshot.DeviceId} en USB 2.0 con cola activa.";
                _audit.RecordEvent("TARGET_SLOW_USB2", message, EventSeverity.Warn, context.JobId, snapshot.DeviceId);
                context.MarkUsb2Alerted();
            }
            _ = MaybePersistTargetAsync(context);
            _ = MaybePersistRamStatsAsync(context);
        }

        private void RaiseProgress(IEnumerable<TargetContext> contexts, Guid jobId)
        {
            var snapshots = contexts.Select(c => c.BuildSnapshot()).ToList();
            ProgressUpdated?.Invoke(this, new EngineProgress(jobId, snapshots));
        }

        private async Task PersistTargetsAsync(IEnumerable<TargetContext> contexts)
        {
            var settings = _settings.MiniWindowSettings;
            foreach (var context in contexts)
            {
                context.ApplyMiniSettings(settings);
                await _repositories.Targets.UpsertAsync(context.Target).ConfigureAwait(false);
            }
        }

        private async Task MaybePersistTargetAsync(TargetContext context)
        {
            var now = DateTime.UtcNow;
            if (_lastTargetPersisted.TryGetValue(context.DeviceId, out var last) && (now - last).TotalSeconds < 1)
            {
                return;
            }

            _lastTargetPersisted[context.DeviceId] = now;
            context.ApplyMiniSettings(_settings.MiniWindowSettings);
            await _repositories.Targets.UpsertAsync(context.Target).ConfigureAwait(false);
        }

        private async Task MaybePersistRamStatsAsync(TargetContext context)
        {
            var now = DateTime.UtcNow;
            if (_lastRamPersisted.TryGetValue(context.DeviceId, out var last) && (now - last).TotalSeconds < 2)
            {
                return;
            }

            _lastRamPersisted[context.DeviceId] = now;
            var stats = context.BuildRamStats();
            if (stats != null)
            {
                await _repositories.RamStats.AddAsync(stats).ConfigureAwait(false);
            }
        }
    }

    internal sealed class TargetContext
    {
        private readonly Channel<TargetFile> _queue = Channel.CreateUnbounded<TargetFile>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        private readonly BufferSettings _settings;
        private readonly ManualResetEventSlim _pauseGate;
        private readonly Stopwatch _stopwatch = new();
        private readonly object _queueLock = new();
        private long _plannedBytes;
        private int _pendingFiles;
        private int _bufferDepth;
        private long _bufferBytes;
        private double _currentSpeed;
        private double _maxSpeed;
        private readonly string _deviceLabel;
        private readonly string _fileSystem;
        private readonly string _busHint;
        private long _totalWritten;
        private int _activeWorkers;
        private TransferState _state;
        private bool _hasErrors;
        private bool _usb2Alerted;
        private bool _queueCompleted;

        public TransferTarget Target { get; }
        public Guid JobId { get; }
        public string DeviceId { get; }

        public int ActiveWorkers => _activeWorkers;
        public int WorkerSlots => Math.Max(1, _settings.MaxWriterWorkersPerTarget);
        public TransferState State => _state;
        public bool HasErrors => _hasErrors;
        public bool QueueWarning => Target.QueueCount > 0 && _busHint.Contains("USB 2", StringComparison.OrdinalIgnoreCase);
        public bool Usb2Alerted => _usb2Alerted;

        public TargetContext(DeviceInfo device, Guid jobId, BufferSettings settings, ManualResetEventSlim pauseGate)
        {
            DeviceId = device.Id;
            JobId = jobId;
            _settings = settings;
            _pauseGate = pauseGate;
            _deviceLabel = device.Label;
            _fileSystem = device.FileSystem;
            _busHint = device.BusHint;
            Target = new TransferTarget
            {
                JobId = jobId,
                DeviceId = device.Id,
                TargetRootPath = device.DriveLetter,
                Status = JobStatus.Pending
            };
            _state = TransferState.Idle;
        }

        public long BytesPlanned => Interlocked.Read(ref _plannedBytes);

        public (int FileCount, long BytesAdded) EnqueueFiles(IEnumerable<TransferFile> files, bool encryptionEnabled)
        {
            var count = 0;
            var bytes = 0L;
            lock (_queueLock)
            {
                if (_queueCompleted)
                {
                    return (0, 0);
                }

                foreach (var file in files)
                {
                    var destinationPath = encryptionEnabled
                        ? Path.Combine(Target.TargetRootPath, file.RelativePath + ".enc")
                        : Path.Combine(Target.TargetRootPath, file.RelativePath);

                    var targetFile = new TargetFile(file.SourcePath, destinationPath, file.RelativePath, file.SizeBytes);
                    if (_queue.Writer.TryWrite(targetFile))
                    {
                        Interlocked.Add(ref _plannedBytes, file.SizeBytes);
                        Interlocked.Increment(ref _pendingFiles);
                        bytes += file.SizeBytes;
                        count++;
                    }
                }
            }

            Target.QueueCount = Math.Max(0, _pendingFiles);
            return (count, bytes);
        }

        public void CompleteQueue()
        {
            lock (_queueLock)
            {
                if (_queueCompleted)
                {
                    return;
                }

                _queueCompleted = true;
                _queue.Writer.TryComplete();
            }
        }

        public void EnsureQueue(IEnumerable<TransferFile> files, bool encryptionEnabled)
        {
            foreach (var file in files)
            {
                var destinationPath = encryptionEnabled 
                    ? Path.Combine(Target.TargetRootPath, file.RelativePath + ".enc") 
                    : Path.Combine(Target.TargetRootPath, file.RelativePath);

                var targetFile = new TargetFile(file.SourcePath, destinationPath, file.RelativePath, file.SizeBytes);
                _queue.Writer.TryWrite(targetFile);
                Interlocked.Add(ref _plannedBytes, file.SizeBytes);
                Interlocked.Increment(ref _pendingFiles);
                Target.QueueCount = _pendingFiles;
            }
        }

        public async Task RunAsync(Func<TargetContext, TargetFile, CancellationToken, Task<bool>> handler, CancellationToken cancellationToken, Action<TargetContext> progressUpdater)
        {
            Target.Status = JobStatus.Running;
            var workers = Enumerable.Range(0, WorkerSlots)
                .Select(_ => Task.Run(() => WorkerLoop(handler, cancellationToken, progressUpdater), cancellationToken))
                .ToArray();

            await Task.WhenAll(workers).ConfigureAwait(false);
            Target.Status = _hasErrors ? JobStatus.Error : JobStatus.Completed;
            SetState(_hasErrors ? TransferState.DoneError : TransferState.DoneOk);
            progressUpdater(this);
        }

        private async Task WorkerLoop(Func<TargetContext, TargetFile, CancellationToken, Task<bool>> handler, CancellationToken cancellationToken, Action<TargetContext> progressUpdater)
        {
            while (await _queue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!_queue.Reader.TryRead(out var file))
                {
                    continue;
                }

                IncrementWorkers();
                SetState(TransferState.Reading);
                bool success = false;
                try
                {
                    success = await handler(this, file, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                finally
                {
                    DecrementWorkers();
                    RecordResult(success, file.SizeBytes);
                    progressUpdater(this);
                }
            }
        }

        public void RecordResult(bool success, long bytes)
        {
            if (success)
            {
                Target.BytesOk += bytes;
            }
            else
            {
                Target.BytesFailed += bytes;
                _hasErrors = true;
            }

            var pending = Interlocked.Decrement(ref _pendingFiles);
            Target.QueueCount = Math.Max(0, pending);
            if (pending == 0)
            {
                CompleteQueue();
            }
        }

        public void BufferReported(int bytes, int delta)
        {
            Interlocked.Add(ref _bufferDepth, delta);
            Interlocked.Add(ref _bufferBytes, bytes);
            if (delta < 0 && _bufferDepth <= 0)
            {
                SetState(TransferState.BufferWait);
            }
        }

        public void UpdateSpeeds(int bytes)
        {
            _stopwatch.Start();
            Interlocked.Add(ref _totalWritten, bytes);
            var elapsed = _stopwatch.Elapsed.TotalSeconds;
            if (elapsed < 0.5)
            {
                return;
            }

            var processed = Interlocked.Read(ref _totalWritten);
            var speed = processed / elapsed / (1024 * 1024);
            _currentSpeed = speed;
            _maxSpeed = Math.Max(_maxSpeed, speed);
            Target.CurrentMBps = _currentSpeed;
            Target.MaxMBps = _maxSpeed;
            Target.AvgMBps = processed / elapsed / (1024 * 1024);
            Target.Elapsed = _stopwatch.Elapsed;
            if (Target.CurrentMBps > 0 && BytesPlanned > 0)
            {
                var remaining = Math.Max(0, BytesPlanned - Target.BytesOk);
                var seconds = remaining / (Target.CurrentMBps * 1024 * 1024);
                Target.ETA = TimeSpan.FromSeconds(Math.Max(1, seconds));
            }
        }

        public void IncrementWorkers() => Interlocked.Increment(ref _activeWorkers);
        public void DecrementWorkers() => Interlocked.Decrement(ref _activeWorkers);

        public void SetState(TransferState state)
        {
            _state = state;
        }

        public void MarkError()
        {
            _hasErrors = true;
            SetState(TransferState.DoneError);
        }

        public bool MarkErrorOnce()
        {
            if (_hasErrors)
            {
                return false;
            }

            MarkError();
            return true;
        }

        public void MarkUsb2Alerted()
        {
            _usb2Alerted = true;
        }

        public void ApplyMiniSettings(MiniWindowSettings settings)
        {
            Target.MiniSimpleMode = settings.MiniSimpleDefault;
            Target.MiniOpacityPercent = settings.MiniOpacityPercent;
            Target.MiniDockSide = settings.MiniDockSide;
            Target.MiniTopMargin = settings.MiniTopMarginPx;
        }

        public RamBufferStats? BuildRamStats()
        {
            if (BytesPlanned == 0)
            {
                return null;
            }

            return new RamBufferStats
            {
                JobId = JobId,
                BytesBuffered = Interlocked.Read(ref _bufferBytes),
                QueueDepth = _bufferDepth,
                ThroughputMBps = Target.CurrentMBps,
                Ts = DateTime.UtcNow
            };
        }

        public TargetSnapshot BuildSnapshot()
        {
            var progress = BytesPlanned == 0 ? 0 : Math.Min(1.0, (double)Target.BytesOk / BytesPlanned);
            var health = _hasErrors
                ? TransferHealth.Critical
                : (_busHint.Contains("USB 2", StringComparison.OrdinalIgnoreCase) || Target.CurrentMBps < 5 ? TransferHealth.Warn : TransferHealth.Ok);
            return new TargetSnapshot(DeviceId, _deviceLabel, _fileSystem, _busHint, Target.CurrentMBps, Target.MaxMBps, Target.AvgMBps, Target.QueueCount, ActiveWorkers, WorkerSlots, progress, Target.BytesOk, BytesPlanned, Target.ETA, State, health, QueueWarning);
        }
    }
}
