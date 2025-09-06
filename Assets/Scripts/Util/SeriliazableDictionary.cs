using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver
{
    [SerializeField] private List<TKey> _keys = new();
    [SerializeField] private List<TValue> _values = new();

    private Dictionary<TKey, TValue> _dict;

    public Dictionary<TKey, TValue> Dictionary
    {
        get
        {
            _dict ??= new Dictionary<TKey, TValue>();
            return _dict;
        }
    }

    public void OnBeforeSerialize()
    {
        _keys.Clear();
        _values.Clear();
        if (_dict == null) return;

        foreach (var kv in _dict)
        {
            _keys.Add(kv.Key);
            _values.Add(kv.Value);
        }
    }
    public void OnAfterDeserialize()
    {
        _dict = new Dictionary<TKey, TValue>(_keys.Count);
        for (int i = 0; i < Math.Min(_keys.Count, _values.Count); i++)
        {
            // If keys can repeat in the list, last one wins
            _dict[_keys[i]] = _values[i];
        }
    }

    public bool TryGetValue(TKey key, out TValue value) => Dictionary.TryGetValue(key, out value);
    public void Add(TKey key, TValue value) => Dictionary.Add(key, value);
    public static SerializableDictionary<TKey, TValue> FromDictionary(Dictionary<TKey, TValue> input)
    {
        var serializableDict = new SerializableDictionary<TKey, TValue>();
        foreach (var kvp in input)
        {
            serializableDict.Add(kvp.Key, kvp.Value);
        }
        return serializableDict;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return Dictionary.GetEnumerator();
    }

}
