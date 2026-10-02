using System.Collections.ObjectModel;

namespace NeoTaskmgr.Core.Collections;

public static class ObservableCollectionExtensions
{
    /// <summary>
    /// Makes <paramref name="target"/> equal to <paramref name="desired"/> using individual remove and insert
    /// operations, so a list view bound to it keeps its realized items instead of being reset.
    /// </summary>
    /// <remarks>
    /// Items that are already in the right relative order (the longest such run) stay put; only the others are
    /// removed and reinserted. A list view recreates the container of every item that moves, so moving the fewest
    /// items is what keeps rows from blinking when a sorted list is updated.
    /// </remarks>
    public static void SyncWith<T>(this ObservableCollection<T> target, IReadOnlyList<T> desired)
        where T : class
    {
        var desiredIndex = new Dictionary<T, int>(desired.Count, ReferenceEqualityComparer.Instance);
        for (var i = 0; i < desired.Count; i++)
        {
            desiredIndex[desired[i]] = i;
        }

        var positions = target.Select(item => desiredIndex.GetValueOrDefault(item, -1)).ToList();
        var stay = LongestIncreasingRun(positions);
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!stay.Contains(i))
            {
                target.RemoveAt(i);
            }
        }

        // What remains is in the desired order, so filling the gaps produces the desired list.
        for (var i = 0; i < desired.Count; i++)
        {
            if (i >= target.Count || !ReferenceEquals(target[i], desired[i]))
            {
                target.Insert(i, desired[i]);
            }
        }
    }

    /// <summary>Indexes of a longest strictly increasing subsequence of the non-negative values.</summary>
    private static HashSet<int> LongestIncreasingRun(List<int> values)
    {
        var tails = new List<int>(); // tails[k]: index of the smallest last value of a run of length k + 1.
        var previous = new int[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] < 0)
            {
                continue;
            }

            int low = 0, high = tails.Count;
            while (low < high)
            {
                var mid = (low + high) / 2;
                if (values[tails[mid]] < values[i])
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            previous[i] = low > 0 ? tails[low - 1] : -1;
            if (low == tails.Count)
            {
                tails.Add(i);
            }
            else
            {
                tails[low] = i;
            }
        }

        var run = new HashSet<int>();
        for (var i = tails.Count > 0 ? tails[^1] : -1; i >= 0; i = previous[i])
        {
            run.Add(i);
        }

        return run;
    }
}
