using UnityEngine;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// Transform 扩展方法
    /// </summary>
    public static class TransformExtensions
    {
        #region 路径

        /// <summary>
        /// 获取 Transform 的完整路径
        /// </summary>
        public static string GetTransformPath(this Transform p)
        {
            string path = p.gameObject.name;
            while (p.parent != null)
            {
                p = p.parent;
                path = p.gameObject.name + "/" + path;
            }
            return path;
        }

        #endregion

        #region 位置

        /// <summary>
        /// 设置世界坐标 X
        /// </summary>
        public static void SetPositionX(this Transform t, float x)
        {
            var pos = t.position;
            pos.x = x;
            t.position = pos;
        }

        /// <summary>
        /// 设置世界坐标 Y
        /// </summary>
        public static void SetPositionY(this Transform t, float y)
        {
            var pos = t.position;
            pos.y = y;
            t.position = pos;
        }

        /// <summary>
        /// 设置世界坐标 Z
        /// </summary>
        public static void SetPositionZ(this Transform t, float z)
        {
            var pos = t.position;
            pos.z = z;
            t.position = pos;
        }

        /// <summary>
        /// 设置本地坐标 X
        /// </summary>
        public static void SetLocalPositionX(this Transform t, float x)
        {
            var pos = t.localPosition;
            pos.x = x;
            t.localPosition = pos;
        }

        /// <summary>
        /// 设置本地坐标 Y
        /// </summary>
        public static void SetLocalPositionY(this Transform t, float y)
        {
            var pos = t.localPosition;
            pos.y = y;
            t.localPosition = pos;
        }

        /// <summary>
        /// 设置本地坐标 Z
        /// </summary>
        public static void SetLocalPositionZ(this Transform t, float z)
        {
            var pos = t.localPosition;
            pos.z = z;
            t.localPosition = pos;
        }

        #endregion

        #region 重置

        /// <summary>
        /// 重置本地变换
        /// </summary>
        public static void ResetLocal(this Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        /// <summary>
        /// 重置本地位置
        /// </summary>
        public static void ResetLocalPosition(this Transform t)
        {
            t.localPosition = Vector3.zero;
        }

        /// <summary>
        /// 重置本地旋转
        /// </summary>
        public static void ResetLocalRotation(this Transform t)
        {
            t.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// 重置本地缩放
        /// </summary>
        public static void ResetLocalScale(this Transform t)
        {
            t.localScale = Vector3.one;
        }

        #endregion

        #region 子对象

        /// <summary>
        /// 销毁所有子对象
        /// </summary>
        public static void DestroyChildren(this Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Object.Destroy(t.GetChild(i).gameObject);
        }

        /// <summary>
        /// 立即销毁所有子对象
        /// </summary>
        public static void DestroyChildrenImmediate(this Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(t.GetChild(i).gameObject);
        }

        /// <summary>
        /// 设置所有子对象的激活状态
        /// </summary>
        public static void SetChildrenActive(this Transform t, bool active)
        {
            for (int i = 0; i < t.childCount; i++)
                t.GetChild(i).gameObject.SetActive(active);
        }

        #endregion

        #region 区域设置

        /// <summary>
        /// 设置为房间区域大小
        /// </summary>
        public static void SetAsRoomAreaSize(this Transform transform, Rect rect, float top, float bottom, float left, float right, Vector2 scale)
        {
            transform.position = rect.center + new Vector2(-left + right, +top - bottom);
            transform.localScale = new Vector3(
                rect.size.x * scale.x + left * 2 + right * 2,
                rect.size.y * scale.y + top * 2 + bottom * 2,
                1
            );
        }

        #endregion

        #region 朝向/移动

        /// <summary>
        /// 2D 朝向目标点
        /// </summary>
        public static void LookAt2D(this Transform t, Vector2 target)
        {
            Vector2 dir = target - (Vector2)t.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            t.rotation = Quaternion.Euler(0, 0, angle);
        }

        /// <summary>
        /// 2D 朝向目标 Transform
        /// </summary>
        public static void LookAt2D(this Transform t, Transform target)
        {
            if (target != null)
                t.LookAt2D(target.position);
        }

        /// <summary>
        /// 设置父级并重置本地变换
        /// </summary>
        public static void SetParentAndReset(this Transform t, Transform parent)
        {
            t.SetParent(parent);
            t.ResetLocal();
        }

        /// <summary>
        /// 向目标位置移动
        /// </summary>
        public static void MoveTowards(this Transform t, Vector3 target, float maxDelta)
        {
            t.position = Vector3.MoveTowards(t.position, target, maxDelta);
        }

        /// <summary>
        /// 向目标位置移动（本地坐标）
        /// </summary>
        public static void MoveTowardsLocal(this Transform t, Vector3 target, float maxDelta)
        {
            t.localPosition = Vector3.MoveTowards(t.localPosition, target, maxDelta);
        }

        #endregion
    }
}
