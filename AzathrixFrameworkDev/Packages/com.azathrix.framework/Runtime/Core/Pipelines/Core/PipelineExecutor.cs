using System;
using System.Collections.Generic;
using System.Reflection;
using Azathrix.Framework.Core.Configs;
using Azathrix.Framework.Interfaces;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azathrix.Framework.Core.Pipelines
{
    /// <summary>
    /// 管线执行器
    /// </summary>
    public class PipelineExecutor
    {
        private readonly ILogger _logger;
        private readonly ScannerConfig _config;
        private readonly PhaseScanner _scanner;

        private List<IPhase> _phases;
        private Dictionary<Type, List<object>> _beforeHooks;
        private Dictionary<Type, List<object>> _afterHooks;

        /// <summary>
        /// 静默模式（不输出日志）
        /// </summary>
        public bool SilentMode { get; set; }

        public PipelineExecutor(ILogger logger, ScannerConfig config)
        {
            _logger = logger;
            _config = config;
            _scanner = new PhaseScanner(logger, config);
        }

        /// <summary>
        /// 刷新阶段和钩子（重新扫描）
        /// </summary>
        public void Refresh<TMarker>() where TMarker : IPhase
        {
            _phases = _scanner.ScanPhases<TMarker>();
            var (before, after) = _scanner.ScanAllHooks();
            _beforeHooks = before;
            _afterHooks = after;
        }

        /// <summary>
        /// 执行管线
        /// </summary>
        public async UniTask ExecuteAsync<TMarker>(PhaseContext context) where TMarker : IPhase
        {
            if (_phases == null)
                Refresh<TMarker>();

            foreach (var phase in _phases)
            {
                if (context.Aborted)
                {
                    if (!SilentMode)
                        Log.Warning($"[Pipeline] 管线已中断，跳过阶段: {phase.GetType().Name}");
                    break;
                }

                var phaseType = phase.GetType();
                var phaseName = phaseType.Name;

                // 执行前置钩子
                if (!await ExecuteBeforeHooksAsync(phase, context))
                {
                    if (!SilentMode)
                        Log.Warning($"[Pipeline] 阶段 {phaseName} 被前置钩子中断");
                    context.Aborted = true;
                    break;
                }

                // 执行阶段
                try
                {
                    if (!SilentMode)
                        Log.Info($"[Pipeline] 执行阶段: {phaseName}");
                    await phase.ExecuteAsync(context);
                }
                catch (Exception e)
                {
                    Log.Error($"[Pipeline] 阶段 {phaseName} 执行失败: {e}");
                    context.Aborted = true;
                    break;
                }

                // 执行后置钩子
                await ExecuteAfterHooksAsync(phase, context);
            }
        }

        private async UniTask<bool> ExecuteBeforeHooksAsync(IPhase phase, PhaseContext context)
        {
            var phaseType = phase.GetType();

            // 查找所有匹配的钩子（包括接口类型）
            foreach (var iface in GetPhaseInterfaces(phaseType))
            {
                if (!_beforeHooks.TryGetValue(iface, out var hooks))
                    continue;

                foreach (var hook in hooks)
                {
                    try
                    {
                        var method = hook.GetType().GetMethod("OnBeforeAsync");
                        var task = (UniTask<bool>)method.Invoke(hook, new object[] { context });
                        if (!await task)
                            return false;
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[Pipeline] 前置钩子 {hook.GetType().Name} 执行失败: {e}");
                    }
                }
            }

            return true;
        }

        private async UniTask ExecuteAfterHooksAsync(IPhase phase, PhaseContext context)
        {
            var phaseType = phase.GetType();

            foreach (var iface in GetPhaseInterfaces(phaseType))
            {
                if (!_afterHooks.TryGetValue(iface, out var hooks))
                    continue;

                foreach (var hook in hooks)
                {
                    try
                    {
                        var method = hook.GetType().GetMethod("OnAfterAsync");
                        var task = (UniTask)method.Invoke(hook, new object[] { context });
                        await task;
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[Pipeline] 后置钩子 {hook.GetType().Name} 执行失败: {e}");
                    }
                }
            }
        }

        private IEnumerable<Type> GetPhaseInterfaces(Type phaseType)
        {
            foreach (var iface in phaseType.GetInterfaces())
            {
                if (typeof(IPhase).IsAssignableFrom(iface) && iface != typeof(IPhase))
                    yield return iface;
            }
        }
    }
}
