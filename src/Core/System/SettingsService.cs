using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CopyOpsSuite.Core.Models;
using CopyOpsSuite.Storage;

namespace CopyOpsSuite.System
{
    public sealed class MiniWindowSettings
    {
        public bool MiniSimpleDefault { get; init; }
        public bool MiniAutoOpen { get; init; }
        public bool MiniAutoCloseNoErrors { get; init; }
        public bool MiniAutoCleanNoErrors { get; init; }
        public string MiniDockSide { get; init; } = string.Empty;
        public int MiniTopMarginPx { get; init; }
        public int MiniOpacityPercent { get; init; }
        public bool TopMostPin { get; init; }
    }

    public sealed class PerformanceSettings
    {
        public int MaxParallelTargets { get; init; }
        public int MaxFilesParallelPerTarget { get; init; }
    }

    public sealed class BufferSettings
    {
        public bool EnableBuffering { get; init; }
        public int ChunkSizeMb { get; init; }
        public int MaxChunks { get; init; }
        public int MaxWriterWorkersPerTarget { get; init; }
    }

    public sealed class AccessSettings
    {
        public bool OperatorModeEnabled { get; init; }
        public string PinHash { get; init; } = string.Empty;
        public string PinSalt { get; init; } = string.Empty;
    }
    
    public sealed class EncryptionSettings
    {
        public bool EncryptionEnabled { get; init; }
    }

    public sealed class CompressionSettings
    {
        public bool CompressionEnabled { get; init; }
    }

    public sealed class SimulationSettings
    {
        public double Usb2BaselineMbps { get; init; }
        public double Usb3BaselineMbps { get; init; }
        public double UnknownBaselineMbps { get; init; }
    }

    public sealed class SettingsService
    {
        private const string DefaultTheme = "Default";
        private const string DefaultPin = "1234";
        private const string DefaultSalt = "default";
        private static readonly string DefaultPinHash = HashPin(DefaultPin, DefaultSalt);
        private readonly SettingsRepository _repository;
        private bool _adminUnlocked;

        public MiniWindowSettings MiniWindowSettings { get; private set; } = new MiniWindowSettings();
        public PerformanceSettings PerformanceSettings { get; private set; } = new PerformanceSettings();
        public BufferSettings BufferSettings { get; private set; } = new BufferSettings();
        public AccessSettings AccessSettings { get; private set; } = new AccessSettings();
        public EncryptionSettings EncryptionSettings { get; private set; } = new EncryptionSettings();
        public CompressionSettings CompressionSettings { get; private set; } = new CompressionSettings();
        public SimulationSettings SimulationSettings { get; private set; } = new SimulationSettings();
        public BillingMode BillingMode { get; private set; } = BillingMode.PerJob;
        public string AppTheme { get; private set; } = DefaultTheme;

        public string? UnlockedPin { get; private set; }
        public bool IsAdminUnlocked => _adminUnlocked;
        public bool HasDefaultPin => AccessSettings.PinHash == DefaultPinHash && AccessSettings.PinSalt == DefaultSalt;

        public SettingsService(SettingsRepository repository)
        {
            _repository = repository;
            LoadAsync().GetAwaiter().GetResult();
        }

        public async Task LoadAsync()
        {
            await EnsurePinDefaultsAsync().ConfigureAwait(false);

            MiniWindowSettings = new MiniWindowSettings
            {
                MiniSimpleDefault = ParseBool(await _repository.GetAsync("mini_simple_mode_default").ConfigureAwait(false), false),
                MiniAutoOpen = ParseBool(await _repository.GetAsync("mini_auto_open").ConfigureAwait(false), true),
                MiniAutoCloseNoErrors = ParseBool(await _repository.GetAsync("mini_auto_close_no_errors").ConfigureAwait(false), true),
                MiniAutoCleanNoErrors = ParseBool(await _repository.GetAsync("mini_auto_clean_no_errors").ConfigureAwait(false), true),
                MiniDockSide = await _repository.GetAsync("mini_dock_side").ConfigureAwait(false) ?? "Left",
                MiniTopMarginPx = ParseInt(await _repository.GetAsync("mini_top_margin").ConfigureAwait(false), 80),
                MiniOpacityPercent = ParseInt(await _repository.GetAsync("mini_opacity_percent").ConfigureAwait(false), 90),
                TopMostPin = ParseBool(await _repository.GetAsync("mini_topmost_pin").ConfigureAwait(false), false)
            };

            PerformanceSettings = new PerformanceSettings
            {
                MaxParallelTargets = ParseInt(await _repository.GetAsync("max_parallel_targets").ConfigureAwait(false), 3),
                MaxFilesParallelPerTarget = ParseInt(await _repository.GetAsync("max_files_parallel_per_target").ConfigureAwait(false), 1)
            };

            BufferSettings = new BufferSettings
            {
                EnableBuffering = ParseBool(await _repository.GetAsync("buffer_enable").ConfigureAwait(false), true),
                ChunkSizeMb = ParseInt(await _repository.GetAsync("buffer_chunk_size_mb").ConfigureAwait(false), 4),
                MaxChunks = ParseInt(await _repository.GetAsync("buffer_max_chunks").ConfigureAwait(false), 32),
                MaxWriterWorkersPerTarget = ParseInt(await _repository.GetAsync("max_writer_workers_per_target").ConfigureAwait(false), 1)
            };

            AccessSettings = new AccessSettings
            {
                OperatorModeEnabled = ParseBool(await _repository.GetAsync("access_operator_mode").ConfigureAwait(false), true),
                PinHash = await _repository.GetAsync("admin_pin_hash").ConfigureAwait(false) ?? DefaultPinHash,
                PinSalt = await _repository.GetAsync("admin_pin_salt").ConfigureAwait(false) ?? DefaultSalt
            };

            EncryptionSettings = new EncryptionSettings
            {
                EncryptionEnabled = ParseBool(await _repository.GetAsync("encryption_enabled").ConfigureAwait(false), false)
            };

            CompressionSettings = new CompressionSettings
            {
                CompressionEnabled = ParseBool(await _repository.GetAsync("compression_enabled").ConfigureAwait(false), false)
            };

            SimulationSettings = new SimulationSettings
            {
                Usb2BaselineMbps = ParseDouble(await _repository.GetAsync("simulation_usb2_mb").ConfigureAwait(false), 20),
                Usb3BaselineMbps = ParseDouble(await _repository.GetAsync("simulation_usb3_mb").ConfigureAwait(false), 90),
                UnknownBaselineMbps = ParseDouble(await _repository.GetAsync("simulation_unknown_mb").ConfigureAwait(false), 50)
            };

            BillingMode = ParseBillingMode(await _repository.GetAsync("billing_mode").ConfigureAwait(false));
            AppTheme = (await _repository.GetAsync("app_theme").ConfigureAwait(false)) ?? DefaultTheme;
        }

        public async Task SetAsync(string key, string value)
        {
            await SaveValueAsync(key, value).ConfigureAwait(false);
            await LoadAsync().ConfigureAwait(false);
        }
        
        public async Task SetEncryptionAsync(bool enabled)
        {
            await SaveValueAsync("encryption_enabled", enabled ? "1" : "0").ConfigureAwait(false);
            await LoadAsync().ConfigureAwait(false);
        }

        public async Task SetCompressionAsync(bool enabled)
        {
            await SaveValueAsync("compression_enabled", enabled ? "1" : "0").ConfigureAwait(false);
            await LoadAsync().ConfigureAwait(false);
        }

        public async Task SetOperatorModeAsync(bool enabled)
        {
            await SaveValueAsync("access_operator_mode", enabled ? "1" : "0").ConfigureAwait(false);
            await LoadAsync().ConfigureAwait(false);
        }

        public async Task SetAdminPinAsync(string pin)
        {
            var salt = GenerateSalt();
            await SaveValueAsync("admin_pin_hash", HashPin(pin, salt)).ConfigureAwait(false);
            await SaveValueAsync("admin_pin_salt", salt).ConfigureAwait(false);
            await LoadAsync().ConfigureAwait(false);
        }

        public async Task SetBufferingAsync(bool enabled, int chunkSizeMb, int maxChunks, int workers)
        {
            await SaveValueAsync("buffer_enable", enabled ? "1" : "0").ConfigureAwait(false);
            await SaveValueAsync("buffer_chunk_size_mb", chunkSizeMb.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            await SaveValueAsync("buffer_max_chunks", maxChunks.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            await SaveValueAsync("max_writer_workers_per_target", workers.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            await LoadAsync().ConfigureAwait(false);
        }

        public async Task SetTopMostPinAsync(bool enabled)
        {
            await SaveValueAsync("mini_topmost_pin", enabled ? "1" : "0").ConfigureAwait(false);
            await LoadAsync().ConfigureAwait(false);
        }

        public async Task SetSimulationBaselineAsync(double usb2, double usb3, double unknown)
        {
            await SaveValueAsync("simulation_usb2_mb", usb2.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            await SaveValueAsync("simulation_usb3_mb", usb3.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            await SaveValueAsync("simulation_unknown_mb", unknown.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            await LoadAsync().ConfigureAwait(false);
        }

        public async Task SetBillingModeAsync(BillingMode mode)
        {
            await SaveValueAsync("billing_mode", mode.ToString()).ConfigureAwait(false);
            await LoadAsync().ConfigureAwait(false);
        }

        public async Task SetAppThemeAsync(string value)
        {
            var normalized = value switch
            {
                "Light" => "Light",
                "Dark" => "Dark",
                _ => DefaultTheme
            };

            await SaveValueAsync("app_theme", normalized).ConfigureAwait(false);
            AppTheme = normalized;
        }

        public string GetAppTheme() => AppTheme;

        public bool TryUnlockAdmin(string pin)
        {
            if (string.IsNullOrWhiteSpace(pin))
            {
                return false;
            }

            if (HashPin(pin, AccessSettings.PinSalt) == AccessSettings.PinHash)
            {
                _adminUnlocked = true;
                UnlockedPin = pin;
                return true;
            }

            return false;
        }

        public void LockAdmin()
        {
            _adminUnlocked = false;
            UnlockedPin = null;
        }

        private Task SaveValueAsync(string key, string value) => _repository.SetAsync(key, value);

        private static string HashPin(string pin, string salt)
        {
            pin = pin?.Trim() ?? string.Empty;
            using var sha = SHA256.Create();
            var payload = Encoding.UTF8.GetBytes($"{salt}:{pin}");
            var hash = sha.ComputeHash(payload);
            var builder = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }

        private static string GenerateSalt()
        {
            var bytes = new byte[8];
            RandomNumberGenerator.Fill(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private async Task EnsurePinDefaultsAsync()
        {
            var currentHash = await _repository.GetAsync("admin_pin_hash").ConfigureAwait(false);
            if (string.IsNullOrEmpty(currentHash))
            {
                await SaveValueAsync("admin_pin_hash", DefaultPinHash).ConfigureAwait(false);
            }

            var currentSalt = await _repository.GetAsync("admin_pin_salt").ConfigureAwait(false);
            if (string.IsNullOrEmpty(currentSalt))
            {
                await SaveValueAsync("admin_pin_salt", DefaultSalt).ConfigureAwait(false);
            }
        }

        private static bool ParseBool(string? value, bool defaultValue)
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return value.Trim() switch
            {
                "1" or "true" or "True" => true,
                "0" or "false" or "False" => false,
                _ => defaultValue
            };
        }

        private static int ParseInt(string? value, int defaultValue)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            return defaultValue;
        }

        private static double ParseDouble(string? value, double defaultValue)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            return defaultValue;
        }

        private static BillingMode ParseBillingMode(string? value)
        {
            if (Enum.TryParse<BillingMode>(value, true, out var mode))
            {
                return mode;
            }

            return BillingMode.PerJob;
        }
    }
}
