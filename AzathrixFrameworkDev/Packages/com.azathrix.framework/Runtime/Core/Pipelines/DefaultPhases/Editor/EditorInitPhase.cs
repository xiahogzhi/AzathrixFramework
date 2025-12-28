#if UNITY_EDITOR
using System;
using Azathrix.Framework.Core.Pipelines.Phases.Editor;
using Azathrix.Framework.Interfaces.SystemEvents;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Azathrix.Framework.Core.Pipelines.DefaultPhases.Editor
{
    /// <summary>
    /// 编辑器初始化阶段
    /// </summary>
    [PhaseOrder(500)]
    public class EditorInitPhase : IEditorInitPhase
    {
        public UniTask ExecuteAsync(PhaseContext context)
        {
            var runtimeManager = AzathrixFramework.EditorRuntimeManager;
            if (runtimeManager == null)
                return UniTask.CompletedTask;

            foreach (var system in runtimeManager.GetAllSystems())
            {
                if (system is ISystemEditorSupport editorSupport)
                {
                    try 
                    { 
                        editorSupport.OnEditorInitialize();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }

            return UniTask.CompletedTask;
        }
    }
}
#endif
