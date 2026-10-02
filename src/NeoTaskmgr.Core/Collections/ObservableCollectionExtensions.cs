using System.Collections.ObjectModel;

namespace NeoTaskmgr.Core.Collections;

public static class ObservableCollectionExtensions
{
    /// <summary>
    /// Makes <paramref name="target"/> equal to <paramref name="desired"/> using individual remove, move and insert
    /// operations, so a list view bound to it keeps its realized items instead of being reset.
    /// </summary>
    public static void SyncWith<T>(this ObservableCollection<T> target, IReadOnlyList<T> desired)
        where T : class
    {
        var keep = new HashSet<T>(desired, ReferenceEqualityComparer.Instance);
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(target[i]))
            {
                target.RemoveAt(i);
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            var item = desired[i];
            if (i < target.Count && ReferenceEquals(target[i], item))
            {
                continue;
            }

            var current = IndexOf(target, item, start: i + 1);
            if (current >= 0)
            {
                target.Move(current, i);
            }
            else
            {
                target.Insert(i, item);
            }
        }
    }

    private static int IndexOf<T>(ObservableCollection<T> list, T item, int start)
        where T : class
    {
        for (var i = start; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], item))
            {
                return i;
            }
        }

        return -1;
    }
}
