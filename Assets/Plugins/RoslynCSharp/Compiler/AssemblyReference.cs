using System;
using System.Collections.Generic;
using System.Reflection;

namespace RoslynCSharp.Compiler
{
    /// <summary>
    /// Represents a reference to an assembly for compilation.
    /// </summary>
    public class AssemblyReference
    {
        public Assembly Assembly { get; private set; }
        public string Location { get; private set; }

        private AssemblyReference(Assembly assembly)
        {
            Assembly = assembly;
            try
            {
                Location = assembly?.Location;
            }
            catch
            {
                Location = null;
            }
        }

        private AssemblyReference(string location)
        {
            Location = location;
            Assembly = null;
        }

        /// <summary>
        /// Create an assembly reference from an Assembly object.
        /// </summary>
        public static AssemblyReference FromAssembly(Assembly assembly)
        {
            return new AssemblyReference(assembly);
        }

        /// <summary>
        /// Create an assembly reference from a file path.
        /// </summary>
        public static AssemblyReference FromFile(string path)
        {
            return new AssemblyReference(path);
        }
    }

    /// <summary>
    /// Collection of assembly references for the compiler service.
    /// </summary>
    public class AssemblyReferenceCollection : List<AssemblyReference>
    {
    }
}
