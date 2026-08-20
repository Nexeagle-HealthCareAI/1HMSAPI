using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UserPermissionsResponseModel
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? RoleId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RoleName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? PermissionKeys { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Roles>? AllRoles { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? HospitalId { get; set; } // Added hospitalId to response

        /// <summary>
        /// Set by the handler when the caller queried someone else's UserId without holding
        /// admin_panel -- the controller maps this to a 403, never mixed with the
        /// user-not-found/no-roles null-response case (a different, non-authorization outcome).
        /// </summary>
        public bool Forbidden { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class Roles
    {
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = null!;
    }
}
