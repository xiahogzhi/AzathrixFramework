using Cysharp.Threading.Tasks;

namespace Azathrix.Framework.Core.Pipelines
{
    /// <summary>
    /// 钩子执行结果
    /// </summary>
    public enum HookResult
    {
        /// <summary>继续执行</summary>
        Continue,
        /// <summary>跳过当前阶段，继续后续阶段</summary>
        SkipPhase,
        /// <summary>中断整个管线</summary>
        Abort
    }

    /// <summary>
    /// 阶段执行前钩子
    /// </summary>
    /// <typeparam name="TPhase">目标阶段类型</typeparam>
    public interface IBeforePhaseHook<TPhase> where TPhase : IPhase
    {
        int Order { get; }
        /// <summary>
        /// 阶段执行前调用
        /// </summary>
        /// <returns>Continue继续执行, SkipPhase跳过当前阶段, Abort中断管线</returns>
        UniTask<HookResult> OnBeforeAsync(PhaseContext context);
    }

    /// <summary>
    /// 阶段执行后钩子
    /// </summary>
    /// <typeparam name="TPhase">目标阶段类型</typeparam>
    public interface IAfterPhaseHook<TPhase> where TPhase : IPhase
    {
        int Order { get; }
        UniTask OnAfterAsync(PhaseContext context);
    }
}
