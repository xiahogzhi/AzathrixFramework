using UnityEngine;

namespace Azathrix.Timer.Runtime
{
    /// <summary>
    /// 定时器销毁器
    /// <para>当 GameObject 销毁时自动取消绑定的定时器</para>
    /// </summary>
    internal class TimerDestroyer : MonoBehaviour
    {
        private TimerManager _manager;

        public void SetManager(TimerManager manager) => _manager = manager;

        private void OnDestroy() => _manager?.CancelAll(gameObject);
    }
}
