using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CatsAreOnlineServer.Configuration;

public class Config {
    public string path { get; }

    public IReadOnlyDictionary<string, ConfigValueBase> values => _values;

    private Dictionary<string, ConfigValueBase> _values = new();
    private readonly JsonSerializerOptions _jsonOptions = new() {
        WriteIndented = true,
        AllowTrailingCommas = false,
        Converters = {
            new ConfigValueBaseJsonConverter()
        }
    };

    public Config(string path) => this.path = path;

    public ConfigValue<T> AddValue<T>(string key, ConfigValue<T> value) {
        if(_values.TryAdd(key, value)) return value;
        ConfigValue<T> newValue = new(value.boxedDefaultValue) { boxedValue = _values[key].boxedValue };
        _values[key] = newValue;
        return newValue;
    }

    public ConfigValue<T> GetValue<T>(string key) => (ConfigValue<T>)_values[key];

    public bool TryGetValue<T>(string key, out ConfigValue<T> value) {
        bool exists = _values.TryGetValue(key, out ConfigValueBase dictValue);
        value = (ConfigValue<T>)dictValue;
        return exists;
    }

    public bool TryGetValue(string key, out ConfigValueBase value) {
        bool exists = _values.TryGetValue(key, out ConfigValueBase dictValue);
        value = dictValue;
        return exists;
    }

    public void SetJsonValue(string key, string value) {
        if(!TryGetValue(key, out ConfigValueBase configValue))
            throw new InvalidDataException($"{key} doesn't exist.");
        ConfigValueBase newValue =
            JsonSerializer.Deserialize<ConfigValueBase>($"{{\"value\":{value}}}", _jsonOptions);
        configValue.boxedValue = newValue.boxedValue;
    }

    public void Load() => _values = File.Exists(path) ?
        JsonSerializer.Deserialize<Dictionary<string, ConfigValueBase>>(File.ReadAllText(path),
            _jsonOptions) : new Dictionary<string, ConfigValueBase>();
    public void Save() => File.WriteAllText(path, JsonSerializer.Serialize(_values, _jsonOptions));
}