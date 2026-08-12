using System;

namespace MegaCrit.Sts2.Core.Modding;

/// <summary>
/// Compile-time stub matching STS2's mod entry attribute.
/// Rebuild against the real game sts2.dll before shipping a runtime DLL.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ModInitializerAttribute : Attribute
{
    public ModInitializerAttribute(string methodName)
    {
        MethodName = methodName;
    }

    public string MethodName { get; }
}
