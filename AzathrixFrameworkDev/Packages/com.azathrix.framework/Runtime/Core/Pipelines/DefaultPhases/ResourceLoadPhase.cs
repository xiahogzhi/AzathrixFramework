using Azathrix.Framework.Core.Pipelines.Phases;
using Cysharp.Threading.Tasks;

namespace Azathrix.Framework.Core.Pipelines.DefaultPhases
{
    /// <summary>
    /// 资源加载阶段（空实现，供用户扩展）
    /// </summary>
    [PhaseOrder(0)]
    public class ResourceLoadPhase : IResourceLoadPhase
    {
        public UniTask ExecuteAsync(PhaseContext context)
        {
            return UniTask.CompletedTask;
        }
    }
}
