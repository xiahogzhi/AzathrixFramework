using System.Collections.Generic;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Pool
{
    /// <summary>
    /// GameObject 对象池管理器（静态工具类）
    /// <para>统一管理多个 GameObjectPool，支持按 key 获取池</para>
    /// </summary>
    /// <example>
    /// <code>
    /// // 简单注册
    /// GameObjectPoolManager.Register("Bullet", bulletPrefab);
    ///
    /// // Builder 注册（自定义配置）
    /// GameObjectPoolManager.GetRegisterBuilder()
    ///     .WithKey("Bullet")
    ///     .WithPrefab(bulletPrefab)
    ///     .WithPrewarm(20)
    ///     .WithMaxSize(100)
    ///     .Build();
    ///
    /// // 简单生成
    /// var bullet = GameObjectPoolManager.Spawn("Bullet", position);
    ///
    /// // Builder 生成（自定义配置）
    /// var bullet = GameObjectPoolManager.GetSpawnBuilder("Bullet")
    ///     .WithPosition(position)
    ///     .WithRotation(rotation)
    ///     .WithParent(transform)
    ///     .Build();
    ///
    /// // 回收对象
    /// GameObjectPoolManager.Despawn(bullet);
    /// </code>
    /// </example>
    public static class GameObjectPoolManager
    {
        private static readonly Dictionary<string, GameObjectPool> _pools = new();
        private static readonly Dictionary<GameObject, PooledGameObject> _cache = new();
        private static Transform _poolRoot;

        #region 池根节点

        /// <summary>
        /// 设置池的根节点
        /// <para>所有通过 PoolManager 注册的池都会在此节点下创建容器</para>
        /// </summary>
        /// <param name="root">根节点 Transform</param>
        public static void SetPoolRoot(Transform root)
        {
            _poolRoot = root;
        }

        #endregion

        #region 注册

        /// <summary>
        /// 注册对象池（简单版本）
        /// </summary>
        /// <param name="key">池的唯一标识</param>
        /// <param name="prefab">预制体</param>
        /// <param name="prewarmCount">预热数量，默认 0</param>
        /// <param name="maxSize">最大容量，默认 1000</param>
        /// <returns>创建的对象池</returns>
        public static GameObjectPool Register(string key, GameObject prefab, int prewarmCount = 0, int maxSize = 1000)
        {
            return RegisterInternal(key, prefab, null, prewarmCount, maxSize);
        }

        /// <summary>
        /// 获取注册构建器（自定义配置）
        /// </summary>
        /// <returns>注册构建器实例</returns>
        public static RegisterBuilder GetRegisterBuilder()
        {
            return RegisterBuilder.Get();
        }

        /// <summary>
        /// 内部注册方法
        /// </summary>
        internal static GameObjectPool RegisterInternal(string key, GameObject prefab, Transform parent,
            int prewarmCount, int maxSize)
        {
            if (_pools.TryGetValue(key, out var existing))
            {
                Debug.LogWarning($"[GameObjectPoolManager] Pool '{key}' already exists.");
                return existing;
            }

            // 如果没有指定 parent，使用 poolRoot 下的容器
            if (parent == null && _poolRoot != null)
            {
                var poolContainer = new GameObject($"Pool_{key}");
                poolContainer.transform.SetParent(_poolRoot);
                parent = poolContainer.transform;
            }

            var pool = new GameObjectPool(prefab, parent, maxSize);
            if (prewarmCount > 0)
                pool.Prewarm(prewarmCount);

            _pools[key] = pool;
            return pool;
        }

        #endregion

        #region 查询

        /// <summary>
        /// 获取对象池
        /// </summary>
        /// <param name="key">池的唯一标识</param>
        /// <returns>对象池，不存在返回 null</returns>
        public static GameObjectPool GetPool(string key)
        {
            return _pools.TryGetValue(key, out var pool) ? pool : null;
        }

        /// <summary>
        /// 检查池是否存在
        /// </summary>
        /// <param name="key">池的唯一标识</param>
        public static bool HasPool(string key) => _pools.ContainsKey(key);

        /// <summary>
        /// 获取所有池的 Key
        /// </summary>
        public static IEnumerable<string> GetAllPoolKeys() => _pools.Keys;

        /// <summary>
        /// 获取缓存的 PooledGameObject
        /// </summary>
        /// <param name="go">目标 GameObject</param>
        /// <returns>PooledGameObject 组件，不存在返回 null</returns>
        public static PooledGameObject GetPooled(GameObject go)
        {
            if (go == null) return null;
            return _cache.TryGetValue(go, out var pooled) ? pooled : go.GetComponent<PooledGameObject>();
        }

        #endregion

        #region 生成

        /// <summary>
        /// 从池中生成对象（简单版本）
        /// </summary>
        /// <param name="key">池的唯一标识</param>
        /// <param name="position">生成位置</param>
        /// <param name="rotation">生成旋转，默认 identity</param>
        /// <returns>生成的 GameObject</returns>
        public static GameObject Spawn(string key, Vector3 position, Quaternion rotation = default)
        {
            return SpawnInternal(key, position, rotation, null, null);
        }

        /// <summary>
        /// 从池中生成对象并获取组件（简单版本）
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="key">池的唯一标识</param>
        /// <param name="position">生成位置</param>
        /// <param name="rotation">生成旋转，默认 identity</param>
        /// <returns>指定类型的组件</returns>
        public static T Spawn<T>(string key, Vector3 position, Quaternion rotation = default) where T : Component
        {
            var go = Spawn(key, position, rotation);
            return go != null ? go.GetComponent<T>() : null;
        }

        /// <summary>
        /// 获取生成构建器（自定义配置）
        /// </summary>
        /// <param name="key">池的唯一标识</param>
        /// <returns>生成构建器实例</returns>
        public static SpawnBuilder GetSpawnBuilder(string key)
        {
            return SpawnBuilder.Get().WithKey(key);
        }

        /// <summary>
        /// 内部生成方法
        /// </summary>
        internal static GameObject SpawnInternal(string key, Vector3 position, Quaternion rotation,
            Transform parent, Vector3? localScale)
        {
            if (!_pools.TryGetValue(key, out var pool))
            {
                Debug.LogError($"[GameObjectPoolManager] Pool '{key}' not found.");
                return null;
            }

            var go = pool.Spawn(position, rotation);

            if (parent != null)
                go.transform.SetParent(parent);

            if (localScale.HasValue)
                go.transform.localScale = localScale.Value;

            // 缓存并设置 PoolKey
            var pooled = go.GetComponent<PooledGameObject>();
            if (pooled != null)
            {
                pooled.PoolKey = key;
                _cache[go] = pooled;
            }

            return go;
        }

        #endregion

        #region 回收

        /// <summary>
        /// 回收对象到指定池
        /// </summary>
        /// <param name="key">池的唯一标识</param>
        /// <param name="go">要回收的 GameObject</param>
        public static void Despawn(string key, GameObject go)
        {
            if (!_pools.TryGetValue(key, out var pool))
            {
                Debug.LogError($"[GameObjectPoolManager] Pool '{key}' not found.");
                return;
            }

            _cache.Remove(go);
            pool.Despawn(go);
        }

        /// <summary>
        /// 自动回收对象（根据缓存的池信息）
        /// </summary>
        /// <param name="go">要回收的 GameObject</param>
        public static void Despawn(GameObject go)
        {
            if (go == null) return;

            if (_cache.TryGetValue(go, out var pooled) && pooled != null && pooled.Pool != null)
            {
                _cache.Remove(go);
                pooled.Pool.Despawn(go);
            }
            else
            {
                Debug.LogWarning($"[GameObjectPoolManager] GameObject '{go.name}' not managed.");
            }
        }

        /// <summary>
        /// 回收指定池的所有活跃对象
        /// </summary>
        /// <param name="key">池的唯一标识</param>
        public static void DespawnAll(string key)
        {
            if (!_pools.TryGetValue(key, out var pool)) return;

            var toDespawn = new List<GameObject>();
            foreach (var kvp in _cache)
            {
                if (kvp.Value != null && kvp.Value.PoolKey == key && kvp.Value.IsSpawned)
                    toDespawn.Add(kvp.Key);
            }

            foreach (var go in toDespawn)
            {
                if (go != null)
                    pool.Despawn(go);
            }
        }

        #endregion

        #region 清理

        /// <summary>
        /// 清空指定池（销毁池中所有对象）
        /// </summary>
        /// <param name="key">池的唯一标识</param>
        public static void ClearPool(string key)
        {
            if (_pools.TryGetValue(key, out var pool))
                pool.Clear();
        }

        /// <summary>
        /// 清空所有池
        /// </summary>
        public static void ClearAll()
        {
            foreach (var pool in _pools.Values)
                pool.Clear();
            _cache.Clear();
        }

        /// <summary>
        /// 移除池（清空并注销）
        /// </summary>
        /// <param name="key">池的唯一标识</param>
        public static void Unregister(string key)
        {
            if (_pools.TryGetValue(key, out var pool))
            {
                pool.Clear();
                _pools.Remove(key);
            }
        }

        /// <summary>
        /// 从缓存中移除（内部使用）
        /// </summary>
        internal static void RemoveFromCache(GameObject go) => _cache.Remove(go);

        #endregion
    }
}
