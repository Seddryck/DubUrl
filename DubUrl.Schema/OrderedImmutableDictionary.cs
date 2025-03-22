using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema;

public sealed class OrderedImmutableDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private ImmutableList<KeyValuePair<TKey, TValue>> Ordered { get; }
    private ImmutableDictionary<TKey, TValue> Map { get; }

    private OrderedImmutableDictionary(
        ImmutableList<KeyValuePair<TKey, TValue>> ordered,
        ImmutableDictionary<TKey, TValue> map)
    {
        Ordered = ordered;
        Map = map;
    }

    public static OrderedImmutableDictionary<TKey, TValue> Empty { get; } =
        new(ImmutableList<KeyValuePair<TKey, TValue>>.Empty, ImmutableDictionary<TKey, TValue>.Empty);

    public static OrderedImmutableDictionary<TKey, TValue> From(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        var list = ImmutableList.CreateBuilder<KeyValuePair<TKey, TValue>>();
        var dict = ImmutableDictionary.CreateBuilder<TKey, TValue>();

        foreach (var kv in items)
        {
            if (!dict.ContainsKey(kv.Key))
            {
                list.Add(kv);
                dict[kv.Key] = kv.Value;
            }
        }

        return new OrderedImmutableDictionary<TKey, TValue>(list.ToImmutable(), dict.ToImmutable());
    }

    public TValue this[TKey key] => Map[key];
    public IEnumerable<TKey> Keys => Ordered.Select(kv => kv.Key);
    public IEnumerable<TValue> Values => Ordered.Select(kv => kv.Value);
    public int Count => Ordered.Count;
    public bool ContainsKey(TKey key) => Map.ContainsKey(key);
    public bool TryGetValue(TKey key, out TValue value) => Map.TryGetValue(key, out value!);
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Ordered.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public OrderedImmutableDictionary<TKey, TValue> Add(TKey key, TValue value)
    {
        if (Map.ContainsKey(key))
            throw new ArgumentException("An element with the same key already exists.");

        return new OrderedImmutableDictionary<TKey, TValue>(
            Ordered.Add(new KeyValuePair<TKey, TValue>(key, value)),
            Map.Add(key, value)
        );
    }

    public OrderedImmutableDictionary<TKey, TValue> Remove(TKey key)
    {
        if (!Map.ContainsKey(key))
            return this;

        var newOrdered = Ordered.RemoveAll(kv => EqualityComparer<TKey>.Default.Equals(kv.Key, key));
        var newMap = Map.Remove(key);
        return new OrderedImmutableDictionary<TKey, TValue>(newOrdered, newMap);
    }

    public OrderedImmutableDictionary<TKey, TValue> SetItem(TKey key, TValue value)
    {
        if (!Map.ContainsKey(key))
            return Add(key, value);

        var newOrdered = Ordered.Select(kv =>
            EqualityComparer<TKey>.Default.Equals(kv.Key, key)
                ? new KeyValuePair<TKey, TValue>(key, value)
                : kv).ToImmutableList();

        var newMap = Map.SetItem(key, value);
        return new OrderedImmutableDictionary<TKey, TValue>(newOrdered, newMap);
    }
}
