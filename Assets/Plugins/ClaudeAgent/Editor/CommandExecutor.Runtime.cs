using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.Linq;
using System.Reflection;
using RoslynCSharp;
using RoslynCSharp.Compiler;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        // Persistent ScriptDomain for runtime code execution
        private ScriptDomain _runtimeDomain;

        /// <summary>
        /// Registers Runtime commands
        /// </summary>
        private void RegisterRuntimeCommands()
        {
            RegisterCommand("execute_code", ExecuteCode);
            RegisterCommand("attach_script", AttachScript);
            RegisterCommand("execute_on_object", ExecuteOnObject);
            RegisterCommand("play_animation", PlayAnimation);
        }

        /// <summary>
        /// Get or create the runtime script domain
        /// </summary>
        private ScriptDomain GetOrCreateRuntimeDomain()
        {
            if (_runtimeDomain == null)
            {
                _runtimeDomain = ScriptDomain.CreateDomain("ClaudeRuntimeDomain");

                // Add essential assembly references
                AddDefaultAssemblyReferences(_runtimeDomain);

                Debug.Log("[CommandExecutor] Created runtime script domain: ClaudeRuntimeDomain");
            }
            return _runtimeDomain;
        }

        /// <summary>
        /// Add default assembly references for runtime compilation
        /// </summary>
        private void AddDefaultAssemblyReferences(ScriptDomain domain)
        {
            var compilerService = domain.RoslynCompilerService;
            var addedAssemblies = new System.Collections.Generic.HashSet<string>();

            // Add all currently loaded assemblies that are commonly needed
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    string name = asm.GetName().Name;

                    // Skip dynamic assemblies
                    if (asm.IsDynamic) continue;

                    // Skip if already added
                    if (addedAssemblies.Contains(name)) continue;

                    // Add key assemblies
                    if (name == "netstandard" ||
                        name == "mscorlib" ||
                        name == "System.Runtime" ||
                        name == "System.Core" ||
                        name.StartsWith("UnityEngine"))
                    {
                        compilerService.ReferenceAssemblies.Add(AssemblyReference.FromAssembly(asm));
                        addedAssemblies.Add(name);
                        ConsoleLog($"[CommandExecutor] Added assembly reference: {name}");
                    }
                }
                catch (Exception e)
                {
                    ConsoleLogWarning($"[CommandExecutor] Failed to add assembly reference: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Format compilation errors for display
        /// </summary>
        private string FormatCompilationErrors(CompilationResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Compilation failed:");
            foreach (var error in result.Errors)
            {
                if (error.IsError)
                {
                    sb.AppendLine($"  ERROR: {error}");
                }
                else if (error.IsWarning)
                {
                    sb.AppendLine($"  WARNING: {error}");
                }
            }
            return sb.ToString();
        }

        // ====== Runtime Operations ======

        /// <summary>
        /// Execute arbitrary C# code at runtime
        /// </summary>
        private (bool, string) ExecuteCode(CommandParams p)
        {
            try
            {
                // Check play mode
                if (!EditorApplication.isPlaying)
                {
                    return Error("execute_code requires Play mode. Enter Play mode first.");
                }

                // Validate params
                if (p == null || string.IsNullOrEmpty(p.code))
                {
                    return Error("Missing required parameter: code");
                }

                // Get or create domain
                var domain = GetOrCreateRuntimeDomain();

                // Compile the code
                ScriptAssembly assembly = domain.CompileAndLoadSource(p.code, ScriptSecurityMode.UseSettings);

                // Check for compilation errors
                if (!domain.CompileResult.Success)
                {
                    return Error(FormatCompilationErrors(domain.CompileResult));
                }

                if (assembly == null)
                {
                    return Error("Compilation succeeded but no assembly was created");
                }

                // Find user-defined type (skip compiler-generated types like EmbeddedAttribute)
                ScriptType type = FindUserDefinedType(assembly, p.class_name);
                if (type == null)
                {
                    return Error("No user-defined class found in the code. Define a class with a Run() method.");
                }

                // Create instance
                ScriptProxy proxy = type.CreateInstance();
                if (proxy == null)
                {
                    return Error($"Failed to create instance of type: {type.Name}");
                }

                // Determine method name
                string methodName = string.IsNullOrEmpty(p.method) ? "Run" : p.method;

                // Call the method
                object result = proxy.SafeCall(methodName);

                var sb = new StringBuilder();
                sb.AppendLine($"Executed {type.Name}.{methodName}()");
                if (result != null)
                {
                    sb.AppendLine($"Return value: {result}");
                }

                ConsoleLog($"[CommandExecutor] {sb}");
                return Success(sb.ToString().TrimEnd());
            }
            catch (Exception e)
            {
                return Error($"Error executing code: {e.Message}");
            }
        }

        /// <summary>
        /// Find a user-defined type in the assembly (skip compiler-generated types)
        /// </summary>
        private ScriptType FindUserDefinedType(ScriptAssembly assembly, string className = null)
        {
            // If class name is specified, find it directly
            if (!string.IsNullOrEmpty(className))
            {
                return assembly.FindType(className);
            }

            // Find first user-defined class (not compiler-generated)
            var allTypes = assembly.FindAllTypes();
            foreach (var type in allTypes)
            {
                string name = type.Name;
                // Skip compiler-generated types
                if (name.Contains("<") || name.Contains(">") ||
                    name.EndsWith("Attribute") ||
                    name.StartsWith("__") ||
                    name == "EmbeddedAttribute" ||
                    name == "IsReadOnlyAttribute" ||
                    name == "NullableAttribute" ||
                    name == "NullableContextAttribute")
                {
                    continue;
                }
                return type;
            }
            return null;
        }

        /// <summary>
        /// Attach a compiled MonoBehaviour to a GameObject
        /// </summary>
        private (bool, string) AttachScript(CommandParams p)
        {
            try
            {
                // Check play mode
                if (!EditorApplication.isPlaying)
                {
                    return Error("attach_script requires Play mode. Enter Play mode first.");
                }

                // Validate params
                if (p == null || string.IsNullOrEmpty(p.code))
                {
                    return Error("Missing required parameter: code");
                }

                if (string.IsNullOrEmpty(p.target))
                {
                    return Error("Missing required parameter: target");
                }

                // Find target GameObject
                var (targetObj, findError) = FindGameObjectByPath(p.target);
                if (targetObj == null)
                {
                    return Error(findError ?? $"GameObject not found: {p.target}");
                }

                // Get or create domain
                var domain = GetOrCreateRuntimeDomain();

                // Compile the code
                ScriptAssembly assembly = domain.CompileAndLoadSource(p.code, ScriptSecurityMode.UseSettings);

                // Check for compilation errors
                if (!domain.CompileResult.Success)
                {
                    return Error(FormatCompilationErrors(domain.CompileResult));
                }

                if (assembly == null)
                {
                    return Error("Compilation succeeded but no assembly was created");
                }

                // Find MonoBehaviour type
                ScriptType behaviourType = null;
                if (!string.IsNullOrEmpty(p.class_name))
                {
                    behaviourType = assembly.FindSubTypeOf<MonoBehaviour>(p.class_name);
                    if (behaviourType == null)
                    {
                        return Error($"MonoBehaviour class not found: {p.class_name}");
                    }
                }
                else
                {
                    // Find first MonoBehaviour in the assembly
                    var behaviourTypes = assembly.FindAllMonoBehaviourTypes();
                    if (behaviourTypes == null || behaviourTypes.Length == 0)
                    {
                        return Error("No MonoBehaviour class found in the code. Ensure your class inherits from MonoBehaviour.");
                    }
                    behaviourType = behaviourTypes[0];
                }

                // Create and attach the behaviour
                ScriptProxy proxy = behaviourType.CreateInstance(targetObj);
                if (proxy == null)
                {
                    return Error($"Failed to attach {behaviourType.Name} to {targetObj.name}");
                }

                string result = $"Attached {behaviourType.Name} to {targetObj.name}";
                ConsoleLog($"[CommandExecutor] {result}");
                return Success(result);
            }
            catch (Exception e)
            {
                return Error($"Error attaching script: {e.Message}");
            }
        }

        /// <summary>
        /// Execute code targeting a specific GameObject (passed as parameter)
        /// </summary>
        private (bool, string) ExecuteOnObject(CommandParams p)
        {
            try
            {
                // Check play mode
                if (!EditorApplication.isPlaying)
                {
                    return Error("execute_on_object requires Play mode. Enter Play mode first.");
                }

                // Validate params
                if (p == null || string.IsNullOrEmpty(p.code))
                {
                    return Error("Missing required parameter: code");
                }

                if (string.IsNullOrEmpty(p.target))
                {
                    return Error("Missing required parameter: target");
                }

                // Find target GameObject
                var (targetObj, findError) = FindGameObjectByPath(p.target);
                if (targetObj == null)
                {
                    return Error(findError ?? $"GameObject not found: {p.target}");
                }

                // Get or create domain
                var domain = GetOrCreateRuntimeDomain();

                // Compile the code
                ScriptAssembly assembly = domain.CompileAndLoadSource(p.code, ScriptSecurityMode.UseSettings);

                // Check for compilation errors
                if (!domain.CompileResult.Success)
                {
                    return Error(FormatCompilationErrors(domain.CompileResult));
                }

                if (assembly == null)
                {
                    return Error("Compilation succeeded but no assembly was created");
                }

                // Find user-defined type
                ScriptType type = FindUserDefinedType(assembly, p.class_name);
                if (type == null)
                {
                    return Error("No user-defined class found in the code. Define a class with a Run(GameObject) method.");
                }

                // Create instance
                ScriptProxy proxy = type.CreateInstance();
                if (proxy == null)
                {
                    return Error($"Failed to create instance of type: {type.Name}");
                }

                // Determine method name
                string methodName = string.IsNullOrEmpty(p.method) ? "Run" : p.method;

                // Call the method with GameObject parameter
                object result = proxy.SafeCall(methodName, targetObj);

                var sb = new StringBuilder();
                sb.AppendLine($"Executed {type.Name}.{methodName}({targetObj.name})");
                if (result != null)
                {
                    sb.AppendLine($"Return value: {result}");
                }

                ConsoleLog($"[CommandExecutor] {sb}");
                return Success(sb.ToString().TrimEnd());
            }
            catch (Exception e)
            {
                return Error($"Error executing code on object: {e.Message}");
            }
        }

        /// <summary>
        /// Play an animation state on an Animator (simpler than execute_on_object)
        /// </summary>
        private (bool, string) PlayAnimation(CommandParams p)
        {
            try
            {
                // Check play mode
                if (!EditorApplication.isPlaying)
                {
                    return Error("play_animation requires Play mode. Enter Play mode first.");
                }

                // Validate params
                if (p == null || string.IsNullOrEmpty(p.target))
                {
                    return Error("Missing required parameter: target");
                }

                if (string.IsNullOrEmpty(p.state))
                {
                    return Error("Missing required parameter: state");
                }

                // Find target GameObject
                var (targetObj, findError) = FindGameObjectByPath(p.target);
                if (targetObj == null)
                {
                    return Error(findError ?? $"GameObject not found: {p.target}");
                }

                // Get Animator component
                var animator = targetObj.GetComponent<Animator>();
                if (animator == null)
                {
                    return Error($"No Animator component on {p.target}");
                }

                // Determine layer and normalized time (use defaults if not specified)
                int layer = p.layer >= 0 ? p.layer : 0;
                float normalizedTime = p.normalized_time >= 0 ? p.normalized_time : 0f;

                // Play the animation
                animator.Play(p.state, layer, normalizedTime);

                string result = $"Playing '{p.state}' on {p.target} (layer {layer})";
                ConsoleLog($"[CommandExecutor] {result}");
                return Success(result);
            }
            catch (Exception e)
            {
                return Error($"Error playing animation: {e.Message}");
            }
        }
    }
}
