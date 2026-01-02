using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Azathrix.Framework.Interfaces.DefaultSystems
{
    /// <summary>
    /// 资源系统接口，扩展基础加载功能
    /// </summary>
    public interface IResourcesSystem : IResourcesLoader
    {
        /// <summary>初始化资源系统</summary>
        UniTask InitializeAsync();

        /// <summary>释放资源</summary>
        void Release(string key);

        /// <summary>释放所有资源</summary>
        void ReleaseAll();

        /// <summary>实例化GameObject</summary>
        UniTask<GameObject> InstantiateAsync(string key, Transform parent = null);

        /// <summary>检查资源是否存在</summary>
        bool Exists(string key);
    }
}