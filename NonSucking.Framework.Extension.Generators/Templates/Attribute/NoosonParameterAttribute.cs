﻿using System;

namespace NonSucking.Framework.Extension.Serialization
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
    public class NoosonParameterAttribute : Attribute
    {
        public string PropertyName { get;  }

        public NoosonParameterAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }
}