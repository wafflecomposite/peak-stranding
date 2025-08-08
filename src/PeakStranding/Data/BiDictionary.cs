using System;
using System.Collections;
using System.Collections.Generic;

public class BiDictionary : IEnumerable<KeyValuePair<string, string>>
{
    private readonly Dictionary<string, string> _forward = new();
    private readonly Dictionary<string, string> _reverse = new();

    // This enables collection initializer syntax
    public void Add(string first, string second)
    {
        if (_forward.ContainsKey(first) || _reverse.ContainsKey(second))
            throw new ArgumentException("Duplicate key or value.");

        _forward[first] = second;
        _reverse[second] = first;
    }

    public bool TryGetByFirst(string first, out string second) =>
        _forward.TryGetValue(first, out second);

    public bool TryGetBySecond(string second, out string first) =>
        _reverse.TryGetValue(second, out first);

    public bool RemoveByFirst(string first)
    {
        if (!_forward.TryGetValue(first, out var second))
            return false;

        _forward.Remove(first);
        _reverse.Remove(second);
        return true;
    }

    public bool RemoveBySecond(string second)
    {
        if (!_reverse.TryGetValue(second, out var first))
            return false;

        _reverse.Remove(second);
        _forward.Remove(first);
        return true;
    }

    public List<string> GetAllFirsts() => new List<string>(_forward.Keys);
    public List<string> GetAllSeconds() => new List<string>(_reverse.Keys);

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _forward.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal string TryGetByFirst(string prefabName)
    {
        throw new NotImplementedException();
    }
}
