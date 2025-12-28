#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Azathrix.Framework.Core.Attributes;
using Azathrix.Framework.Core.Pipelines.Phases.Editor;
using Azathrix.Framework.Interfaces;
using Azathrix.Framework.Interfaces.SystemEvents;
using Azathrix.Framework.Settings;
using Cysharp.Threading.Tasks;

namespace Azathrix.Framework.Core.Pipelines.DefaultPhases.Editor
{
    /// <summary>
    /// 编辑器系统扫描阶段
    /// </summary>
    [PhaseOrder(300)]
    public class EditorScanPhase : IEditorScanPhase
    {
        public UniTask ExecuteAsync(PhaseContext context)
        {
            var config = AzathrixFramework.ScannerConfig;

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => ShouldScanAssembly(a, config))
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => IsEditorSupportSystem(t, config))
                .ToArray();

            context.ScannedSystemTypes = types;
            return UniTask.CompletedTask;
        }

        private bool ShouldScanAssembly(Assembly assembly, Configs.ScannerConfig config)
        {
            var name = assembly.GetName().Name;
            if (config.ExcludeAssemblyPrefixes.Any(p => name.StartsWith(p)))
                return false;

            var moduleSettings = ModuleRegistrySettings.Instance;
            if (moduleSettings != null && moduleSettings.IsAssemblyDisabled(name))
                return false;

            if (config.AssemblyPrefixes.Count > 0)
                return config.AssemblyPrefixes.Any(p => name.StartsWith(p));

            return true;
        }

        private bool IsEditorSupportSystem(Type type, Configs.ScannerConfig config)
        {
            if (!typeof(ISystem).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                return false;
            if (!typeof(ISystemEditorSupport).IsAssignableFrom(type))
                return false;
            if (config.RequireAutoRegister && type.GetCustomAttribute<AutoRegisterAttribute>() == null)
                return false;

            var systemSettings = SystemRegistrySettings.Instance;
            if (systemSettings != null && systemSettings.IsSystemDisabled(type))
                return false;

            return true;
        }
    }
}
#endif
