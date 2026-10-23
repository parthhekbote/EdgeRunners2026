using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.UI
{
    /// <summary>
    /// Guard and automatic repair against Unity uGUI's internal Selectable.s_SelectableCount corruption bug
    /// where s_SelectableCount drops to -1, causing IndexOutOfRangeException on any new Selectable.OnEnable().
    /// </summary>
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public static class UGUISelectableFix
    {
        private static FieldInfo _countField;
        private static FieldInfo _arrayField;
        private static FieldInfo _idxField;

        static UGUISelectableFix()
        {
            InitFields();
            Repair();
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

#if UNITY_EDITOR
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            Repair();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void OnBeforeSceneLoad()
        {
            Repair();
        }

        private static void InitFields()
        {
            if (_countField != null) return;
            _countField = typeof(Selectable).GetField("s_SelectableCount", BindingFlags.Static | BindingFlags.NonPublic);
            _arrayField = typeof(Selectable).GetField("s_Selectables", BindingFlags.Static | BindingFlags.NonPublic);
            _idxField = typeof(Selectable).GetField("m_CurrentIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public static void Repair()
        {
            InitFields();
            if (_countField == null || _arrayField == null) return;

            try
            {
                int count = (int)_countField.GetValue(null);
                var array = (Selectable[])_arrayField.GetValue(null);

                if (count < 0 || array == null || count > array.Length)
                {
                    var activeSelectables = Object.FindObjectsByType<Selectable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                    var newArray = new Selectable[Mathf.Max(activeSelectables.Length * 2, 128)];
                    for (int i = 0; i < activeSelectables.Length; i++)
                    {
                        newArray[i] = activeSelectables[i];
                        _idxField?.SetValue(activeSelectables[i], i);
                    }
                    _arrayField.SetValue(null, newArray);
                    _countField.SetValue(null, activeSelectables.Length);
                    Debug.Log($"<color=#38EFD7>[UGUIFix]</color> Repaired corrupt Selectable state: count was {count}, reset to {activeSelectables.Length}.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[UGUIFix] Exception during Selectable repair: " + ex.Message);
            }
        }
    }
}
