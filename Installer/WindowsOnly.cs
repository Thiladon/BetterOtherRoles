// The installer runs on Windows only (Windows Forms, the registry for Steam): the platform analyzer (CA1416) is told so
// for the whole program.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

namespace System.Runtime.Versioning
{
    /// <summary>
    ///     Platform the code runs on, read by the platform analyzer by its name. Part of .NET 5 and later, not of .NET
    ///     Framework 4.8: declared here, without any effect at run time.
    /// </summary>
    /// <param name="platformName">The platform ("windows").</param>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    internal sealed class SupportedOSPlatformAttribute(string platformName) : Attribute
    {
        /// <summary>
        ///     Gets the platform.
        /// </summary>
        public string PlatformName { get; } = platformName;
    }
}
