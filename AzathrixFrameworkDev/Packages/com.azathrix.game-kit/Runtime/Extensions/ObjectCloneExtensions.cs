using System;
using Sirenix.Serialization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// 对象克隆扩展方法
    /// </summary>
    public static class ObjectCloneExtensions
    {
        #region Unity 对象

        /// <summary>
        /// 实例化 Unity Object
        /// </summary>
        public static T Instantiate<T>(this T obj) where T : Object
        {
            return obj == null ? null : Object.Instantiate(obj);
        }

        /// <summary>
        /// 实例化 Unity Object（指定父对象）
        /// </summary>
        public static T Instantiate<T>(this T obj, Transform parent) where T : Object
        {
            return obj == null ? null : Object.Instantiate(obj, parent);
        }

        /// <summary>
        /// 实例化 Unity Object（指定位置和旋转）
        /// </summary>
        public static T Instantiate<T>(this T obj, Vector3 position, Quaternion rotation) where T : Object
        {
            return obj == null ? null : Object.Instantiate(obj, position, rotation);
        }

        #endregion

        #region 普通对象

        /// <summary>
        /// 使用 Unity JsonUtility 克隆对象
        /// </summary>
        public static T UnityClone<T>(this T obj)
        {
            var json = JsonUtility.ToJson(obj);
            return (T)JsonUtility.FromJson(json, obj.GetType());
        }

        /// <summary>
        /// 使用 Unity JsonUtility 覆盖对象
        /// </summary>
        public static void CloneOverwrite(this object obj, object target)
        {
            var json = JsonUtility.ToJson(obj);
            JsonUtility.FromJsonOverwrite(json, target);
        }

        /// <summary>
        /// 使用 Odin 序列化克隆对象（深拷贝）
        /// </summary>
        public static T Clone<T>(this T obj)
        {
            try
            {
                var data = SerializationUtility.SerializeValue(obj, DataFormat.Binary);
                return SerializationUtility.DeserializeValue<T>(data, DataFormat.Binary);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return default;
            }
        }

        #endregion
    }
}
