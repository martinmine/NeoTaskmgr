using Microsoft.UI.Xaml;
using NeoTaskmgr.ViewModels.Performance;
using Windows.UI;

namespace NeoTaskmgr;

/// <summary>Small conversion functions for x:Bind.</summary>
public static class XamlHelpers
{
    /// <summary>Indents child rows of a tree-like table.</summary>
    public static Thickness Indent(bool isChild) => new(isChild ? 24 : 0, 0, 0, 0);

    /// <summary>A proportional (star) size, e.g. for the parts of the memory composition bar.</summary>
    public static GridLength Star(long value) => new(Math.Max(0, value), GridUnitType.Star);

    public static Visibility Not(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility NotEmpty(string? value) => string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;

    // For templates whose data has no device kind (x:Bind cannot pass enum values to functions).
    public static Color CpuColor => DeviceColor(PerformanceDeviceKind.Cpu);
    public static Color GpuColor => DeviceColor(PerformanceDeviceKind.Gpu);

    /// <summary>The graph color of a device type, matching Task Manager's.</summary>
    public static Color DeviceColor(PerformanceDeviceKind kind) => kind switch
    {
        PerformanceDeviceKind.Cpu => Color.FromArgb(255, 17, 125, 187),
        PerformanceDeviceKind.Memory => Color.FromArgb(255, 139, 18, 174),
        PerformanceDeviceKind.Disk => Color.FromArgb(255, 77, 166, 12),
        PerformanceDeviceKind.Network => Color.FromArgb(255, 167, 79, 1),
        _ => Color.FromArgb(255, 172, 57, 49),
    };
}
