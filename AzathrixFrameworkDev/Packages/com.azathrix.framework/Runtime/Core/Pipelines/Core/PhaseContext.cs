using System;
using System.Collections.Generic;
using Azathrix.Framework.Interfaces;

namespace Azathrix.Framework.Core.Pipelines
{
    /// <summary>
    /// 阶段执行上下文
    /// </summary>
    public class PhaseContext
    {
        public ILogger Logger { get; set; }
        public IResourcesLoader ResourcesLoader { get; set; }
        public Dictionary<string, object> Data { get; } = new();

        /// <summary>
        /// 扫描到的系统类型（Scan阶段填充）
        /// </summary>
        public Type[] ScannedSystemTypes { get; set; }

        /// <summary>
        /// 是否中断管线执行
        /// </summary>
        public bool Aborted { get; set; }

        public T Get<T>(string key) => Data.TryGetValue(key, out var v) ? (T)v : default;
        public void Set(string key, object value) => Data[key] = value;
    }
}
