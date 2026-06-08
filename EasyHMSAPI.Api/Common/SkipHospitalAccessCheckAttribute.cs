using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Common
{
    /// <summary>
    /// Opt a controller or action out of <see cref="HospitalAccessFilter"/> — for identity/setup
    /// endpoints (auth, hospital registration, chains, invitations) where the caller may legitimately
    /// act before/across hospital membership.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class SkipHospitalAccessCheckAttribute : Attribute
    {
    }
}
