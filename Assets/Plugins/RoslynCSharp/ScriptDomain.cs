using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityEngine;

namespace RoslynCSharp
{
    /// <summary>
    /// Represents a script domain for compiling and loading C# code at runtime.
    /// Open source implementation compatible with RoslynCSharp API.
    /// </summary>
    public class ScriptDomain
    {
        private string _domainName;
        private RoslynCompilerService _compilerService;
        private CompilationResult _compileResult;

        public string Name => _domainName;
        public RoslynCompilerService RoslynCompilerService => _compilerService;
        public CompilationResult CompileResult => _compileResult;

        private ScriptDomain(string name)
        {
            _domainName = name;
            _compilerService = new RoslynCompilerService();
        }

        /// <summary>
        /// Create a new script domain with the specified name.
        /// </summary>
        public static ScriptDomain CreateDomain(string name)
        {
            return new ScriptDomain(name);
        }

        /// <summary>
        /// Compile and load C# source code.
        /// </summary>
        public ScriptAssembly CompileAndLoadSource(string code, ScriptSecurityMode securityMode = ScriptSecurityMode.UseSettings)
        {
            try
            {
                // Parse the code
                var syntaxTree = CSharpSyntaxTree.ParseText(code);

                // Build references
                var references = new List<MetadataReference>();
                foreach (var asmRef in _compilerService.ReferenceAssemblies)
                {
                    if (asmRef.Assembly != null && !asmRef.Assembly.IsDynamic)
                    {
                        try
                        {
                            var location = asmRef.Assembly.Location;
                            if (!string.IsNullOrEmpty(location) && File.Exists(location))
                            {
                                references.Add(MetadataReference.CreateFromFile(location));
                            }
                        }
                        catch (Exception)
                        {
                            // Skip assemblies that can't be referenced
                        }
                    }
                }

                // Create compilation
                var compilation = CSharpCompilation.Create(
                    assemblyName: $"{_domainName}_{Guid.NewGuid():N}",
                    syntaxTrees: new[] { syntaxTree },
                    references: references,
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                        .WithOptimizationLevel(OptimizationLevel.Debug)
                        .WithAllowUnsafe(true)
                );

                // Emit to memory
                using (var ms = new MemoryStream())
                {
                    var emitResult = compilation.Emit(ms);

                    // Store compilation result
                    _compileResult = new CompilationResult(emitResult);

                    if (!emitResult.Success)
                    {
                        return null;
                    }

                    // Load assembly
                    ms.Seek(0, SeekOrigin.Begin);
                    var assembly = Assembly.Load(ms.ToArray());

                    return new ScriptAssembly(assembly);
                }
            }
            catch (Exception ex)
            {
                _compileResult = new CompilationResult(ex);
                return null;
            }
        }
    }
}
