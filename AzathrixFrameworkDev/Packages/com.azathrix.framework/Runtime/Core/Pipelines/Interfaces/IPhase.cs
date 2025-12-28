using Cysharp.Threading.Tasks;

namespace Azathrix.Framework.Core.Pipelines
{
    /// <summary>
    /// 阶段基础接口
    /// </summary>
    public interface IPhase
    {
        UniTask ExecuteAsync(PhaseContext context);
    }
}
