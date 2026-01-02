using Azathrix.Framework.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// GameObject 扩展方法
    /// </summary>
    public static class GameObjectExtensions
    {
        #region 资源加载

        /// <summary>
        /// 加载预制体
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <returns>预制体 GameObject</returns>
        public static GameObject LoadPrefab(this string path)
        {
            return AzathrixFramework.ResourcesLoader.Load<GameObject>(path);
        }

        /// <summary>
        /// 使用框架 ResourcesLoader 加载资源
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <typeparam name="T">资源类型</typeparam>
        /// <returns>加载的资源</returns>
        public static T LoadAsset<T>(this string path) where T : Object
        {
            return AzathrixFramework.ResourcesLoader.Load<T>(path);
        }

        #endregion

        #region 激活状态

        /// <summary>
        /// 安全设置激活状态
        /// </summary>
        public static void SetActiveSafe(this GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        /// <summary>
        /// 切换激活状态
        /// </summary>
        public static void ToggleActive(this GameObject go)
        {
            if (go != null)
                go.SetActive(!go.activeSelf);
        }

        #endregion

        #region 层级

        /// <summary>
        /// 递归设置层级
        /// </summary>
        public static void SetLayerRecursively(this GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                child.gameObject.SetLayerRecursively(layer);
        }

        /// <summary>
        /// 递归设置层级（按名称）
        /// </summary>
        public static void SetLayerRecursively(this GameObject go, string layerName)
        {
            go.SetLayerRecursively(LayerMask.NameToLayer(layerName));
        }

        #endregion

        #region 销毁

        /// <summary>
        /// 安全销毁 GameObject
        /// </summary>
        public static void DestroySafe(this GameObject go)
        {
            if (go != null)
                Object.Destroy(go);
        }

        /// <summary>
        /// 安全立即销毁 GameObject
        /// </summary>
        public static void DestroyImmediateSafe(this GameObject go)
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }

        #endregion

        #region 子对象

        /// <summary>
        /// 获取或创建子对象
        /// </summary>
        public static GameObject GetOrCreateChild(this GameObject go, string name)
        {
            var child = go.transform.Find(name);
            if (child != null)
                return child.gameObject;

            var newChild = new GameObject(name);
            newChild.transform.SetParent(go.transform);
            newChild.transform.localPosition = Vector3.zero;
            newChild.transform.localRotation = Quaternion.identity;
            newChild.transform.localScale = Vector3.one;
            return newChild;
        }

        /// <summary>
        /// 获取或创建子对象
        /// </summary>
        public static Transform GetOrCreateChild(this Transform t, string name)
        {
            return t.gameObject.GetOrCreateChild(name).transform;
        }

        /// <summary>
        /// 深度查找子对象
        /// </summary>
        public static Transform FindDeep(this Transform t, string name)
        {
            var result = t.Find(name);
            if (result != null)
                return result;

            foreach (Transform child in t)
            {
                result = child.FindDeep(name);
                if (result != null)
                    return result;
            }
            return null;
        }

        /// <summary>
        /// 深度查找子对象
        /// </summary>
        public static Transform FindDeep(this GameObject go, string name)
        {
            return go.transform.FindDeep(name);
        }

        /// <summary>
        /// 检查是否有指定组件
        /// </summary>
        public static bool HasComponent<T>(this GameObject go) where T : Component
        {
            return go.GetComponent<T>() != null;
        }

        #endregion
    }
}