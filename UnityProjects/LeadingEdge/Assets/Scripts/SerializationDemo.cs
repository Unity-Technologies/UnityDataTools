using System;
using UnityEngine;

// Reference asset exercising Unity serialization of a range of primitive types, an array and a string.
// Useful for systematically testing the UnityDataTool `dump` command against known field layout and values.
public class SerializationDemo : ScriptableObject
{
    // Held through a managed reference so the data is serialized as a referenced object rather than inline.
    [SerializeReference]
    public SerializedData data;

    [Serializable]
    public class SerializedData
    {
        public long longValue = -1234567890123456789L;
        public ulong ulongValue = 12345678901234567890UL;
        public int intValue = -2000000000;
        public uint uintValue = 4000000000U;
        public short shortValue = -12345;
        public ushort ushortValue = 54321;
        public sbyte signedCharValue = -123;   // C++ "signed char"
        public byte unsignedCharValue = 234;    // C++ "unsigned char"
        public bool boolValue = true;
        public float floatValue = 3.1415927f;
        public double doubleValue = 2.718281828459045;
        public char charValue = 'Z';
        public string stringValue = "SerializationDemo string value";
        public int[] intArray;

        public SerializedData()
        {
            intArray = new int[512];
            for (int i = 0; i < intArray.Length; i++)
                intArray[i] = i;
        }
    }
}
