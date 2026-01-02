using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// 组件扩展方法
    /// </summary>
    public static class ComponentExtensions
    {
        #region 添加组件

        /// <summary>
        /// 尝试添加组件（如果已存在则返回现有组件）
        /// </summary>
        public static T TryAddComponent<T>(this GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var p) ? p : go.AddComponent<T>();
        }

        /// <summary>
        /// 尝试添加组件（如果已存在则返回现有组件）
        /// </summary>
        public static T TryAddComponent<T>(this MonoBehaviour mo) where T : Component
        {
            return mo.TryGetComponent<T>(out var p) ? p : mo.gameObject.AddComponent<T>();
        }

        /// <summary>
        /// 尝试添加组件（按类型）
        /// </summary>
        public static Component TryAddComponent(this GameObject go, Type type)
        {
            if (go == null) return null;
            return go.TryGetComponent(type, out var p) ? p : go.AddComponent(type);
        }

        /// <summary>
        /// 获取或添加组件（别名）
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            return go.TryAddComponent<T>();
        }

        #endregion

        #region 获取组件

        /// <summary>
        /// 获取组件（带缓存）
        /// </summary>
        public static T GetComponentExt<T>(this Component self, ref T cacheField)
        {
            if (self == null) return default;
            if (cacheField == null || cacheField.Equals(null))
                cacheField = self.GetComponent<T>();
            return cacheField;
        }

        /// <summary>
        /// 获取子对象组件（带缓存）
        /// </summary>
        public static T GetComponentInChildrenExt<T>(this Component self, ref T cacheField, bool includeInactive = false)
        {
            if (self == null) return default;
            if (cacheField == null || cacheField.Equals(null))
                cacheField = self.GetComponentInChildren<T>(includeInactive);
            return cacheField;
        }

        /// <summary>
        /// 获取组件或子对象组件
        /// </summary>
        public static T GetComponentOrChild<T>(this MonoBehaviour mo)
        {
            return mo.TryGetComponent<T>(out var p) ? p : mo.GetComponentInChildren<T>(true);
        }

        /// <summary>
        /// 获取组件或子对象组件
        /// </summary>
        public static T GetComponentOrChild<T>(this GameObject go)
        {
            return go.TryGetComponent<T>(out var p) ? p : go.GetComponentInChildren<T>();
        }

        #endregion

        #region 接口获取

        /// <summary>
        /// 尝试获取接口
        /// </summary>
        public static bool TryGetInterface<T>(this Object o, out T result)
        {
            if (o == null)
            {
                result = default;
                return false;
            }

            if (o is T direct)
            {
                result = direct;
                return true;
            }

            if (o is GameObject go && go.TryGetComponent<T>(out var component))
            {
                result = component;
                return true;
            }

            result = default;
            return false;
        }

        #endregion

        #region 移除组件

        /// <summary>
        /// 安全移除组件
        /// </summary>
        public static void RemoveComponent<T>(this GameObject go) where T : Component
        {
            if (go.TryGetComponent<T>(out var component))
                Object.Destroy(component);
        }

        /// <summary>
        /// 安全移除组件（立即）
        /// </summary>
        public static void RemoveComponentImmediate<T>(this GameObject go) where T : Component
        {
            if (go.TryGetComponent<T>(out var component))
                Object.DestroyImmediate(component);
        }

        #endregion
    }
}
