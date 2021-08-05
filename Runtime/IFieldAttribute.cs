using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Recstazy.SerializedInterface
{
    public class IFieldAttribute : PropertyAttribute 
    {
        public Type InterfaceType { get; private set; }

        public IFieldAttribute(Type type)
        {
            InterfaceType = type.IsInterface ? type : null;
        }
    }
}