using System.Collections.Generic;

namespace Azathrix.MiniPanda.Core
{
    public class Environment : IEnvironmentProvider
    {
        private readonly Dictionary<string, Value> _values = new Dictionary<string, Value>();
        private readonly Environment _parent;
        private readonly IEnvironmentProvider _provider;

        public Environment() { }

        public Environment(Environment parent)
        {
            _parent = parent;
        }

        public Environment(IEnvironmentProvider provider)
        {
            _provider = provider;
        }

        public Environment(Environment parent, IEnvironmentProvider provider)
        {
            _parent = parent;
            _provider = provider;
        }

        public Environment CreateChild() => new Environment(this);

        public void Define(string name, Value value)
        {
            _values[name] = value;
        }

        public void Define(string name, object value)
        {
            _values[name] = ConvertToValue(value);
        }

        public void Set(string name, Value value)
        {
            if (_values.ContainsKey(name))
            {
                _values[name] = value;
                return;
            }
            if (_parent != null)
            {
                _parent.Set(name, value);
                return;
            }
            // Define in current scope if not found
            _values[name] = value;
        }

        public void Set(string name, object value)
        {
            Set(name, ConvertToValue(value));
        }

        public Value Get(string name)
        {
            if (_values.TryGetValue(name, out var value))
                return value;
            if (_provider != null && _provider.Contains(name))
                return _provider.Get(name);
            if (_parent != null)
                return _parent.Get(name);
            return Value.Null;
        }

        public bool Contains(string name)
        {
            if (_values.ContainsKey(name)) return true;
            if (_provider != null && _provider.Contains(name)) return true;
            return _parent?.Contains(name) ?? false;
        }

        public bool ContainsLocal(string name) => _values.ContainsKey(name);

        public void Clear() => _values.Clear();

        public void SetLocal(string name, Value value)
        {
            _values[name] = value;
        }

        public void SetLocal(string name, object value)
        {
            _values[name] = ConvertToValue(value);
        }

        public Value GetLocal(string name)
        {
            return _values.TryGetValue(name, out var value) ? value : Value.Null;
        }

        public IEnumerable<KeyValuePair<string, Value>> GetAll()
        {
            return _values;
        }

        public Environment With(params (string name, Value value)[] vars)
        {
            foreach (var (name, value) in vars)
            {
                Define(name, value);
            }
            return this;
        }

        public Environment With(Dictionary<string, object> dict)
        {
            if (dict == null) return this;

            foreach (var kvp in dict)
            {
                Define(kvp.Key, ConvertToValue(kvp.Value));
            }
            return this;
        }

        private static Value ConvertToValue(object obj)
        {
            return obj switch
            {
                null => Value.Null,
                bool b => Value.FromBool(b),
                int i => Value.FromNumber(i),
                long l => Value.FromNumber(l),
                float f => Value.FromNumber(f),
                double d => Value.FromNumber(d),
                string s => s,
                Value v => v,
                _ => Value.FromObject(MiniPandaString.Create(obj.ToString()))
            };
        }
    }
}
