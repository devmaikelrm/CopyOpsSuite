using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using CopyOpsSuite.Core.Models;

namespace CopyOpsSuite.App.WinUI.ViewModels
{
    public partial class CashViewModel : ObservableObject
    {
        private const int RefreshIntervalSeconds = 12;
        private readonly AppServices _services;
        private readonly DispatcherQueue _dispatcher;
        private readonly DispatcherQueueTimer _refreshTimer;
        private bool _isRefreshingSales;

        public ObservableCollection<CashCurrencySummary> TodaySummaries { get; } = new();
        public ObservableCollection<SaleRowViewModel> Sales { get; } = new();

        [ObservableProperty]
        private DateTime salesFrom = DateTime.Today.AddDays(-7);

        [ObservableProperty]
        private DateTime salesTo = DateTime.Today.AddDays(1);

        [ObservableProperty]
        private SaleStatus? selectedStatus;

        [ObservableProperty]
        private string operatorFilter = string.Empty;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private decimal countedAmount;

        [ObservableProperty]
        private decimal expectedAmount;

        [ObservableProperty]
        private string closingNotes = string.Empty;

        [ObservableProperty]
        private string closingMessage = string.Empty;

        [ObservableProperty]
        private string highlightedSaleMessage = string.Empty;

        [ObservableProperty]
        private string lastDiagSummary = string.Empty;

        [ObservableProperty]
        private DateTime? lastDiagTimestamp;

        public string LastDiagSummaryDisplay =>
            string.IsNullOrWhiteSpace(LastDiagSummary)
                ? "Sin diagnósticos recientes."
                : LastDiagTimestamp.HasValue
                    ? $"{LastDiagTimestamp:dd/MM HH:mm} - {LastDiagSummary}"
                    : LastDiagSummary;

        public CashViewModel(AppServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _dispatcher = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("DispatcherQueue no disponible.");
            _refreshTimer = DispatcherQueue.CreateTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds);
            _refreshTimer.Tick += (_, _) => _ = RefreshSalesAsync();
            _refreshTimer.Start();
            _services.CashSelectionState.FocusChanged += CashSelectionState_FocusChanged;
            _ = RefreshSalesAsync();
        }

        partial void OnLastDiagSummaryChanged(string value) => OnPropertyChanged(nameof(LastDiagSummaryDisplay));

        public IReadOnlyList<StatusOption> StatusOptions { get; } = Enum.GetValues<SaleStatus>()
            .Select(s => new StatusOption(s.ToString(), s))
            .Prepend(new StatusOption("Todos", null))
            .ToList();

        [RelayCommand]
        private async Task RefreshSalesAsync()
        {
            if (_isRefreshingSales)
            {
                return;
            }

            _isRefreshingSales = true;
            try
            {
                var from = SalesFrom;
                var to = SalesTo;
                var sales = await _services.Repositories.Sales.QueryAsync(from, to).ConfigureAwait(false);
                var filtered = FilterSales(sales).ToList();
                await UpdateSalesAsync(filtered).ConfigureAwait(false);
                UpdateTodaySummaries(sales);
                await UpdateClosingAmountsAsync(sales).ConfigureAwait(false);
                await UpdateDiagSummaryAsync().ConfigureAwait(false);
            }
            finally
            {
                _isRefreshingSales = false;
            }
        }

        [RelayCommand]
        private Task ResetFiltersAsync()
        {
            SalesFrom = DateTime.Today.AddDays(-7);
            SalesTo = DateTime.Today.AddDays(1);
            SelectedStatus = null;
            OperatorFilter = string.Empty;
            SearchText = string.Empty;
            _services.CashSelectionState.Clear();
            HighlightedSaleMessage = string.Empty;
            return RefreshSalesAsync();
        }

        [RelayCommand]
        private async Task CloseShiftAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var register = new CashRegister
            {
                Day = today,
                OpeningAmount = 0,
                ClosingAmount = CountedAmount,
                CountedAmount = CountedAmount,
                ExpectedAmount = ExpectedAmount,
                Notes = ClosingNotes,
                Sales = Sales.Select(row => row.Sale).ToList()
            };

            await _services.Repositories.CashRegister.UpsertAsync(register).ConfigureAwait(false);
            var diff = CountedAmount - ExpectedAmount;
            var severity = diff == 0 ? EventSeverity.Info : EventSeverity.Critical;
            _services.AuditService.RecordEvent("CASH_CLOSED", $"Cierre con diferencia {diff:0.##}", severity);
            ClosingMessage = diff == 0 ? "Caja cuadrada" : $"Diferencia {diff:0.##}";
        }

        private IEnumerable<Sale> FilterSales(IEnumerable<Sale> sales)
        {
            var query = sales;
            if (SelectedStatus.HasValue)
            {
                query = query.Where(s => s.Status == SelectedStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(OperatorFilter))
            {
                query = query.Where(s => s.OperatorName.Contains(OperatorFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim();
                query = query.Where(s =>
                    s.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || s.Notes.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || s.SaleId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            return query;
        }

        private async Task UpdateSalesAsync(IEnumerable<Sale> sales)
        {
            await InvokeOnDispatcherAsync(() =>
            {
                Sales.Clear();
                foreach (var sale in sales.OrderByDescending(s => s.Ts))
                {
                    Sales.Add(new SaleRowViewModel(sale));
                }
            });
        }

        private async Task UpdateClosingAmountsAsync(IEnumerable<Sale> allSales)
        {
            var salesToday = allSales.Where(s => s.Ts.Date == DateTime.Today);
            var expected = salesToday.Sum(s => s.AmountExpected);
            var shouldResetCounted = CountedAmount == 0 || Math.Abs(CountedAmount - ExpectedAmount) < 0.01m;
            await InvokeOnDispatcherAsync(() =>
            {
                ExpectedAmount = expected;
                if (shouldResetCounted)
                {
                    CountedAmount = expected;
                }
            });
        }

        private void UpdateTodaySummaries(IEnumerable<Sale> sales)
        {
            var today = DateTime.Today;
            var grouped = sales.Where(s => s.Ts.Date == today)
                .GroupBy(s => s.Currency)
                .Select(g => new CashCurrencySummary
                {
                    Currency = "CUP+",
                    Expected = g.Sum(s => s.AmountExpected),
                    Paid = g.Sum(s => s.AmountPaid),
                    Difference = g.Sum(s => s.AmountExpected) - g.Sum(s => s.AmountPaid),
                    GbToday = g.Sum(s => s.RealGb)
                }).ToList();

            InvokeOnDispatcherAsync(() =>
            {
                TodaySummaries.Clear();
                foreach (var summary in grouped)
                {
                    TodaySummaries.Add(summary);
                }
            });
        }

        private async Task UpdateDiagSummaryAsync()
        {
            var from = DateTime.UtcNow.AddDays(-7);
            var diagEvents = await _services.Repositories.Events.QueryAsync(from, DateTime.UtcNow, "DIAG_BUFFER_SUMMARY").ConfigureAwait(false);
            var latest = diagEvents.FirstOrDefault();
            if (latest != null)
            {
                await InvokeOnDispatcherAsync(() =>
                {
                    LastDiagSummary = latest.Message;
                    LastDiagTimestamp = latest.Ts;
                }).ConfigureAwait(false);
            }
            else
            {
                await InvokeOnDispatcherAsync(() =>
                {
                    LastDiagSummary = string.Empty;
                    LastDiagTimestamp = null;
                }).ConfigureAwait(false);
            }
        }

        private async void CashSelectionState_FocusChanged(object? sender, EventArgs e)
        {
            var state = _services.CashSelectionState;
            if (state.SaleId.HasValue)
            {
                var jobDesc = state.JobId.HasValue ? $" job {state.JobId.Value:D}" : string.Empty;
                HighlightedSaleMessage = $"Venta destacada {state.SaleId.Value:D}{jobDesc}";
                SearchText = state.SaleId.Value.ToString();
                await RefreshSalesAsync().ConfigureAwait(false);
            }
            else
            {
                HighlightedSaleMessage = string.Empty;
            }
        }

        private Task InvokeOnDispatcherAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            _dispatcher.TryEnqueue(() =>
            {
                action();
                tcs.SetResult(true);
            });
            return tcs.Task;
        }
    }

    public sealed class CashCurrencySummary
    {
        public string Currency { get; set; } = string.Empty;
        public decimal Expected { get; set; }
        public decimal Paid { get; set; }
        public decimal Difference { get; set; }
        public decimal GbToday { get; set; }
    }

    public sealed class SaleRowViewModel
    {
        public Sale Sale { get; }

        public SaleRowViewModel(Sale sale)
        {
            Sale = sale;
        }

        public string Timestamp => Sale.Ts.ToString("g");
        public string Customer => Sale.CustomerName;
        public string Operator => Sale.OperatorName;
        public string Currency => "CUP+";
        public decimal Expected => Sale.AmountExpected;
        public decimal Paid => Sale.AmountPaid;
        public string Status => Sale.Status.ToString();
        public string Method => Sale.PaymentMethod;
        public string RealGb => $"{Sale.RealGb:0.##}";
        public string BillableGb => $"{Sale.BillableGb:0.##}";
        public string RoundMode => Sale.RoundMode.ToString();
    }

    public sealed class StatusOption
    {
        public string Label { get; }
        public SaleStatus? Value { get; }

        public StatusOption(string label, SaleStatus? value)
        {
            Label = label;
            Value = value;
        }
    }
}
