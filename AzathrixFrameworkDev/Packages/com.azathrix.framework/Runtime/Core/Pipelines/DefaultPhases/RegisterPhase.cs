using System.Diagnostics;
using Azathrix.Framework.Core.Pipelines.Phases;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azathrix.Framework.Core.Pipelines.DefaultPhases
{
    /// <summary>
    /// 系统注册阶段
    /// </summary>
    [PhaseOrder(400)]
    public class RegisterPhase : IRegisterPhase
    {
        public async UniTask ExecuteAsync(PhaseContext context)
        {
            Log.Separator("Register 阶段");

            var runtimeConfig = AzathrixFramework.RuntimeConfig;
            var runtimeManager = new SystemRuntimeManager();
            runtimeManager.EnableProfiling = runtimeConfig.EnableProfiling;

            if (runtimeConfig.Symbols.Count > 0)
            {
                Log.Info($"[Register] 条件符号: {string.Join(", ", runtimeConfig.Symbols)}");
                foreach (var symbol in runtimeConfig.Symbols)
                    runtimeManager.AddSymbol(symbol);
            }

            AzathrixFramework.SetRuntimeManager(runtimeManager);
            AzathrixFramework.CreateRuntimeBehaviour();

            var watch = Stopwatch.StartNew();
            await runtimeManager.CreateSystemFromTypesAsync(context.ScannedSystemTypes);
            watch.Stop();

            Log.Info($"[Register] 完成，共 {runtimeManager.GetAllSystems().Count} 个系统，耗时: {watch.Elapsed.TotalMilliseconds:F2}ms");
        }
    }
}
