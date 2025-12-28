using Azathrix.Framework.Core.Pipelines.Phases;
using Cysharp.Threading.Tasks;

namespace Azathrix.Framework.Core.Pipelines.DefaultPhases
{
    /// <summary>
    /// 程序集加载阶段（空实现，供HybridCLR等扩展）
    /// </summary>
    [PhaseOrder(100)]
    public class AssemblyLoadPhase : IAssemblyLoadPhase
    {
        public UniTask ExecuteAsync(PhaseContext context)
        {
            return UniTask.CompletedTask;
        }
    }
}
