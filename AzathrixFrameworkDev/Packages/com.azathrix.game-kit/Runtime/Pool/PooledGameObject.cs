using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Pool
{
    /// <summary>
    /// 池化 GameObject 标记组件
    /// <para>自动挂载到池化对象上，缓存 IPoolable 组件并监听销毁事件</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledGameObject : MonoBehaviour
    {
        /// <summary>所属池的 Key</summary>
        public string PoolKey { get; internal set; }

        /// <summary>所属的对象池</summary>
        public GameObjectPool Pool { get; internal set; }

        /// <summary>是否已生成（未回收）</summary>
        public bool IsSpawned { get; internal set; }

        /// <summary>缓存的 IPoolable 组件</summary>
        public IPoolable[] Poolables { get; private set; }

        internal Action<GameObject> OnDestroyCallback;

        internal void Initialize()
        {
            Poolables = GetComponents<IPoolable>();
        }

        /// <summary>
        /// 调用所有 IPoolable 的 OnSpawn
        /// </summary>
        public void NotifySpawn()
        {
            if (Poolables == null) return;
            foreach (var p in Poolables)
                p.OnSpawn();
        }

        /// <summary>
        /// 调用所有 IPoolable 的 OnDespawn
        /// </summary>
        public void NotifyDespawn()
        {
            if (Poolables == null) return;
            foreach (var p in Poolables)
                p.OnDespawn();
        }

        /// <summary>
        /// 回收到所属池
        /// </summary>
        public void Despawn()
        {
            Pool?.Despawn(gameObject);
        }

        /// <summary>
        /// 延迟回收到所属池
        /// </summary>
        public async void DespawnAfter(float delay)
        {
            if (Pool == null || !IsSpawned) return;

            var cancelled = await UniTask.Delay((int)(delay * 1000), cancellationToken: destroyCancellationToken)
                .SuppressCancellationThrow();

            if (cancelled || !IsSpawned) return;
            Pool.Despawn(gameObject);
        }

        private void OnDestroy()
        {
            GameObjectPoolManager.RemoveFromCache(gameObject);
            OnDestroyCallback?.Invoke(gameObject);
            OnDestroyCallback = null;
        }
    }
}
