namespace DubUrl.Schema;

internal static class ViewDependencyPlanner
{
    public static IReadOnlyList<View> Order(IEnumerable<View> views)
    {
        var declared = views.ToArray();
        var byKey = declared.ToDictionary(view => view.Identity.Key, StringComparer.Ordinal);
        var indegree = declared.ToDictionary(view => view.Identity.Key, _ => 0, StringComparer.Ordinal);
        var dependents = declared.ToDictionary(
            view => view.Identity.Key,
            _ => new List<View>(),
            StringComparer.Ordinal);

        foreach (var view in declared)
            if (byKey.TryGetValue(view.Query.Source.Key, out var dependency))
            {
                indegree[view.Identity.Key]++;
                dependents[dependency.Identity.Key].Add(view);
            }

        var ordered = new List<View>(declared.Length);
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        while (ordered.Count < declared.Length)
        {
            var next = declared.FirstOrDefault(view =>
                !emitted.Contains(view.Identity.Key) && indegree[view.Identity.Key] == 0);
            if (next is null)
                throw new ArgumentException(
                    $"View dependency cycle detected: {string.Join(" -> ", FindCycle(declared, byKey))}.",
                    nameof(views));

            ordered.Add(next);
            emitted.Add(next.Identity.Key);
            foreach (var dependent in dependents[next.Identity.Key])
                indegree[dependent.Identity.Key]--;
        }

        return ordered.AsReadOnly();
    }

    private static IReadOnlyList<string> FindCycle(
        IReadOnlyList<View> declared,
        IReadOnlyDictionary<string, View> byKey)
    {
        foreach (var start in declared)
        {
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            var path = new List<View>();
            var current = start;
            while (true)
            {
                if (positions.TryGetValue(current.Identity.Key, out var cycleStart))
                    return path.Skip(cycleStart).Select(view => view.Identity.ToString())
                        .Append(current.Identity.ToString()).ToArray();

                positions[current.Identity.Key] = path.Count;
                path.Add(current);
                if (!byKey.TryGetValue(current.Query.Source.Key, out current!))
                    break;
            }
        }

        return ["unknown"];
    }
}
