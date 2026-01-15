using System;
using System.Reflection;
using UnityEngine;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a proxy to an instance of a dynamically compiled type.
    /// </summary>
    public class ScriptProxy
    {
        private object _instance;
        private Type _type;

        public object Instance => _instance;
        public Type ScriptType => _type;

        internal ScriptProxy(object instance, Type type)
        {
            _instance = instance;
            _type = type;
        }

        /// <summary>
        /// Safely call a method on the instance.
        /// </summary>
        public object SafeCall(string methodName, params object[] args)
        {
            try
            {
                var method = _type.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (method == null)
                {
                    Debug.LogWarning($"[RoslynCSharp] Method '{methodName}' not found on type '{_type.Name}'");
                    return null;
                }

                return method.Invoke(_instance, args);
            }
            catch (TargetInvocationException ex)
            {
                Debug.LogError($"[RoslynCSharp] Error invoking {methodName}: {ex.InnerException?.Message ?? ex.Message}");
                throw ex.InnerException ?? ex;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoslynCSharp] Error calling {methodName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get a field value from the instance.
        /// </summary>
        public T GetFieldValue<T>(string fieldName)
        {
            var field = _type.GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                Debug.LogWarning($"[RoslynCSharp] Field '{fieldName}' not found on type '{_type.Name}'");
                return default;
            }

            return (T)field.GetValue(_instance);
        }

        /// <summary>
        /// Set a field value on the instance.
        /// </summary>
        public void SetFieldValue(string fieldName, object value)
        {
            var field = _type.GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                Debug.LogWarning($"[RoslynCSharp] Field '{fieldName}' not found on type '{_type.Name}'");
                return;
            }

            field.SetValue(_instance, value);
        }
    }
}
