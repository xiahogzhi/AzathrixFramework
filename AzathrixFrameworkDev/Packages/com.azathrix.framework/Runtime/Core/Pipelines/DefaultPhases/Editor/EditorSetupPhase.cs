#if UNITY_EDITOR
using Azathrix.Framework.Core.Pipelines.Phases.Editor;
using Azathrix.Framework.Settings;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azathrix.Framework.Core.Pipelines.DefaultPhases.Editor
{
    /// <summary>
    /// 编辑器配置阶段
    /// </summary>
    [PhaseOrder(200)]
    public class EditorSetupPhase : IEditorSetupPhase
    {
        public UniTask ExecuteAsync(PhaseContext context)
        {
            context.Logger ??= new DefaultLogger();
            context.ResourcesLoader ??= new DefaultResourcesLoader();

            var settings = AzathrixFrameworkSettings.Instance;
            AzathrixFramework.SetupInternal(
                context.Logger,
                context.ResourcesLoader,
                settings.ToScannerConfig(),
                settings.ToRuntimeConfig()
            );

            return UniTask.CompletedTask;
        }
    }
}
#endif
