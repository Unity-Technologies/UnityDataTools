using System;
using UnityEngine;

public class SerializeReferencePolymorphismExample : MonoBehaviour
{
    [Serializable]
    public class Base
    {
        public int m_Data = 1;
    }

    [Serializable]
    public class Apple : Base
    {
        public string m_Description = "Ripe";
    }

    [Serializable]
    public class Orange : Base
    {
        public bool m_IsRound = true;
    }

    // Use SerializeReference if this field needs to hold both
    // Apples and Oranges.  Otherwise only m_Data from Base object would be serialized
    [SerializeReference]
    public Base m_Item = new Apple();

    [SerializeReference]
    public Base m_Item2 = new Orange();

    // Use by-value instead of SerializeReference, because
    // no polymorphism and no other field needs to share this object
    public Apple m_MyApple = new Apple();
}
