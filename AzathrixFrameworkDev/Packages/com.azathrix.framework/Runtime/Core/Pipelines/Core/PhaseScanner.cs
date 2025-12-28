using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Azathrix.Framework.Core.Configs;
using Azathrix.Framework.Interfaces;
using Azathrix.Framework.Settings;

namespace Azathrix.Framework.Core.Pipelines
{
    /// <summary>
    /// 阶段和钩子扫描器
    /// </summary>
    public class PhaseScanner
    {
        private readonly ILogger _logger;
        private readonly ScannerConfig _config;

        public PhaseScanner(ILogger logger, ScannerConfig config)
        {
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// 扫描指定标记类型的阶段
        /// </summary>
        public List<IPhase> ScanPhases<TMarker>() where TMarker : IPhase
        {
            var phases = new List<(IPhase phase, int order)>();

            foreach (var assembly in GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!typeof(TMarker).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                            continue;

                        var order = type.GetCustomAttribute<PhaseOrderAttribute>()?.Order ?? 0;
                        var phase = (IPhase)Activator.CreateInstance(type);
                        phases.Add((phase, order));
                    }
                }
                catch { }
            }

            return phases.OrderBy(p => p.order).Select(p => p.phase).ToList();
        }

        /// <summary>
        /// 扫描指定阶段的前置钩子
        /// </summary>
        public List<IBeforePhaseHook<TPhase>> ScanBeforeHooks<TPhase>() where TPhase : IPhase
        {
            var hooks = new List<(IBeforePhaseHook<TPhase> hook, int order)>();

            foreach (var assembly in GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!typeof(IBeforePhaseHook<TPhase>).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                            continue;

                        var hook = (IBeforePhaseHook<TPhase>)Activator.CreateInstance(type);
                        hooks.Add((hook, hook.Order));
                    }
                }
                catch { }
            }

            return hooks.OrderBy(h => h.order).Select(h => h.hook).ToList();
        }

        /// <summary>
        /// 扫描指定阶段的后置钩子
        /// </summary>
        public List<IAfterPhaseHook<TPhase>> ScanAfterHooks<TPhase>() where TPhase : IPhase
        {
            var hooks = new List<(IAfterPhaseHook<TPhase> hook, int order)>();

            foreach (var assembly in GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!typeof(IAfterPhaseHook<TPhase>).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                            continue;

                        var hook = (IAfterPhaseHook<TPhase>)Activator.CreateInstance(type);
                        hooks.Add((hook, hook.Order));
                    }
                }
                catch { }
            }

            return hooks.OrderBy(h => h.order).Select(h => h.hook).ToList();
        }

        /// <summary>
        /// 扫描所有钩子（按阶段类型分组）
        /// </summary>
        public (Dictionary<Type, List<object>> beforeHooks, Dictionary<Type, List<object>> afterHooks) ScanAllHooks()
        {
            var beforeHooks = new Dictionary<Type, List<object>>();
            var afterHooks = new Dictionary<Type, List<object>>();

            foreach (var assembly in GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsAbstract || type.IsInterface)
                            continue;

                        foreach (var iface in type.GetInterfaces())
                        {
                            if (!iface.IsGenericType)
                                continue;

                            var genericDef = iface.GetGenericTypeDefinition();
                            var phaseType = iface.GetGenericArguments()[0];

                            if (genericDef == typeof(IBeforePhaseHook<>))
                            {
                                if (!beforeHooks.ContainsKey(phaseType))
                                    beforeHooks[phaseType] = new List<object>();
                                beforeHooks[phaseType].Add(Activator.CreateInstance(type));
                            }
                            else if (genericDef == typeof(IAfterPhaseHook<>))
                            {
                                if (!afterHooks.ContainsKey(phaseType))
                                    afterHooks[phaseType] = new List<object>();
                                afterHooks[phaseType].Add(Activator.CreateInstance(type));
                            }
                        }
                    }
                }
                catch { }
            }

            // 排序
            foreach (var key in beforeHooks.Keys.ToList())
                beforeHooks[key] = beforeHooks[key].OrderBy(h => ((dynamic)h).Order).ToList();
            foreach (var key in afterHooks.Keys.ToList())
                afterHooks[key] = afterHooks[key].OrderBy(h => ((dynamic)h).Order).ToList();

            return (beforeHooks, afterHooks);
        }

        private IEnumerable<Assembly> GetAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => ShouldScanAssembly(a));
        }

        private bool ShouldScanAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            if (_config.ExcludeAssemblyPrefixes.Any(p => name.StartsWith(p)))
                return false;

            var moduleSettings = ModuleRegistrySettings.Instance;
            if (moduleSettings != null && moduleSettings.IsAssemblyDisabled(name))
                return false;

            if (_config.AssemblyPrefixes.Count > 0)
                return _config.AssemblyPrefixes.Any(p => name.StartsWith(p));

            return true;
        }
    }
}
