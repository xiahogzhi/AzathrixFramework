using Azathrix.Framework.Core;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Builder.PrefabBuilders
{
    /// <summary>
    /// PrefabBuilder 扩展方法
    /// <para>提供不同资源加载方式的支持</para>
    /// </summary>
    public static class PrefabBuilderExtensions
    {
        /// <summary>
        /// 从 Framework Resources 文件夹加载预设
        /// </summary>
        /// <param name="builder">构建器</param>
        /// <param name="path">Resources 相对路径</param>
        public static PrefabBuilder SetPrefabResources(this PrefabBuilder builder, string path)
        {
            builder.Context.Prefab = AzathrixFramework.ResourcesLoader.Load<GameObject>(path);
            return builder;
        }
    }
}
