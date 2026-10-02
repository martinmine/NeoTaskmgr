namespace NeoTaskmgr.Core.Processes;

public static class ProcessTree
{
    /// <summary>
    /// Returns all descendants of <paramref name="root"/>, deepest first (so they can be ended bottom-up).
    /// A child must be created after its parent; this filters out processes whose parent PID was reused.
    /// </summary>
    public static IReadOnlyList<ProcessCounters> GetDescendants(IReadOnlyList<ProcessCounters> processes, ProcessKey root)
    {
        var childrenByParent = processes.ToLookup(p => p.ParentPid);
        var result = new List<ProcessCounters>();
        var visited = new HashSet<ProcessKey> { root };

        void Visit(ProcessKey parent)
        {
            foreach (var child in childrenByParent[parent.Pid])
            {
                if (child.CreateTime >= parent.CreateTime && visited.Add(child.Key))
                {
                    Visit(child.Key);
                    result.Add(child);
                }
            }
        }

        Visit(root);
        return result;
    }
}
