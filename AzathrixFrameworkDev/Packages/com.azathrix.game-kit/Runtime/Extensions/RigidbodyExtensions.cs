using UnityEngine;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// Rigidbody 扩展方法
    /// </summary>
    public static class RigidbodyExtensions
    {
        #region Rigidbody2D

        /// <summary>
        /// 设置速度 X
        /// </summary>
        public static void SetVelocityX(this Rigidbody2D rb, float x)
        {
            rb.linearVelocity = new Vector2(x, rb.linearVelocity.y);
        }

        /// <summary>
        /// 设置速度 Y
        /// </summary>
        public static void SetVelocityY(this Rigidbody2D rb, float y)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, y);
        }

        /// <summary>
        /// 添加速度 X
        /// </summary>
        public static void AddVelocityX(this Rigidbody2D rb, float x)
        {
            rb.linearVelocity += new Vector2(x, 0);
        }

        /// <summary>
        /// 添加速度 Y
        /// </summary>
        public static void AddVelocityY(this Rigidbody2D rb, float y)
        {
            rb.linearVelocity += new Vector2(0, y);
        }

        /// <summary>
        /// 停止移动
        /// </summary>
        public static void Stop(this Rigidbody2D rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        /// <summary>
        /// 冻结位置
        /// </summary>
        public static void FreezePosition(this Rigidbody2D rb)
        {
            rb.constraints = RigidbodyConstraints2D.FreezePosition;
        }

        /// <summary>
        /// 冻结旋转
        /// </summary>
        public static void FreezeRotation(this Rigidbody2D rb)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        /// <summary>
        /// 冻结全部
        /// </summary>
        public static void FreezeAll(this Rigidbody2D rb)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        /// <summary>
        /// 解除冻结
        /// </summary>
        public static void Unfreeze(this Rigidbody2D rb)
        {
            rb.constraints = RigidbodyConstraints2D.None;
        }

        #endregion

        #region Rigidbody (3D)

        /// <summary>
        /// 设置速度 X
        /// </summary>
        public static void SetVelocityX(this Rigidbody rb, float x)
        {
            rb.linearVelocity = new Vector3(x, rb.linearVelocity.y, rb.linearVelocity.z);
        }

        /// <summary>
        /// 设置速度 Y
        /// </summary>
        public static void SetVelocityY(this Rigidbody rb, float y)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, y, rb.linearVelocity.z);
        }

        /// <summary>
        /// 设置速度 Z
        /// </summary>
        public static void SetVelocityZ(this Rigidbody rb, float z)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y, z);
        }

        /// <summary>
        /// 停止移动
        /// </summary>
        public static void Stop(this Rigidbody rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// 冻结位置
        /// </summary>
        public static void FreezePosition(this Rigidbody rb)
        {
            rb.constraints = RigidbodyConstraints.FreezePosition;
        }

        /// <summary>
        /// 冻结旋转
        /// </summary>
        public static void FreezeRotation(this Rigidbody rb)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        /// <summary>
        /// 冻结全部
        /// </summary>
        public static void FreezeAll(this Rigidbody rb)
        {
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        /// <summary>
        /// 解除冻结
        /// </summary>
        public static void Unfreeze(this Rigidbody rb)
        {
            rb.constraints = RigidbodyConstraints.None;
        }

        /// <summary>
        /// 设置运动学状态
        /// </summary>
        public static void SetKinematic(this Rigidbody rb, bool isKinematic)
        {
            rb.isKinematic = isKinematic;
        }

        #endregion
    }
}
