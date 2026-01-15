using System;
using System.Reflection;
using UnityEngine;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a type within a compiled script assembly.
    /// </summary>
    public class ScriptType
    {
        private Type _type;

        public Type SystemType => _type;
        public string Name => _type.Name;
        public string FullName => _type.FullName;

        internal ScriptType(Type type)
        {
            _type = type;
        }

        /// <summary>
        /// Create an instance of this type.
        /// </summary>
        public ScriptProxy CreateInstance()
        {
            try
            {
                var instance = Activator.CreateInstance(_type);
                return new ScriptProxy(instance, _type);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoslynCSharp] Failed to create instance of {_type.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create an instance of this MonoBehaviour type and attach it to a GameObject.
        /// </summary>
        public ScriptProxy CreateInstance(GameObject target)
        {
            if (target == null)
            {
                Debug.LogError("[RoslynCSharp] Cannot attach script to null GameObject");
                return null;
            }

            if (!typeof(MonoBehaviour).IsAssignableFrom(_type))
            {
                Debug.LogError($"[RoslynCSharp] Type {_type.Name} is not a MonoBehaviour");
                return null;
            }

            try
            {
                var component = target.AddComponent(_type);
                return new ScriptProxy(component, _type);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoslynCSharp] Failed to attach {_type.Name} to {target.name}: {ex.Message}");
                return null;
            }
        }
    }
}
