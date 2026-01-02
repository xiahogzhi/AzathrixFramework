using UnityEngine;
using Object = UnityEngine.Object;

namespace Azathrix.GameKit.Runtime.Builder.PrefabBuilders
{
    /// <summary>
    /// 预设构建器，用于链式配置并实例化 GameObject
    /// </summary>
    /// <example>
    /// <code>
    /// // 基础用法
    /// var go = PrefabBuilder.Get()
    ///     .SetPrefab(myPrefab)
    ///     .SetParent(parentTransform)
    ///     .SetPosition(Vector3.zero)
    ///     .Build();
    ///
    /// // 使用扩展方法加载资源
    /// var go = PrefabBuilder.Get()
    ///     .SetPrefabFromResources("Prefabs/Player")
    ///     .SetScale(Vector3.one * 2)
    ///     .Build();
    /// </code>
    /// </example>
    public class PrefabBuilder : BuilderBase<PrefabBuilder, GameObject, PrefabBuildContext>
    {
        /// <inheritdoc/>
        protected override GameObject OnBuild()
        {
            var prefab = Context.PrefabLoader?.Invoke() ?? Context.Prefab;
            if (prefab == null)
                return null;

            bool flag = prefab.activeSelf;
            prefab.SetActive(false);

            var p = Context.Parent;
            var go = Object.Instantiate(prefab, p);
            go.transform.localScale = Context.Scale;
            go.transform.rotation = Context.Rotation;

            prefab.SetActive(flag);

            switch (Context.PositionOption)
            {
                case PositionOptionEnum.None:
                    break;
                case PositionOptionEnum.Local:
                    go.transform.localPosition = Context.Position;
                    break;
                case PositionOptionEnum.World:
                    go.transform.position = Context.Position;
                    break;
            }

            go.SetActive(Context.DefaultActive);

            return go;
        }

        /// <summary>
        /// 设置父对象
        /// </summary>
        public PrefabBuilder SetParent(Transform p)
        {
            CheckIfDispose();
            Context.Parent = p;
            return this;
        }

        /// <summary>
        /// 设置预设对象
        /// </summary>
        public PrefabBuilder SetPrefab(GameObject go)
        {
            CheckIfDispose();
            Context.Prefab = go;
            return this;
        }

        /// <summary>
        /// 设置旋转
        /// </summary>
        public PrefabBuilder SetRotate(Quaternion p)
        {
            CheckIfDispose();
            Context.Rotation = p;
            return this;
        }

        /// <summary>
        /// 设置缩放
        /// </summary>
        public PrefabBuilder SetScale(Vector3 p)
        {
            CheckIfDispose();
            Context.Scale = p;
            return this;
        }

        /// <summary>
        /// 设置位置
        /// </summary>
        /// <param name="p">位置坐标</param>
        /// <param name="option">坐标类型（本地/世界）</param>
        public PrefabBuilder SetPosition(Vector3 p, PositionOptionEnum option = PositionOptionEnum.World)
        {
            CheckIfDispose();
            Context.Position = p;
            Context.PositionOption = option;
            return this;
        }

        /// <summary>
        /// 设置实例化后的默认激活状态
        /// </summary>
        public PrefabBuilder SetDefaultActive(bool flag)
        {
            CheckIfDispose();
            Context.DefaultActive = flag;
            return this;
        }
    }
}