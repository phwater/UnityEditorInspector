using System.Collections.Generic;
using UnityEngine;

[System.Flags]
public enum Stuff {
    Coke = 1 << 0,
    Hamburguer = 1 << 1,
    Pizza = 1 << 2,
    Hotdog = 1 << 3,
    Pepsi = 1 << 4,
    Beer = 1 << 5,
    BuffaloWings = 1 << 6,
    IceCream = 1 << 7,
}

[System.Serializable]
public struct SomeOtherData {
    public int aNumber;

    [Range(0f, 1f)]
    public float anotherNumber;

    public Vector3 position;
}

public class ReordableListTest : MonoBehaviour {
    [EditorInspector.ReorderableList]
    public int[] aIntArray;

    [EditorInspector.ReorderableList]
    public float[] aFloatArray;

    [Range(0.0f, 100.0f), EditorInspector.ReorderableList]
    public float[] anotherFloatArray;

    [SerializeField, EditorInspector.ReorderableList]
    private List<Stuff> privateListOfStuff;

    [SerializeField, EditorInspector.ReorderableList]
    private SomeOtherData[] NestedData1;

}
