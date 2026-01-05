using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Azathrix.MiniPanda.Core
{
    /// <summary>
    /// MiniPanda 字符串类型，支持字符串驻留优化
    /// </summary>
    public class MiniPandaString : MiniPandaHeapObject
    {
        // 字符串驻留池（短字符串自动驻留以减少内存分配）
        private static readonly ConcurrentDictionary<string, MiniPandaString> _internPool = new ConcurrentDictionary<string, MiniPandaString>();
        private const int InternThreshold = 64; // 只驻留短字符串

        public string Value { get; }

        private MiniPandaString(string value) => Value = value;

        /// <summary>
        /// 创建或获取驻留的字符串实例
        /// </summary>
        public static MiniPandaString Create(string value)
        {
            if (value == null) return new MiniPandaString(string.Empty);

            // 短字符串使用驻留池
            if (value.Length <= InternThreshold)
            {
                return _internPool.GetOrAdd(value, v => new MiniPandaString(v));
            }

            // 长字符串直接创建
            return new MiniPandaString(value);
        }

        /// <summary>
        /// 清空驻留池（用于重置 VM 状态）
        /// </summary>
        public static void ClearInternPool() => _internPool.Clear();

        public override string ToString() => Value;
    }

    public class MiniPandaArray : MiniPandaHeapObject
    {
        public List<Value> Elements { get; } = new List<Value>();

        public MiniPandaArray() { }
        public MiniPandaArray(IEnumerable<Value> elements) => Elements.AddRange(elements);

        public int Length => Elements.Count;

        public Value Get(int index)
        {
            if (index < 0 || index >= Elements.Count) return Value.Null;
            return Elements[index];
        }

        public void Set(int index, Value value)
        {
            while (Elements.Count <= index) Elements.Add(Value.Null);
            Elements[index] = value;
        }

        public void Push(Value value) => Elements.Add(value);
        public Value Pop()
        {
            if (Elements.Count == 0) return Value.Null;
            var value = Elements[Elements.Count - 1];
            Elements.RemoveAt(Elements.Count - 1);
            return value;
        }

        public override string ToString() => $"[{string.Join(", ", Elements)}]";
    }

    public class MiniPandaObject : MiniPandaHeapObject
    {
        public Dictionary<string, Value> Fields { get; } = new Dictionary<string, Value>();

        public Value Get(string key)
        {
            return Fields.TryGetValue(key, out var value) ? value : Value.Null;
        }

        public void Set(string key, Value value)
        {
            Fields[key] = value;
        }

        public bool Contains(string key) => Fields.ContainsKey(key);

        public override string ToString()
        {
            var pairs = new List<string>();
            foreach (var kvp in Fields)
            {
                pairs.Add($"{kvp.Key}: {kvp.Value}");
            }
            return "{" + string.Join(", ", pairs) + "}";
        }
    }

    /// <summary>
    /// Global table (_G) that proxies access to the global scope.
    /// Similar to Lua's _G table.
    /// </summary>
    public class MiniPandaGlobalTable : MiniPandaHeapObject
    {
        private readonly Environment _globalScope;

        public MiniPandaGlobalTable(Environment globalScope)
        {
            _globalScope = globalScope;
        }

        public Value Get(string key)
        {
            return _globalScope.Get(key);
        }

        public void Set(string key, Value value)
        {
            _globalScope.Define(key, value);
        }

        public bool Contains(string key) => _globalScope.Contains(key);

        public override string ToString() => "<_G>";
    }
}
