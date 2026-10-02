using System.Diagnostics;
using NeoTaskmgr.Core.Performance;
using NeoTaskmgr.Core.Processes;
using NeoTaskmgr.Core.Settings;

namespace NeoTaskmgr.Core.Sampling;

/// <summary>Everything collected in one sampling tick.</summary>
public sealed record SystemSnapshot(ProcessSnapshot Processes, MemoryStatus Memory);

/// <summary>
/// Collects a <see cref="SystemSnapshot"/> on a background thread at the configured <see cref="UpdateSpeed"/>.
/// <see cref="Sampled"/> is raised on that background thread.
/// </summary>
public sealed class SamplingService(UpdateSpeed speed) : IDisposable
{
    private readonly ProcessProvider _processes = new();
    private readonly SemaphoreSlim _wake = new(0);
    private readonly CancellationTokenSource _stop = new();
    private volatile UpdateSpeed _speed = speed;
    private Task? _loop;

    public event Action<SystemSnapshot>? Sampled;

    /// <summary>Changing the speed takes effect immediately (and triggers a sample).</summary>
    public UpdateSpeed Speed
    {
        get => _speed;
        set
        {
            _speed = value;
            Wake();
        }
    }

    public void Start() => _loop ??= Task.Run(() => RunAsync(_stop.Token));

    /// <summary>Samples now, also when paused.</summary>
    public void RefreshNow() => Wake();

    public void Dispose()
    {
        _stop.Cancel();
        // Only free native buffers once the loop is guaranteed to no longer use them.
        if (_loop is null || _loop.Wait(TimeSpan.FromSeconds(2)))
        {
            _processes.Dispose();
        }
    }

    private void Wake()
    {
        if (_wake.CurrentCount == 0)
        {
            _wake.Release();
        }
    }

    private async Task RunAsync(CancellationToken stop)
    {
        var clock = Stopwatch.StartNew();
        while (!stop.IsCancellationRequested)
        {
            try
            {
                Sampled?.Invoke(new SystemSnapshot(_processes.Sample(clock.Elapsed), MemoryStatus.Read()));
            }
            catch (Exception e)
            {
                // Keep sampling; a single failed tick should not stop the app from updating.
                Debug.WriteLine($"Sampling failed: {e}");
            }

            try
            {
                await _wake.WaitAsync(_speed.ToInterval() ?? Timeout.InfiniteTimeSpan, stop);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
