using UnityEngine;

namespace Azathrix.GameKit.Runtime.Utils
{
    /// <summary>
    /// GameObject 工具类
    /// </summary>
    public static class GameObjectUtils
    {
        /// <summary>
        /// 创建 GameObject
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="parent">父对象</param>
        /// <param name="single">是否单例（如果已存在同名对象则返回现有对象）</param>
        /// <returns>创建的 GameObject</returns>
        public static GameObject Create(string name, Transform parent, bool single = false)
        {
            if (single)
            {
                var existing = parent.Find(name);
                if (existing) return existing.gameObject;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = Vector3.zero;
            return go;
        }

        /// <summary>
        /// 创建空 GameObject
        /// </summary>
        public static GameObject CreateEmpty(string name = "GameObject")
        {
            return new GameObject(name);
        }

        /// <summary>
        /// 创建带组件的 GameObject
        /// </summary>
        public static GameObject CreateWithComponent<T>(string name = null) where T : Component
        {
            var go = new GameObject(name ?? typeof(T).Name);
            go.AddComponent<T>();
            return go;
        }
    }
}
