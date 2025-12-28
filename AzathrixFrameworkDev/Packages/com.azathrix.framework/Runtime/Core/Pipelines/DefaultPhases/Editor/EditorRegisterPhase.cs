#if UNITY_EDITOR
using Azathrix.Framework.Core.Pipelines.Phases.Editor;
using Cysharp.Threading.Tasks;

namespace Azathrix.Framework.Core.Pipelines.DefaultPhases.Editor
{
    /// <summary>
    /// 编辑器系统注册阶段
    /// </summary>
    [PhaseOrder(400)]
    public class EditorRegisterPhase : IEditorRegisterPhase
    {
        public async UniTask ExecuteAsync(PhaseContext context)
        {
            var runtimeConfig = AzathrixFramework.RuntimeConfig;
            var runtimeManager = new SystemRuntimeManager();
            runtimeManager.IsEditorMode = true;
            runtimeManager.EnableProfiling = runtimeConfig.EnableProfiling;

            foreach (var symbol in runtimeConfig.Symbols)
                runtimeManager.AddSymbol(symbol);

            AzathrixFramework.SetEditorRuntimeManager(runtimeManager);

            await runtimeManager.CreateSystemFromTypesAsync(context.ScannedSystemTypes);
        }
    }
}
#endif
