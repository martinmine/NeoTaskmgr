using System.Collections.ObjectModel;
using System.Collections.Specialized;
using NeoTaskmgr.Core.Collections;

namespace NeoTaskmgr.Core.Tests.Collections;

public sealed class ObservableCollectionExtensionsTests
{
    private static readonly string[] Items = ["a", "b", "c", "d", "e"];

    public static TheoryData<int[], int[]> Cases => new()
    {
        { [], [0, 1, 2] },
        { [0, 1, 2], [] },
        { [0, 1, 2], [2, 1, 0] },
        { [0, 1, 2, 3], [3, 0, 4, 1] },
        { [0, 2, 4], [1, 2, 3, 4] },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void SyncWith_ProducesDesiredOrder(int[] initial, int[] desired)
    {
        var target = new ObservableCollection<string>(initial.Select(i => Items[i]));
        var expected = desired.Select(i => Items[i]).ToList();

        target.SyncWith(expected);

        Assert.Equal(expected, target);
    }

    [Fact]
    public void SyncWith_UnchangedList_RaisesNoEvents()
    {
        var target = new ObservableCollection<string>(Items);
        var events = 0;
        target.CollectionChanged += (_, _) => events++;

        target.SyncWith(Items);

        Assert.Equal(0, events);
    }

    [Fact]
    public void SyncWith_NeverResets()
    {
        var target = new ObservableCollection<string>(Items);
        var actions = new List<NotifyCollectionChangedAction>();
        target.CollectionChanged += (_, e) => actions.Add(e.Action);

        target.SyncWith(["e", "a", "c"]);

        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
    }
}
