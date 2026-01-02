using System;
using System.Collections.Generic;
using System.Reflection;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// 反射扩展方法
    /// </summary>
    public static class ReflectionExtensions
    {
        private const BindingFlags DefaultFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        #region 方法调用

        /// <summary>
        /// 通过反射调用方法
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="methodName">方法名</param>
        /// <param name="parameters">参数数组</param>
        public static void Invoke(this object target, string methodName, object[] parameters = null)
        {
            var mi = target.GetType().GetMethod(methodName, DefaultFlags);
            mi?.Invoke(target, parameters);
        }

        /// <summary>
        /// 通过反射调用方法（带返回值）
        /// </summary>
        public static T Invoke<T>(this object target, string methodName, object[] parameters = null)
        {
            var mi = target.GetType().GetMethod(methodName, DefaultFlags);
            if (mi == null) return default;
            return (T)mi.Invoke(target, parameters);
        }

        /// <summary>
        /// 尝试调用方法
        /// </summary>
        public static bool TryInvoke(this object target, string methodName, object[] parameters = null)
        {
            var mi = target.GetType().GetMethod(methodName, DefaultFlags);
            if (mi == null) return false;
            mi.Invoke(target, parameters);
            return true;
        }

        #endregion

        #region 字段操作

        /// <summary>
        /// 获取当前类型及所有父类型的字段
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="flags">绑定标志</param>
        /// <returns>字段信息数组</returns>
        public static FieldInfo[] GetAllFieldInfo(this Type type, BindingFlags flags = DefaultFlags)
        {
            var allFields = new List<FieldInfo>();
            while (type != null)
            {
                allFields.AddRange(type.GetFields(flags));
                type = type.BaseType;
            }
            return allFields.ToArray();
        }

        /// <summary>
        /// 获取字段值
        /// </summary>
        public static T GetFieldValue<T>(this object target, string fieldName)
        {
            var fi = target.GetType().GetField(fieldName, DefaultFlags);
            if (fi == null) return default;
            return (T)fi.GetValue(target);
        }

        /// <summary>
        /// 设置字段值
        /// </summary>
        public static void SetFieldValue(this object target, string fieldName, object value)
        {
            var fi = target.GetType().GetField(fieldName, DefaultFlags);
            fi?.SetValue(target, value);
        }

        #endregion

        #region 属性操作

        /// <summary>
        /// 获取属性值
        /// </summary>
        public static T GetPropertyValue<T>(this object target, string propertyName)
        {
            var pi = target.GetType().GetProperty(propertyName, DefaultFlags);
            if (pi == null) return default;
            return (T)pi.GetValue(target);
        }

        /// <summary>
        /// 设置属性值
        /// </summary>
        public static void SetPropertyValue(this object target, string propertyName, object value)
        {
            var pi = target.GetType().GetProperty(propertyName, DefaultFlags);
            pi?.SetValue(target, value);
        }

        #endregion

        #region 类型检查

        /// <summary>
        /// 检查类型是否实现了指定接口
        /// </summary>
        public static bool ImplementsInterface<T>(this Type type)
        {
            return typeof(T).IsAssignableFrom(type);
        }

        /// <summary>
        /// 检查类型是否继承自指定类型
        /// </summary>
        public static bool InheritsFrom<T>(this Type type)
        {
            return typeof(T).IsAssignableFrom(type) && type != typeof(T);
        }

        #endregion
    }
}
