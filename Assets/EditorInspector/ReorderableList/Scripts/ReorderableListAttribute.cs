using UnityEngine;

namespace EditorInspector {
    /// <summary>
    /// Display a List/Array as a sortable list in the inspector
    /// </summary>
    public class ReorderableListAttribute : PropertyAttribute {
        public string ElementHeader { get; protected set; }
        public bool HeaderZeroIndex { get; protected set; }
        public bool ElementSingleLine { get; protected set; }

        /// <summary>
        /// Display a List/Array as a sortable list in the inspector
        /// </summary>
        public ReorderableListAttribute() {
            ElementHeader = string.Empty;
            HeaderZeroIndex = false;
            ElementSingleLine = false;
        }

        /// <summary>
        /// Display a List/Array as a sortable list in the inspector
        /// </summary>
        /// <param name="headerString">Customize the element name in the inspector</param>
        /// <param name="isZeroIndex">If false, start element list count from 1</param>
        /// <param name="isSingleLine">Try to fit the array elements in a single line</param>
        public ReorderableListAttribute(string headerString = "", bool isZeroIndex = true, bool isSingleLine = false) {
            ElementHeader = headerString;
            HeaderZeroIndex = isZeroIndex;
            ElementSingleLine = isSingleLine;
        }
    }
}
