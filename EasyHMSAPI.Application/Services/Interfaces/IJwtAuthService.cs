using System.Security.Claims;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    public interface IJwtAuthService
    {
        string GenerateJwtToken(List<Claim> claims);
    }
}
