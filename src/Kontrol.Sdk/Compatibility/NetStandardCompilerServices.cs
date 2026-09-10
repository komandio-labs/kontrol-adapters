#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{

internal sealed class IsExternalInit;

[AttributeUsage(AttributeTargets.All, Inherited = false)]
internal sealed class RequiredMemberAttribute : Attribute;

[AttributeUsage(AttributeTargets.All, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
    {
        public string FeatureName { get; } = featureName;
        public bool IsOptional { get; set; }
    }
}

namespace System.Diagnostics.CodeAnalysis
{

[AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
internal sealed class SetsRequiredMembersAttribute : Attribute;
}
#endif
