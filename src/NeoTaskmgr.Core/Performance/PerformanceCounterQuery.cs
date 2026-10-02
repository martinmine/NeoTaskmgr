using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Performance;

namespace NeoTaskmgr.Core.Performance;

/// <summary>
/// A query of Windows performance counters (PDH). Counters are added by their English path, so it works on any
/// display language. Call <see cref="Collect"/> once per sample, then read the values.
/// </summary>
internal sealed unsafe class PerformanceCounterQuery : IDisposable
{
    private const uint ValidData = 0; // PDH_CSTATUS_VALID_DATA
    private const uint NewData = 1; // PDH_CSTATUS_NEW_DATA
    private const PDH_FMT Format = PDH_FMT.PDH_FMT_DOUBLE | (PDH_FMT)0x8000; // PDH_FMT_NOCAP100: don't cap percentages at 100.

    private readonly PdhCloseQuerySafeHandle _query;

    public PerformanceCounterQuery()
    {
        var status = PInvoke.PdhOpenQuery(null, 0, out _query);
        if (status != 0)
        {
            throw new Win32Exception((int)status);
        }
    }

    /// <summary>Adds a counter, or returns null if it does not exist on this system (e.g. no GPU counters).</summary>
    public PDH_HCOUNTER? TryAdd(string englishPath) =>
        PInvoke.PdhAddEnglishCounter(_query, englishPath, 0, out var counter) == 0 ? counter : null;

    /// <summary>Takes a sample of all counters. Rate counters need two samples before they have a value.</summary>
    public void Collect() => PInvoke.PdhCollectQueryData((PDH_HQUERY)_query.DangerousGetHandle());

    /// <summary>The counter's value, or 0 if it has no valid value (yet).</summary>
    public double GetValue(PDH_HCOUNTER? counter) =>
        counter is { } handle &&
        PInvoke.PdhGetFormattedCounterValue(handle, Format, out var value) == 0 &&
        value.CStatus is ValidData or NewData
            ? value.Anonymous.doubleValue
            : 0;

    /// <summary>The values of all instances of a wildcard counter, such as <c>\PhysicalDisk(*)\% Idle Time</c>.</summary>
    public List<(string Instance, double Value)> GetValues(PDH_HCOUNTER? counter)
    {
        var values = new List<(string, double)>();
        if (counter is not { } handle)
        {
            return values;
        }

        // Instances can appear between the size query and the read, so retry a few times.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            uint size = 0;
            if (PInvoke.PdhGetFormattedCounterArray(handle, Format, ref size, out _) != PInvoke.PDH_MORE_DATA)
            {
                return values;
            }

            var buffer = NativeMemory.Alloc(size);
            try
            {
                uint count;
                var status = PInvoke.PdhGetFormattedCounterArray(handle, Format, &size, &count, (PDH_FMT_COUNTERVALUE_ITEM_W*)buffer);
                if (status == PInvoke.PDH_MORE_DATA)
                {
                    continue;
                }

                var items = (PDH_FMT_COUNTERVALUE_ITEM_W*)buffer;
                for (var i = 0; status == 0 && i < count; i++)
                {
                    if (items[i].FmtValue.CStatus is ValidData or NewData)
                    {
                        values.Add((items[i].szName.ToString(), items[i].FmtValue.Anonymous.doubleValue));
                    }
                }

                return values;
            }
            finally
            {
                NativeMemory.Free(buffer);
            }
        }

        return values;
    }

    public void Dispose() => _query.Dispose();
}
