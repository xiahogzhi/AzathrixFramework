using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Pool
{
    /// <summary>
    /// GameObject 对象池
    /// <para>专门用于管理 Unity GameObject 的复用</para>
    /// </summary>
    /// <example>
    /// <code>
    /// // 创建池
    /// var bulletPool = new GameObjectPool(bulletPrefab, poolParent, maxSize: 100);
    ///
    /// // 预热（可选）
    /// bulletPool.Prewarm(20);
    ///
    /// // 生成对象
    /// var bullet = bulletPool.Spawn(spawnPos, Quaternion.identity);
    ///
    /// // 生成并获取组件
    /// var bulletComp = bulletPool.Spawn&lt;Bullet&gt;(spawnPos, Quaternion.identity);
    ///
    /// // 回收对象
    /// bulletPool.Despawn(bullet);
    ///
    /// // 清空池（销毁所有对象）
    /// bulletPool.Clear();
    /// </code>
    /// </example>
    public class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Stack<GameObject> _pool = new();
        private readonly HashSet<GameObject> _pooledSet = new();
        private readonly Dictionary<GameObject, PooledGameObject> _pooledObjects = new();
        private readonly int _maxSize;

        /// <summary>池中空闲对象数量</summary>
        public int CountInactive => _pool.Count;

        /// <summary>池中活跃对象数量</summary>
        public int CountActive => CountAll - CountInactive;

        /// <summary>池创建的总对象数量</summary>
        public int CountAll { get; private set; }

        /// <summary>
        /// 创建 GameObject 对象池
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="parent">池对象的父节点（可选）</param>
        /// <param name="maxSize">池的最大容量，超出后归还的对象将被销毁</param>
        public GameObjectPool(GameObject prefab, Transform parent = null, int maxSize = 1000)
        {
            _prefab = prefab;
            _parent = parent;
            _maxSize = maxSize;
        }

        /// <summary>
        /// 从池中获取 GameObject
        /// </summary>
        /// <param name="position">生成位置</param>
        /// <param name="rotation">生成旋转</param>
        /// <returns>激活的 GameObject</returns>
        public GameObject Spawn(Vector3 position = default, Quaternion rotation = default)
        {
            GameObject go = null;

            // 从池中取出有效对象，跳过已销毁的
            while (_pool.Count > 0)
            {
                var candidate = _pool.Pop();
                _pooledSet.Remove(candidate);

                if (candidate != null)
                {
                    go = candidate;
                    break;
                }
            }

            // 池中没有有效对象，创建新的
            if (go == null)
            {
                go = Object.Instantiate(_prefab, position, rotation, _parent);
                RegisterPooledObject(go);
                CountAll++;
            }
            else
            {
                go.transform.SetPositionAndRotation(position, rotation);
            }

            go.SetActive(true);

            // 调用缓存的 IPoolable 组件
            if (_pooledObjects.TryGetValue(go, out var pooledObj))
            {
                pooledObj.IsSpawned = true;
                pooledObj.NotifySpawn();
            }

            return go;
        }

        /// <summary>
        /// 注册池化对象，添加销毁监听
        /// </summary>
        private void RegisterPooledObject(GameObject go)
        {
            var pooledObj = go.AddComponent<PooledGameObject>();
            pooledObj.Pool = this;
            pooledObj.Initialize();
            pooledObj.OnDestroyCallback = OnPooledObjectDestroyed;
            _pooledObjects[go] = pooledObj;
        }

        /// <summary>
        /// 池化对象被销毁时的回调
        /// </summary>
        private void OnPooledObjectDestroyed(GameObject go)
        {
            _pooledSet.Remove(go);
            _pooledObjects.Remove(go);
            CountAll--;
        }

        /// <summary>
        /// 从池中获取 GameObject 并返回指定组件
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="position">生成位置</param>
        /// <param name="rotation">生成旋转</param>
        /// <returns>指定类型的组件</returns>
        public T Spawn<T>(Vector3 position = default, Quaternion rotation = default) where T : Component
        {
            return Spawn(position, rotation).GetComponent<T>();
        }

        /// <summary>
        /// 从池中获取 GameObject 并设置父节点
        /// </summary>
        /// <param name="parent">父节点</param>
        /// <param name="position">生成位置（世界坐标）</param>
        /// <param name="rotation">生成旋转</param>
        /// <returns>激活的 GameObject</returns>
        public GameObject Spawn(Transform parent, Vector3 position = default, Quaternion rotation = default)
        {
            var go = Spawn(position, rotation);
            go.transform.SetParent(parent);
            return go;
        }

        /// <summary>
        /// 将 GameObject 归还到池中
        /// <para>对象会被禁用并移动到池父节点下</para>
        /// </summary>
        /// <param name="go">要归还的 GameObject</param>
        public void Despawn(GameObject go)
        {
            // 安全检查：对象为空或已销毁
            if (go == null) return;

            // 防止重复回收
            if (_pooledSet.Contains(go)) return;

            // 调用缓存的 IPoolable 组件
            if (_pooledObjects.TryGetValue(go, out var pooledObj))
            {
                pooledObj.IsSpawned = false;
                pooledObj.NotifyDespawn();
            }

            go.SetActive(false);
            go.transform.SetParent(_parent);

            if (_pool.Count < _maxSize)
            {
                _pool.Push(go);
                _pooledSet.Add(go);
            }
            else
            {
                _pooledObjects.Remove(go);
                Object.Destroy(go);
            }
        }

        /// <summary>
        /// 检查对象是否在池中
        /// </summary>
        public bool IsPooled(GameObject go)
        {
            return go != null && _pooledSet.Contains(go);
        }

        /// <summary>
        /// 预热池，预先创建指定数量的对象
        /// <para>适合在加载界面调用，避免运行时卡顿</para>
        /// </summary>
        /// <param name="count">预创建数量</param>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count && _pool.Count < _maxSize; i++)
            {
                var go = Object.Instantiate(_prefab, _parent);
                go.SetActive(false);
                RegisterPooledObject(go);
                CountAll++;
                _pool.Push(go);
                _pooledSet.Add(go);
            }
        }

        /// <summary>
        /// 异步预热池，分帧创建避免卡顿
        /// </summary>
        /// <param name="count">预创建数量</param>
        /// <param name="countPerFrame">每帧创建数量</param>
        public async UniTask PrewarmAsync(int count, int countPerFrame = 5)
        {
            int created = 0;
            while (created < count && _pool.Count < _maxSize)
            {
                int batch = System.Math.Min(countPerFrame, count - created);
                for (int i = 0; i < batch && _pool.Count < _maxSize; i++)
                {
                    var go = Object.Instantiate(_prefab, _parent);
                    go.SetActive(false);
                    RegisterPooledObject(go);
                    CountAll++;
                    _pool.Push(go);
                    _pooledSet.Add(go);
                    created++;
                }
                await UniTask.Yield();
            }
        }

        /// <summary>
        /// 清空池，销毁所有池中的对象
        /// </summary>
        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var go = _pool.Pop();
                if (go != null)
                    Object.Destroy(go);
            }
            _pooledSet.Clear();
            _pooledObjects.Clear();
            CountAll = 0;
        }
    }
}
