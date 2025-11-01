using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
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
    }

    public class Roles
    {
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = null!;
    }
}
