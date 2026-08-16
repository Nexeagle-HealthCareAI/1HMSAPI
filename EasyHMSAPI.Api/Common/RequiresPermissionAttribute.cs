namespace EasyHMSAPI.Api.Common
{
    /// <summary>
    /// Marks a controller/action as gated behind one of the given PermissionKeys (OR
    /// semantics -- holding any one is enough). Read by PermissionAuthorizationFilter.
    /// Opt-in (unlike HospitalAccessFilter's opt-out [SkipHospitalAccessCheck]) since
    /// board coverage is being rolled out incrementally across existing controllers --
    /// an unannotated controller behaves exactly as it does today, bare [Authorize].
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class RequiresPermissionAttribute : Attribute
    {
        public string[] PermissionKeys { get; }

        public RequiresPermissionAttribute(params string[] permissionKeys)
        {
            PermissionKeys = permissionKeys;
        }
    }
}
