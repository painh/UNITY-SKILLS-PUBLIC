using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a compiled script assembly.
    /// </summary>
    public class ScriptAssembly
    {
        private Assembly _assembly;

        public Assembly Assembly => _assembly;

        internal ScriptAssembly(Assembly assembly)
        {
            _assembly = assembly;
        }

        /// <summary>
        /// Find a type by name in this assembly.
        /// </summary>
        public ScriptType FindType(string typeName)
        {
            var type = _assembly.GetTypes().FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
            return type != null ? new ScriptType(type) : null;
        }

        /// <summary>
        /// Find all types in this assembly.
        /// </summary>
        public ScriptType[] FindAllTypes()
        {
            return _assembly.GetTypes()
                .Select(t => new ScriptType(t))
                .ToArray();
        }

        /// <summary>
        /// Find a type that is a subtype of T.
        /// </summary>
        public ScriptType FindSubTypeOf<T>(string typeName) where T : class
        {
            var baseType = typeof(T);
            var type = _assembly.GetTypes()
                .FirstOrDefault(t => (t.Name == typeName || t.FullName == typeName) && baseType.IsAssignableFrom(t));
            return type != null ? new ScriptType(type) : null;
        }

        /// <summary>
        /// Find all MonoBehaviour types in this assembly.
        /// </summary>
        public ScriptType[] FindAllMonoBehaviourTypes()
        {
            return _assembly.GetTypes()
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => new ScriptType(t))
                .ToArray();
        }
    }
}
