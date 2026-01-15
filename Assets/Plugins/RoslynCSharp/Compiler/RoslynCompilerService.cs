namespace RoslynCSharp.Compiler
{
    /// <summary>
    /// Provides compiler services for runtime C# compilation.
    /// </summary>
    public class RoslynCompilerService
    {
        private AssemblyReferenceCollection _referenceAssemblies;

        public AssemblyReferenceCollection ReferenceAssemblies => _referenceAssemblies;

        public RoslynCompilerService()
        {
            _referenceAssemblies = new AssemblyReferenceCollection();
        }
    }
}
