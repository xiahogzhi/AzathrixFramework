using Azathrix.Framework.Core.Pipelines.Phases;
using Azathrix.Framework.Settings;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azathrix.Framework.Core.Pipelines.DefaultPhases
{
    /// <summary>
    /// 配置阶段
    /// </summary>
    [PhaseOrder(200)]
    public class SetupPhase : ISetupPhase
    {
        public UniTask ExecuteAsync(PhaseContext context)
        {
            Log.Separator("Setup 阶段");

            context.Logger ??= new DefaultLogger();
            context.ResourcesLoader ??= new DefaultResourcesLoader();

            var settings = AzathrixFrameworkSettings.Instance;
            AzathrixFramework.SetupInternal(
                context.Logger,
                context.ResourcesLoader,
                settings.ToScannerConfig(),
                settings.ToRuntimeConfig()
            );

            Log.Info("[Setup] 框架配置完成");
            Log.Info($"[Setup]   ResourcesLoader: {context.ResourcesLoader.GetType().Name}");
            Log.Info($"[Setup]   Logger: {context.Logger.GetType().Name}");

            return UniTask.CompletedTask;
        }
    }
}
