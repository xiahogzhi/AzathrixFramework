using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Pool
{
    /// <summary>
    /// 对象池扩展方法
    /// </summary>
    public static class PoolExtensions
    {
        /// <summary>
        /// 回收到所属池（通过 PooledGameObject 组件）
        /// </summary>
        /// <param name="go">要回收的对象</param>
        /// <returns>是否成功回收</returns>
        public static bool Despawn(this GameObject go)
        {
            var pooled = GameObjectPoolManager.GetPooled(go);
            if (pooled == null || pooled.Pool == null)
            {
                Debug.LogWarning($"[Pool] GameObject '{go?.name}' is not a pooled object.");
                return false;
            }

            pooled.Pool.Despawn(go);
            return true;
        }

        /// <summary>
        /// 延迟回收到所属池
        /// </summary>
        /// <param name="go">要回收的对象</param>
        /// <param name="delay">延迟时间（秒）</param>
        public static void DespawnAfter(this GameObject go, float delay)
        {
            var pooled = GameObjectPoolManager.GetPooled(go);
            if (pooled != null && pooled.Pool != null)
            {
                pooled.DespawnAfter(delay);
            }
            else
            {
                Debug.LogWarning($"[Pool] GameObject '{go?.name}' is not a pooled object.");
            }
        }

        /// <summary>
        /// 延迟回收到 GameObjectPool
        /// </summary>
        public static async void DespawnAfter(this GameObjectPool pool, GameObject go, float delay)
        {
            if (pool == null || go == null) return;

            var pooled = GameObjectPoolManager.GetPooled(go);
            if (pooled == null || !pooled.IsSpawned) return;

            var cancelled = await UniTask.Delay((int)(delay * 1000), cancellationToken: go.GetCancellationTokenOnDestroy())
                .SuppressCancellationThrow();

            // 检查是否已销毁或已回收
            if (cancelled || pooled == null || !pooled.IsSpawned) return;
            pool.Despawn(go);
        }
    }

    /// <summary>
    /// 自动回收组件 - 挂载后会在指定时间后自动回收到所属池
    /// </summary>
    public class AutoDespawn : MonoBehaviour, IPoolable
    {
        [Tooltip("延迟回收时间（秒），0 表示不自动回收")]
        public float delay = 0f;

        private bool _scheduled;
        private PooledGameObject _pooled;

        private void Awake()
        {
            _pooled = GetComponent<PooledGameObject>();
        }

        public void OnSpawn()
        {
            _scheduled = false;
            if (delay > 0f)
                ScheduleDespawn();
        }

        public void OnDespawn()
        {
            _scheduled = false;
        }

        private async void ScheduleDespawn()
        {
            if (_scheduled) return;
            _scheduled = true;

            var cancelled = await UniTask.Delay((int)(delay * 1000), cancellationToken: destroyCancellationToken)
                .SuppressCancellationThrow();

            // 检查是否已取消、已销毁或已回收
            if (cancelled || !_scheduled || _pooled == null || !_pooled.IsSpawned) return;

            _pooled.Pool?.Despawn(gameObject);
        }

        /// <summary>
        /// 手动触发延迟回收
        /// </summary>
        public void DespawnAfter(float delayTime)
        {
            delay = delayTime;
            ScheduleDespawn();
        }
    }
}
