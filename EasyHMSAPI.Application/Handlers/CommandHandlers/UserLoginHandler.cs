using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UserLoginHandler : IRequestHandler<UserLoginRequestModel, UserLoginResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IJwtAuthService _jwtAuthService;
        private readonly IConfiguration _configuration;

        public UserLoginHandler(AppDbContext context, IJwtAuthService jwtAuthService, IConfiguration configuration)
        {
            _context = context;
            _jwtAuthService = jwtAuthService;
            _configuration = configuration;
        }

        public async Task<UserLoginResponseModel> Handle(UserLoginRequestModel request, CancellationToken cancellationToken)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                try
                {
                    string? accessToken = null;
                    var claims = new List<Claim>();

                    if (!request.IsLoginWithOtp)
                    {
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.EmailOrPhone && u.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);
                        user ??= await _context.Users.FirstOrDefaultAsync(u => u.MobileNumber == request.EmailOrPhone && u.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);


                        if (user != null)
                        {
                            var userAuth = await _context.UserAuths.FirstOrDefaultAsync(x => x.UserID == user.UserID, cancellationToken);
                            if(userAuth != null)
                            {
                                if (user.UserStatusId != (int)UserStatusEnum.Active || userAuth.IsLocked)
                                {
                                    return new UserLoginResponseModel
                                    {
                                        Success = false,
                                        Message = "User account is not active",
                                        AccessToken = accessToken
                                    };
                                }
                                
                                string hashedInputPassword = string.Empty;
                                if (!string.IsNullOrEmpty(request.Password))
                                {
                                    var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.Password));
                                    hashedInputPassword = BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
                                }
                                if (userAuth.HashedPassword == hashedInputPassword)
                                {
                                    claims.Add(new Claim(ClaimTypes.Email, user.Email ?? ""));
                                    claims.Add(new Claim(ClaimTypes.MobilePhone, user.MobileNumber ?? ""));
                                    claims.Add(new Claim("userId", user.UserID.ToString()));

                                    var userRoles = await _context.UserRoles
                                        .Where(ur => ur.UserID == user.UserID)
                                        .Join(_context.Roles, ur => ur.RoleID, r => r.RoleID, (ur, r) => r.RoleName)
                                        .ToListAsync(cancellationToken);
                                    var rolesString = string.Join(",", userRoles);
                                    claims.Add(new Claim("roles", rolesString));
                                    claims.Add(new Claim("isLoginWithOp", request.IsLoginWithOtp.ToString()));
                                    accessToken = _jwtAuthService.GenerateJwtToken(claims);

                                    userAuth.LastLoginTime = DateTime.UtcNow;
                                    userAuth.LoginMethod = "Password";
                                    await _context.SaveChangesAsync(cancellationToken);

                                    return new UserLoginResponseModel
                                    {
                                        Success = true,
                                        Message = "Login Successful",
                                        UserId = user.UserID,
                                        AccessToken = accessToken
                                    };
                                }
                                else
                                {
                                    userAuth.FailedLoginAttempts++;
                                    await _context.SaveChangesAsync(cancellationToken);

                                    return new UserLoginResponseModel
                                    {
                                        Success = false,
                                        Message = "Invalid Password",
                                        AccessToken = accessToken
                                    };
                                }
                            }
                            else 
                            {                           
                                return new UserLoginResponseModel
                                {
                                    Success = false,
                                    Message = "User not found",
                                    AccessToken = accessToken
                                };
                            }
                        }
                        else
                        {
                            await _context.SaveChangesAsync(cancellationToken);

                            return new UserLoginResponseModel
                            {
                                Success = false,
                                Message = "Invalid Email or Mobile Number",
                                AccessToken = accessToken
                            };
                        }
                    }
                    else
                    {
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.MobileNumber == request.EmailOrPhone, cancellationToken);
                        if (user != null)
                        {
                            var userAuth = await _context.UserAuths.FirstOrDefaultAsync(x => x.UserID == user.UserID, cancellationToken);
                            if (userAuth != null)
                            {
                                if (user.UserStatusId != (int)UserStatusEnum.Active  || userAuth.IsLocked)
                                {
                                    return new UserLoginResponseModel
                                    {
                                        Success = false,
                                        Message = userAuth.IsLocked ? "Account is locked" : "User account is not active",
                                        AccessToken = accessToken
                                    };
                                }
                                // Verify OTP using HMAC-SHA256 with pepper
                                var pepper = _configuration["Security:OtpPepper"] ?? string.Empty;
                                var key = Encoding.UTF8.GetBytes(pepper);
                                var data = Encoding.UTF8.GetBytes(request.Otp ?? string.Empty);
                                var incomingHash = HMACSHA256.HashData(key, data);
                                bool isMatch = false;

                                if (!string.IsNullOrEmpty(userAuth.Otp))
                                {
                                    try
                                    {
                                        //var storedHash = Convert.FromBase64String(userAuth.Otp);
                                        //isMatch = CryptographicOperations.FixedTimeEquals(incomingHash, storedHash);
                                        if (userAuth.Otp == request.Otp)
                                        {
                                            isMatch = true;
                                        }
                                    }
                                    catch
                                    {
                                        isMatch = false;
                                    }
                                }

                                if (isMatch && !userAuth.IsOtpUsed)
                                {
                                    // Check if OTP is expired
                                    if (!userAuth.OtpExpireAt.HasValue || userAuth.OtpExpireAt.Value < DateTime.UtcNow)
                                    {
                                        return new UserLoginResponseModel
                                        {
                                            Success = false,
                                            Message = "OTP has expired.",
                                            AccessToken = null
                                        };
                                    }

                                    claims.Add(new Claim(ClaimTypes.Email, user.Email ?? ""));
                                    claims.Add(new Claim(ClaimTypes.MobilePhone, user.MobileNumber ?? ""));
                                    claims.Add(new Claim("userId", user.UserID.ToString()));

                                    var userRoles = await _context.UserRoles
                                        .Where(ur => ur.UserID == user.UserID)
                                        .Join(_context.Roles, ur => ur.RoleID, r => r.RoleID, (ur, r) => r.RoleName)
                                        .ToListAsync(cancellationToken);
                                    var rolesString = string.Join(",", userRoles);
                                    claims.Add(new Claim("roles", rolesString));
                                    claims.Add(new Claim("isLoginWithOp", request.IsLoginWithOtp.ToString()));
                                    accessToken = _jwtAuthService.GenerateJwtToken(claims);

                                    userAuth.LastLoginTime = DateTime.UtcNow;
                                    userAuth.LoginMethod = "OTP";
                                    // Clear OTP after successful verification
                                    userAuth.IsOtpUsed = true;
                                    userAuth.Otp = null;
                                    await _context.SaveChangesAsync(cancellationToken);

                                    return new UserLoginResponseModel
                                    {
                                        Success = true,
                                        Message = "Login Successful",
                                        UserId = user.UserID,
                                        AccessToken = accessToken
                                    };
                                }
                                else
                                {
                                    userAuth.FailedLoginAttempts++;
                                    await _context.SaveChangesAsync(cancellationToken);

                                    return new UserLoginResponseModel
                                    {
                                        Success = false,
                                        Message = "Invalid OTP",
                                        AccessToken = accessToken
                                    };
                                }
                            }
                            else
                            {
                                return new UserLoginResponseModel
                                {
                                    Success = false,
                                    Message = "User not found",
                                    AccessToken = accessToken
                                };
                            }
                        }
                        else
                        {
                            return new UserLoginResponseModel
                            {
                                Success = false,
                                Message = "Invalid Email or Mobile Number",
                                AccessToken = accessToken
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    return new UserLoginResponseModel
                    {
                        Success = false,
                        Message = ex.Message,
                        AccessToken = null
                    };
                }
            });
        }
    }
}