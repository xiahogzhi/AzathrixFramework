using System;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Builder.PrefabBuilders
{
    /// <summary>
    /// 位置设置选项
    /// </summary>
    public enum PositionOptionEnum
    {
        /// <summary>不设置位置</summary>
        None,
        /// <summary>设置本地坐标</summary>
        Local,
        /// <summary>设置世界坐标</summary>
        World,
    }

    /// <summary>
    /// 预设构建上下文，存储实例化所需的所有参数
    /// </summary>
    public class PrefabBuildContext : IBuildContext<PrefabBuildContext>
    {
        /// <summary>预设对象</summary>
        public GameObject Prefab { get; set; }

        /// <summary>预设加载器，优先级高于 Prefab</summary>
        public Func<GameObject> PrefabLoader { get; set; }

        /// <summary>父对象</summary>
        public Transform Parent { get; set; }

        /// <summary>位置</summary>
        public Vector3 Position { get; set; }

        /// <summary>缩放</summary>
        public Vector3 Scale { get; set; } = Vector3.one;

        /// <summary>旋转</summary>
        public Quaternion Rotation { get; set; } = Quaternion.identity;

        /// <summary>实例化后的默认激活状态</summary>
        public bool DefaultActive { get; set; } = true;

        /// <summary>位置设置选项</summary>
        public PositionOptionEnum PositionOption { get; set; }

        /// <inheritdoc/>
        public PrefabBuildContext Clone()
        {
            return new PrefabBuildContext
            {
                Prefab = Prefab,
                PrefabLoader = PrefabLoader,
                Parent = Parent,
                Position = Position,
                Scale = Scale,
                Rotation = Rotation,
                DefaultActive = DefaultActive,
                PositionOption = PositionOption
            };
        }
    }
}
