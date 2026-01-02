#if UNITY_EDITOR
using Azathrix.Framework.Settings;
using UnityEditor;
using UnityEngine;

namespace Azathrix.Framework.Editor.Core
{
    /// <summary>
    /// AzathrixFrameworkSettings 的自定义编辑器
    /// </summary>
    [CustomEditor(typeof(AzathrixFrameworkSettings))]
    public class AzathrixFrameworkSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty _scanMode;
        private SerializedProperty _requireAutoRegister;
        private SerializedProperty _assemblyNames;
        private SerializedProperty _excludeAssemblyPrefixes;
        private SerializedProperty _enableProfiling;
        private SerializedProperty _symbols;
        private SerializedProperty _autoInitialize;
        private SerializedProperty _enableEditorSupport;
        private SerializedProperty _systemInfoLevel;
        private SerializedProperty _debugEditorPipeline;

        private void OnEnable()
        {
            _scanMode = serializedObject.FindProperty("scanMode");
            _requireAutoRegister = serializedObject.FindProperty("requireAutoRegister");
            _assemblyNames = serializedObject.FindProperty("assemblyNames");
            _excludeAssemblyPrefixes = serializedObject.FindProperty("excludeAssemblyPrefixes");
            _enableProfiling = serializedObject.FindProperty("enableProfiling");
            _symbols = serializedObject.FindProperty("symbols");
            _autoInitialize = serializedObject.FindProperty("autoInitialize");
            _enableEditorSupport = serializedObject.FindProperty("enableEditorSupport");
            _systemInfoLevel = serializedObject.FindProperty("systemInfoLevel");
            _debugEditorPipeline = serializedObject.FindProperty("debugEditorPipeline");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 扫描配置
            // EditorGUILayout.LabelField("扫描配置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_scanMode, new GUIContent("扫描模式"));
            EditorGUILayout.PropertyField(_requireAutoRegister, new GUIContent("只扫描 [AutoRegister]"));

            if (_scanMode.enumValueIndex == (int)ScanMode.Specified)
            {
                EditorGUILayout.PropertyField(_assemblyNames, new GUIContent("程序集名称"), true);
            }

            EditorGUILayout.PropertyField(_excludeAssemblyPrefixes, new GUIContent("排除程序集前缀"), true);

            EditorGUILayout.Space();

            // Runtime 配置
            // EditorGUILayout.LabelField("Runtime 配置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_enableProfiling, new GUIContent("启用性能统计"));
            EditorGUILayout.PropertyField(_symbols, new GUIContent("条件符号"), true);

            EditorGUILayout.Space();

            // 初始化配置
            // EditorGUILayout.LabelField("初始化配置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_autoInitialize, new GUIContent("自动初始化"));
            EditorGUILayout.PropertyField(_enableEditorSupport, new GUIContent("编辑器支持"));

            EditorGUILayout.Space();

            // 日志配置
            // EditorGUILayout.LabelField("日志配置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_systemInfoLevel, new GUIContent("系统信息级别"));
            EditorGUILayout.PropertyField(_debugEditorPipeline, new GUIContent("编辑器管线调试"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
