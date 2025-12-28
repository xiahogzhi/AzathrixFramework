using Cysharp.Threading.Tasks;

namespace Azathrix.Framework.Core.Pipelines
{
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
        /// <returns>返回 false 中断阶段执行</returns>
        UniTask<bool> OnBeforeAsync(PhaseContext context);
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
