namespace RoslynCSharp
{
    /// <summary>
    /// Specifies the security mode for script execution.
    /// </summary>
    public enum ScriptSecurityMode
    {
        /// <summary>
        /// Use the default security settings.
        /// </summary>
        UseSettings = 0,

        /// <summary>
        /// Allow all operations (no security restrictions).
        /// </summary>
        AllowAll = 1,

        /// <summary>
        /// Deny all potentially dangerous operations.
        /// </summary>
        DenyAll = 2
    }
}
