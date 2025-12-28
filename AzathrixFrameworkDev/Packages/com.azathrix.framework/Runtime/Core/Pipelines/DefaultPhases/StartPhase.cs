using Azathrix.Framework.Core.Pipelines.Phases;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azathrix.Framework.Core.Pipelines.DefaultPhases
{
    /// <summary>
    /// 启动完成阶段
    /// </summary>
    [PhaseOrder(500)]
    public class StartPhase : IStartPhase
    {
        public UniTask ExecuteAsync(PhaseContext context)
        {
            AzathrixFramework.Dispatcher.SendDefault<SystemEventDefines.OnGameInitialized>();
            AzathrixFramework.SetStarted(true);

            Log.Separator("启动完成");
            return UniTask.CompletedTask;
        }
    }
}
