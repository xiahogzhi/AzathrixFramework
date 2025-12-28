using UnityEngine;

namespace Azathrix.Framework.Core
{
    /// <summary>
    /// Runtime 的 MonoBehaviour 载体，负责转发 Unity 生命周期事件
    /// </summary>
    public class FrameworkBehaviour : MonoBehaviour
    {
        private SystemRuntimeManager _runtimeManager;

        /// <summary>
        /// 初始化运行时行为组件
        /// </summary>
        /// <param name="runtimeManager">游戏系统运行时实例</param>
        public void Initialize(SystemRuntimeManager runtimeManager)
        {
            _runtimeManager = runtimeManager;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            _runtimeManager?.Update(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            _runtimeManager?.FixedUpdate(Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            _runtimeManager?.LateUpdate(Time.deltaTime);
        }

        private void OnApplicationFocus(bool focus)
        {
            _runtimeManager?.OnApplicationFocus(focus);
        }

        private void OnApplicationPause(bool pause)
        {
            _runtimeManager?.OnApplicationPause(pause);
        }

        private void OnApplicationQuit()
        {
            _runtimeManager?.OnApplicationQuit();
        }
    }
}
