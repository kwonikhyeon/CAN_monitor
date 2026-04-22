using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CanMonitor.Wpf.Shell;

public sealed partial class SessionViewModel : ObservableObject, ISessionState, IDisposable
{
    private readonly Wpf.Infrastructure.ICanBusFactory _factory;
    private readonly IDbcProvider _dbcProvider;
    private readonly CanEventHub _hub;
    private readonly ManualBusStatusPublisher _publisher;
    private readonly IAlarmEngine _alarmEngine;
    private readonly IEnumerable<IBusHeartbeatProvider> _heartbeatProviders;
    private readonly string _dbcRootDir;
    private readonly BehaviorSubject<ConnectionState> _stateSubject = new(ConnectionState.Disconnected);
    private readonly BehaviorSubject<DbcFileOption?> _dbcSubject = new(null);

    private ICanBus? _currentBus;
    private TxScheduler? _currentTxScheduler;
    private BusLifecycleService? _currentLifecycle;
    private IDisposable? _hubBinding;

    [ObservableProperty] private ConnectionState _state = ConnectionState.Disconnected;
    [ObservableProperty] private string? _errorMessage;

    public SessionViewModel(
        Wpf.Infrastructure.ICanBusFactory factory,
        IDbcProvider dbcProvider,
        CanEventHub hub,
        ManualBusStatusPublisher publisher,
        IAlarmEngine alarmEngine,
        IEnumerable<IBusHeartbeatProvider> heartbeatProviders,
        string dbcRootDir = "dbc")
    {
        _factory = factory;
        _dbcProvider = dbcProvider;
        _hub = hub;
        _publisher = publisher;
        _alarmEngine = alarmEngine;
        _heartbeatProviders = heartbeatProviders;
        _dbcRootDir = dbcRootDir;

        Adapters = _factory.Known;
        SelectedAdapter = Adapters.First();
    }

    public IReadOnlyList<AdapterOption> Adapters { get; }
    public AdapterOption SelectedAdapter { get; set; }
    public IReadOnlyList<DbcFileOption> DbcFiles { get; private set; } = Array.Empty<DbcFileOption>();

    private DbcFileOption? _selectedDbc;
    public DbcFileOption? SelectedDbc
    {
        get => _selectedDbc;
        set
        {
            if (SetProperty(ref _selectedDbc, value))
                _dbcSubject.OnNext(value);
        }
    }

    public IObservable<ConnectionState> StateChanges => _stateSubject;
    public IObservable<DbcFileOption?> DbcChanges => _dbcSubject;

    public async Task InitializeAsync()
    {
        DbcFiles = ScanDbcFolder(_dbcRootDir);
        SelectedDbc = DbcFiles.FirstOrDefault(f => f.DisplayName.Equals("120HP_NoPto.dbc", StringComparison.OrdinalIgnoreCase))
                      ?? DbcFiles.FirstOrDefault();
        if (SelectedDbc is null) return;
        try
        {
            await _dbcProvider.LoadAsync(SelectedDbc.Path);
        }
        catch (Exception ex)
        {
            SetState(ConnectionState.Error);
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting) return;
        try
        {
            SetState(ConnectionState.Connecting);
            _currentBus = _factory.Create(SelectedAdapter.Kind);
            await _currentBus.OpenAsync(new CanBusOptions());
            _currentTxScheduler = new TxScheduler(_currentBus);
            _currentLifecycle = new BusLifecycleService(_heartbeatProviders, _currentTxScheduler);
            _hubBinding = _hub.Attach(_currentBus,
                Observable.Never<SignalValue>(),              // Phase 3b: SignalDecoder 연결
                _alarmEngine.AlarmChanges,
                _publisher.Changes);
            _currentLifecycle.Start();
            _publisher.Publish(new BusStatusChange(BusStatus.Connected, null, null, 0, DateTimeOffset.UtcNow));
            SetState(ConnectionState.Connected);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            SetState(ConnectionState.Error);
            ErrorMessage = ex.Message;
            await DisconnectAsync();
        }
    }

    [RelayCommand]
    private Task DisconnectCommandImpl() => DisconnectAsync();

    public async Task DisconnectAsync()
    {
        if (_currentBus is null && State == ConnectionState.Disconnected) return;
        try
        {
            if (_currentLifecycle is not null) await _currentLifecycle.DisposeAsync();
            if (_currentTxScheduler is not null) await _currentTxScheduler.DisposeAsync();
            _hubBinding?.Dispose();
            if (_currentBus is not null) await _currentBus.CloseAsync();
            _publisher.Publish(new BusStatusChange(BusStatus.Disconnected, null, null, 0, DateTimeOffset.UtcNow));
        }
        finally
        {
            _currentLifecycle = null;
            _currentTxScheduler = null;
            _hubBinding = null;
            _currentBus = null;
            SetState(ConnectionState.Disconnected);
        }
    }

    private void SetState(ConnectionState s)
    {
        State = s;
        _stateSubject.OnNext(s);
    }

    private static IReadOnlyList<DbcFileOption> ScanDbcFolder(string root)
    {
        var result = new List<DbcFileOption>();
        Add(Path.Combine(root, "confirmed"), DbcSource.Confirmed);
        Add(Path.Combine(root, "experimental"), DbcSource.Experimental);
        return result;

        void Add(string dir, DbcSource src)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var path in Directory.EnumerateFiles(dir, "*.dbc"))
                result.Add(new DbcFileOption(path, Path.GetFileName(path), src));
        }
    }

    public void Dispose()
    {
        _hubBinding?.Dispose();
        _stateSubject.Dispose();
        _dbcSubject.Dispose();
    }
}
