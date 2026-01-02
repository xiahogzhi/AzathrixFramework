using UnityEngine;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// LayerMask 扩展方法
    /// </summary>
    public static class LayerMaskExtensions
    {
        /// <summary>
        /// 检查 LayerMask 是否包含指定层
        /// </summary>
        public static bool Contains(this LayerMask mask, int layer)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        /// <summary>
        /// 检查 LayerMask 是否包含指定层（按名称）
        /// </summary>
        public static bool Contains(this LayerMask mask, string layerName)
        {
            return mask.Contains(LayerMask.NameToLayer(layerName));
        }

        /// <summary>
        /// 添加层到 LayerMask
        /// </summary>
        public static LayerMask Add(this LayerMask mask, int layer)
        {
            return mask | (1 << layer);
        }

        /// <summary>
        /// 添加层到 LayerMask（按名称）
        /// </summary>
        public static LayerMask Add(this LayerMask mask, string layerName)
        {
            return mask.Add(LayerMask.NameToLayer(layerName));
        }

        /// <summary>
        /// 从 LayerMask 移除层
        /// </summary>
        public static LayerMask Remove(this LayerMask mask, int layer)
        {
            return mask & ~(1 << layer);
        }

        /// <summary>
        /// 从 LayerMask 移除层（按名称）
        /// </summary>
        public static LayerMask Remove(this LayerMask mask, string layerName)
        {
            return mask.Remove(LayerMask.NameToLayer(layerName));
        }

        /// <summary>
        /// 切换层的包含状态
        /// </summary>
        public static LayerMask Toggle(this LayerMask mask, int layer)
        {
            return mask ^ (1 << layer);
        }

        /// <summary>
        /// 从层名称数组创建 LayerMask
        /// </summary>
        public static LayerMask FromNames(params string[] layerNames)
        {
            return LayerMask.GetMask(layerNames);
        }

        /// <summary>
        /// 从层索引数组创建 LayerMask
        /// </summary>
        public static LayerMask FromLayers(params int[] layers)
        {
            int mask = 0;
            foreach (int layer in layers)
                mask |= 1 << layer;
            return mask;
        }
    }
}
