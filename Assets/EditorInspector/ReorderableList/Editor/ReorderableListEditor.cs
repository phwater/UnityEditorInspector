//~ Uncomment the line below to turn all arrays into reorderable lists
//#define TURN_ALL_ARRAYS_REORDERABLE

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditorInternal;
using UnityEngine;

namespace EditorInspector {

    [CustomEditor(typeof(UnityEngine.Object), true, isFallback = true)]
    [CanEditMultipleObjects]
    public class ReorderableListEditor : Editor {

        /// <summary>
        /// Internal class that manages ReorderableLists for each reorderable SerializedProperty in a SerializedObject's direct child
        /// </summary>
        protected class SortableListData {
            public string Parent { get; private set; }
            public Func<int, string> ElementHeaderCallback = null;

            private readonly Dictionary<string, ReorderableList> propIndex = new Dictionary<string, ReorderableList>();
            private readonly Dictionary<string, Action<SerializedProperty, UnityEngine.Object[]>> propDropHandlers = new Dictionary<string, Action<SerializedProperty, UnityEngine.Object[]>>();
            private readonly Dictionary<string, int> countIndex = new Dictionary<string, int>();

            public SortableListData(string parent) {
                Parent = parent;
            }

            public void AddProperty(SerializedProperty property) {
                // Check if this property actually belongs to the same direct child
                if (GetGrandParentPath(property).Equals(Parent) == false)
                    return;

                ReorderableList propList = new ReorderableList(property.serializedObject, property, draggable: true, displayHeader: false, displayAddButton: true, displayRemoveButton: true) {
                    headerHeight = 5
                };

                propList.drawElementCallback = (rect, index, active, focused) => {
                    SerializedProperty targetElement = property.GetArrayElementAtIndex(index);

                    bool isExpanded = targetElement.isExpanded;
                    rect.height = EditorGUI.GetPropertyHeight(targetElement, GUIContent.none, isExpanded);

                    if (targetElement.hasVisibleChildren)
                        rect.xMin += 10;

                    // Get Unity to handle drawing each element
                    GUIContent propHeader = new GUIContent(targetElement.displayName);
                    if (ElementHeaderCallback != null)
                        propHeader.text = ElementHeaderCallback(index);
                    EditorGUI.PropertyField(rect, targetElement, propHeader, isExpanded);

                    // If drawing the selected element, use it to set the element height
                    // Element height seems to control selected background
#if UNITY_5_1 || UNITY_5_2
                    if (index == propList.index) {
                        // Height might have changed when dealing with serialized class
                        // Call the select callback when height changes to reset the list elementHeight
                        float newHeight = EditorGUI.GetPropertyHeight(targetElement, GUIContent.none, targetElement.isExpanded);
                        if (rect.height != newHeight)
                            propList.elementHeight = Mathf.Max(propList.elementHeight, newHeight);
                    }
#endif
                };

                // Unity 5.3 onwards allows reorderable lists to have variable element heights
#if UNITY_5_3_OR_NEWER
                propList.elementHeightCallback = (index) => ElementHeightCallback(property, index);

                propList.drawElementBackgroundCallback = (rect, index, active, focused) => {
                    if (styleHighlight == null)
                        styleHighlight = GUI.skin.FindStyle("MeTransitionSelectHead");
                    if (focused == false)
                        return;
                    rect.height = ElementHeightCallback(property, index);
                    GUI.Box(rect, GUIContent.none, styleHighlight);
                };
#endif
                propIndex.Add(property.propertyPath, propList);
            }

            public bool DoLayoutProperty(SerializedProperty property) {
                if (propIndex.ContainsKey(property.propertyPath) == false)
                    return false;

                // Draw the header
                string headerText = string.Format("{0} [{1}]", property.displayName, property.arraySize);
                EditorGUILayout.PropertyField(property, new GUIContent(headerText), false);

                // Save header rect for handling drag and drop
                Rect dropRect = GUILayoutUtility.GetLastRect();

                // Draw the reorderable list for the property
                if (property.isExpanded) {
                    int newArraySize = EditorGUILayout.IntField("Size", property.arraySize);
                    if (newArraySize != property.arraySize)
                        property.arraySize = newArraySize >= 0 ? newArraySize : 0;
                    propIndex[property.propertyPath].DoLayoutList();
                }

                // Handle drag and drop into the header
                Event evt = Event.current;
                if (evt == null)
                    return true;

                if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) {
                    if (dropRect.Contains(evt.mousePosition) == false)
                        return true;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform) {
                        DragAndDrop.AcceptDrag();
                        Action<SerializedProperty, UnityEngine.Object[]> handler = null;
                        if (propDropHandlers.TryGetValue(property.propertyPath, out handler)) {
                            if (handler != null)
                                handler(property, DragAndDrop.objectReferences);
                        } else {
                            foreach (UnityEngine.Object dragged_object in DragAndDrop.objectReferences) {
                                if (dragged_object.GetType() != property.GetType())
                                    continue;

                                int newIndex = property.arraySize;
                                property.arraySize++;

                                SerializedProperty target = property.GetArrayElementAtIndex(newIndex);
                                target.objectReferenceInstanceIDValue = dragged_object.GetInstanceID();
                            }
                        }
                        evt.Use();
                    }
                }
                return true;
            }

            public int GetElementCount(SerializedProperty property) {
                if (property.arraySize <= 0)
                    return 0;

                int count;
                if (countIndex.TryGetValue(property.propertyPath, out count)) {
                    return count;
                }

                SerializedProperty element = property.GetArrayElementAtIndex(0);
                SerializedProperty countElement = element.Copy();
                int childCount = 0;
                if (countElement.NextVisible(true)) {
                    int depth = countElement.Copy().depth;
                    do {
                        if (countElement.depth != depth)
                            break;
                        childCount++;
                    } while (countElement.NextVisible(false));
                }

                countIndex.Add(property.propertyPath, childCount);
                return childCount;
            }

            public ReorderableList GetPropertyList(SerializedProperty property) {
                if (propIndex.ContainsKey(property.propertyPath))
                    return propIndex[property.propertyPath];
                return null;
            }

            public void SetDropHandler(SerializedProperty property, Action<SerializedProperty, UnityEngine.Object[]> handler) {
                string path = property.propertyPath;
                if (propDropHandlers.ContainsKey(path))
                    propDropHandlers[path] = handler;
                else
                    propDropHandlers.Add(path, handler);
            }

            private float ElementHeightCallback(SerializedProperty property, int index) {
                SerializedProperty arrayElement = property.GetArrayElementAtIndex(index);
                float calculatedHeight = EditorGUI.GetPropertyHeight(arrayElement, GUIContent.none, arrayElement.isExpanded);
                calculatedHeight += 3;
                return calculatedHeight;
            }

        } // End SortableListData

        #region STATIC
        protected static bool FORCE_INIT = false;

        private static GUIStyle styleHighlight;

        protected static string GetGrandParentPath(SerializedProperty property) {
            string parent = property.propertyPath;
            int firstDot = property.propertyPath.IndexOf('.');
            if (firstDot > 0) {
                parent = property.propertyPath.Substring(0, firstDot);
            }
            return parent;
        }
        #endregion

        public bool isSubEditor;

        private readonly List<SortableListData> listIndex = new List<SortableListData>();
        private readonly Dictionary<string, Editor> editableIndex = new Dictionary<string, Editor>();

        protected bool alwaysDrawInspector = false;
        protected bool isInitialized = false;
        protected bool hasSortableArrays = false;
        protected bool hasEditable = false;

        ~ReorderableListEditor() {
            listIndex.Clear();
            editableIndex.Clear();
            isInitialized = false;
        }

        [DidReloadScripts]
        private static void HandleScriptReload() {
            FORCE_INIT = true;
            EditorApplication.delayCall = () => {
                EditorApplication.delayCall = () => { FORCE_INIT = false; };
            };
        }

        #region Init Inspector
        private void OnEnable() {
            InitInspector();
        }

        protected virtual void InitInspector(bool force = false) {
            if (force) {
                isInitialized = false;
            }

            if (isInitialized && FORCE_INIT == false) {
                return;
            }

            FindTargetProperties();
        }

        protected void FindTargetProperties() {
            listIndex.Clear();
            editableIndex.Clear();

            SerializedProperty iterProp = serializedObject.GetIterator();
            // This iterator goes through all the child serialized properties, looking
            // for properties that have the SortableArray attribute
            while (iterProp.NextVisible(true)) {
                if (iterProp.isArray && iterProp.propertyType != SerializedPropertyType.String) {
#if TURN_ALL_ARRAYS_REORDERABLE
                    bool bTurnToList = true;
#else
                    bool bTurnToList = iterProp.HasAttribute<ReorderableListAttribute>();
#endif
                    if (bTurnToList) {
                        hasSortableArrays = true;
                        CreateListData(serializedObject.FindProperty(iterProp.propertyPath));
                    }
                }
            }

            isInitialized = true;
            if (hasSortableArrays == false) {
                listIndex.Clear();
            }
        }

        private void CreateListData(SerializedProperty property) {
            string parent = GetGrandParentPath(property);

            // Try to find the grand parent in SortableListData
            SortableListData data = listIndex.Find(listData => listData.Parent.Equals(parent));
            if (data == null) {
                data = new SortableListData(parent);
                listIndex.Add(data);
            }

            data.AddProperty(property);
            object[] attr = property.GetAttributes<ReorderableListAttribute>();
            if (attr != null && attr.Length == 1) {
                ReorderableListAttribute arrayAttr = (ReorderableListAttribute) attr[0];
                if (arrayAttr != null) {
                    HandleReorderableOptions(arrayAttr, property, data);
                }
            }
        }

        private void HandleReorderableOptions(ReorderableListAttribute arrayAttr, SerializedProperty property, SortableListData data) {
            // Custom element header
            if (string.IsNullOrEmpty(arrayAttr.ElementHeader) == false) {
                data.ElementHeaderCallback = i => string.Format("{0} {1}", arrayAttr.ElementHeader, (arrayAttr.HeaderZeroIndex ? i : i + 1));
            }

            // Draw property as single line
            if (arrayAttr.ElementSingleLine) {
                ReorderableList list = data.GetPropertyList(property);

#if UNITY_5_3_OR_NEWER
                list.elementHeightCallback = index => EditorGUIUtility.singleLineHeight + 6;
                list.drawElementBackgroundCallback = (rect, index, active, focused) => {
                    if (focused == false)
                        return;
                    if (styleHighlight == null)
                        styleHighlight = GUI.skin.FindStyle("MeTransitionSelectHead");
                    GUI.Box(rect, GUIContent.none, styleHighlight);
                };
#endif

                list.drawElementCallback = (rect, index, active, focused) => {
                    SerializedProperty element = property.GetArrayElementAtIndex(index);
                    element.isExpanded = false;

                    int childCount = data.GetElementCount(property);
                    if (childCount < 1)
                        return;

                    rect.y += 3;
                    rect.height -= 6;

                    if (element.NextVisible(true)) {
                        float restoreWidth = EditorGUIUtility.labelWidth;
                        EditorGUIUtility.labelWidth /= childCount;

                        float padding = 5f;
                        float width = rect.width - padding * (childCount - 1);
                        width /= childCount;

                        Rect childRect = new Rect(rect) { width = width };
                        int depth = element.Copy().depth;
                        do {
                            if (element.depth != depth)
                                break;

                            if (childCount <= 2)
                                EditorGUI.PropertyField(childRect, element, false);
                            else
                                EditorGUI.PropertyField(childRect, element, GUIContent.none, false);
                            childRect.x += width + padding;
                        } while (element.NextVisible(false));

                        EditorGUIUtility.labelWidth = restoreWidth;
                    }
                };

            }
        }

        #endregion

        #region Draw Inspector
        public override void OnInspectorGUI() {
            if (InspectorGUIStart(alwaysDrawInspector) == false) {
                return;
            }

            EditorGUI.BeginChangeCheck();

            DrawPropertiesAll();

            if (EditorGUI.EndChangeCheck()) {
                serializedObject.ApplyModifiedProperties();
                InitInspector(true);
            }
        }

        protected bool InspectorGUIStart(bool force = false) {
            // Not initialized, try initializing
            if (hasSortableArrays && listIndex.Count == 0)
                InitInspector();
            if (hasEditable && editableIndex.Count == 0)
                InitInspector();

            // No sortable arrays or list index unintialized
            bool cannotDrawOrderable = (hasSortableArrays == false || listIndex.Count == 0);
            bool cannotDrawEditable = (hasEditable == false || editableIndex.Count == 0);
            if (cannotDrawOrderable && cannotDrawEditable && force == false) {
                if (isSubEditor) {
                    DrawPropertiesExcluding(serializedObject, "m_Script");
                } else {
                    base.OnInspectorGUI();
                }
                return false;
            }

            serializedObject.Update();
            return true;
        }

        protected enum IterControl {
            Draw,
            Continue,
            Break
        }

        protected void IterateDrawProperty(SerializedProperty property, Func<IterControl> filter = null) {
            if (property.NextVisible(true)) {
                // Remember depth iteration started from
                int depth = property.Copy().depth;
                do {
                    // If goes deeper than the iteration depth, get out
                    if (property.depth != depth)
                        break;
                    if (isSubEditor && property.name.Equals("m_Script"))
                        continue;

                    if (filter != null) {
                        var filterResult = filter();
                        if (filterResult == IterControl.Break)
                            break;
                        if (filterResult == IterControl.Continue)
                            continue;
                    }

                    DrawPropertySortableArray(property);
                } while (property.NextVisible(false));
            }
        }

        /// <summary>
        /// Draw a SerializedProperty as a ReorderableList if it was found during
        /// initialization, otherwise use EditorGUILayout.PropertyField
        /// </summary>
        /// <param name="property"></param>
        protected void DrawPropertySortableArray(SerializedProperty property) {
            // Try to get the sortable list this property belongs to
            SortableListData listData = null;
            if (listIndex.Count > 0) {
                listData = listIndex.Find(data => property.propertyPath.StartsWith(data.Parent));
            }

            // Has ReorderableList
            if (listData != null) {
                // Try to show the list
                if (listData.DoLayoutProperty(property) == false) {
                    EditorGUILayout.PropertyField(property, false);
                    if (property.isExpanded) {
                        EditorGUI.indentLevel++;
                        SerializedProperty targetProp = serializedObject.FindProperty(property.propertyPath);
                        IterateDrawProperty(targetProp);
                        EditorGUI.indentLevel--;
                    }
                }
            } else {
                SerializedProperty targetProp = serializedObject.FindProperty(property.propertyPath);

                bool isStartProp = targetProp.propertyPath.StartsWith("m_");
                using (new EditorGUI.DisabledScope(isStartProp)) {
                    EditorGUILayout.PropertyField(targetProp, targetProp.isExpanded);
                }
            }
        }
        #endregion

        #region Helper functions
        /// <summary>
        /// Draw the default inspector, with the sortable arrays
        /// </summary>
        public void DrawPropertiesAll() {
            SerializedProperty iterProp = serializedObject.GetIterator();
            IterateDrawProperty(iterProp);
        }

        /// <summary>
        /// Draw the default inspector, except for the given property names
        /// </summary>
        /// <param name="propertyNames"></param>
        public void DrawPropertiesExcept(params string[] propertyNames) {
            SerializedProperty iterProp = serializedObject.GetIterator();
            IterateDrawProperty(iterProp, filter: () => {
                if (propertyNames.Contains(iterProp.name))
                    return IterControl.Continue;
                return IterControl.Draw;
            });
        }

        /// <summary>
        /// Draw the default inspector, starting from a given property
        /// </summary>
        /// <param name="propertyStart">Property name to start from</param>
        public void DrawPropertiesFrom(string propertyStart) {
            SerializedProperty iterProp = serializedObject.GetIterator();
            IterateDrawProperty(iterProp, filter: () => {
                if (iterProp.name.Equals(propertyStart))
                    return IterControl.Draw;
                return IterControl.Continue;
            });
        }

        /// <summary>
        /// Draw the default inspector, up to a given property
        /// </summary>
        /// <param name="propertyStop">Property name to stop at</param>
        public void DrawPropertiesUpTo(string propertyStop) {
            SerializedProperty iterProp = serializedObject.GetIterator();
            IterateDrawProperty(iterProp, filter: () => {
                if (iterProp.name.Equals(propertyStop))
                    return IterControl.Break;
                return IterControl.Draw;
            });
        }

        /// <summary>
        /// Draw the default inspector, starting from a given property to a stopping property
        /// </summary>
        /// <param name="propertyStart">Property name to start from</param>
        /// <param name="propertyStop">Property name to stop at</param>
        public void DrawPropertiesFromUpTo(string propertyStart, string propertyStop) {
            bool canDraw = false;
            SerializedProperty iterProp = serializedObject.GetIterator();
            IterateDrawProperty(iterProp,
                filter: () => {
                    if (iterProp.name.Equals(propertyStop))
                        return IterControl.Break;

                    if (iterProp.name.Equals(propertyStart))
                        canDraw = true;

                    if (canDraw == false)
                        return IterControl.Continue;

                    return IterControl.Draw;
                });
        }

        #endregion

        #region For subclass
        /// <summary>
        /// Given a SerializedProperty, return the automatic ReorderableList assigned to it if any
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        protected ReorderableList GetSortableList(SerializedProperty property) {
            if (listIndex.Count == 0)
                return null;

            string parent = GetGrandParentPath(property);

            SortableListData data = listIndex.Find(listData => listData.Parent.Equals(parent));
            if (data == null)
                return null;

            return data.GetPropertyList(property);
        }

        /// <summary>
        /// Set a drag and drop handler function on a SerializedObject's ReorderableList, if any
        /// </summary>
        /// <param name="property"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        protected bool SetDragDropHandler(SerializedProperty property, Action<SerializedProperty, UnityEngine.Object[]> handler) {
            if (listIndex.Count == 0)
                return false;

            string parent = GetGrandParentPath(property);

            SortableListData data = listIndex.Find(listData => listData.Parent.Equals(parent));
            if (data == null)
                return false;

            data.SetDropHandler(property, handler);
            return true;
        }
        #endregion

    }
}
