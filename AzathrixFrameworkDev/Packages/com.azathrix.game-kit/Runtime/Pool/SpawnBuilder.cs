using Azathrix.GameKit.Runtime.Builder;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Pool
{
    /// <summary>
    /// 生成构建器
    /// </summary>
    /// <example>
    /// <code>
    /// // 基础用法
    /// var bullet = SpawnBuilder.Get()
    ///     .WithKey("Bullet")
    ///     .WithPosition(pos)
    ///     .Build();
    ///
    /// // 保存配置复用
    /// var ctx = SpawnBuilder.Get()
    ///     .WithKey("Bullet")
    ///     .WithPosition(pos)
    ///     .BuildContext();
    ///
    /// var bullet1 = SpawnBuilder.Get(ctx).Build();
    /// var bullet2 = SpawnBuilder.Get(ctx).Build();
    /// </code>
    /// </example>
    public class SpawnBuilder : BuilderBase<SpawnBuilder, GameObject, SpawnContext>
    {
        public SpawnBuilder WithKey(string key)
        {
            CheckIfDispose();
            Context.Key = key;
            return this;
        }

        public SpawnBuilder WithPosition(Vector3 position)
        {
            CheckIfDispose();
            Context.Position = position;
            return this;
        }

        public SpawnBuilder WithRotation(Quaternion rotation)
        {
            CheckIfDispose();
            Context.Rotation = rotation;
            return this;
        }

        public SpawnBuilder WithParent(Transform parent)
        {
            CheckIfDispose();
            Context.Parent = parent;
            return this;
        }

        public SpawnBuilder WithLocalScale(Vector3 scale)
        {
            CheckIfDispose();
            Context.LocalScale = scale;
            return this;
        }

        protected override GameObject OnBuild()
        {
            return GameObjectPoolManager.SpawnInternal(
                Context.Key,
                Context.Position,
                Context.Rotation,
                Context.Parent,
                Context.LocalScale);
        }

        /// <summary>
        /// 构建并获取组件
        /// </summary>
        public T Build<T>(bool autoDispose = true) where T : Component
        {
            var go = Build(autoDispose);
            return go != null ? go.GetComponent<T>() : null;
        }
    }
}
