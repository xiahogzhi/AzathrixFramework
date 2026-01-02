using Azathrix.GameKit.Runtime.Builder;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Pool
{
    /// <summary>
    /// 注册构建器
    /// <para>用于自定义配置对象池的注册参数</para>
    /// </summary>
    /// <example>
    /// <code>
    /// // 基础用法
    /// RegisterBuilder.Get()
    ///     .WithKey("Bullet")
    ///     .WithPrefab(bulletPrefab)
    ///     .WithPrewarm(20)
    ///     .WithMaxSize(100)
    ///     .Build();
    ///
    /// // 保存配置复用
    /// var ctx = RegisterBuilder.Get()
    ///     .WithKey("Bullet")
    ///     .WithPrefab(bulletPrefab)
    ///     .BuildContext();
    /// </code>
    /// </example>
    public class RegisterBuilder : BuilderBase<RegisterBuilder, GameObjectPool, RegisterContext>
    {
        /// <summary>
        /// 设置池的唯一标识
        /// </summary>
        public RegisterBuilder WithKey(string key)
        {
            CheckIfDispose();
            Context.Key = key;
            return this;
        }

        /// <summary>
        /// 设置预制体
        /// </summary>
        public RegisterBuilder WithPrefab(GameObject prefab)
        {
            CheckIfDispose();
            Context.Prefab = prefab;
            return this;
        }

        /// <summary>
        /// 设置父节点
        /// </summary>
        public RegisterBuilder WithParent(Transform parent)
        {
            CheckIfDispose();
            Context.Parent = parent;
            return this;
        }

        /// <summary>
        /// 设置预热数量
        /// </summary>
        public RegisterBuilder WithPrewarm(int count)
        {
            CheckIfDispose();
            Context.PrewarmCount = count;
            return this;
        }

        /// <summary>
        /// 设置最大容量
        /// </summary>
        public RegisterBuilder WithMaxSize(int size)
        {
            CheckIfDispose();
            Context.MaxSize = size;
            return this;
        }

        protected override GameObjectPool OnBuild()
        {
            return GameObjectPoolManager.RegisterInternal(
                Context.Key,
                Context.Prefab,
                Context.Parent,
                Context.PrewarmCount,
                Context.MaxSize);
        }
    }
}
