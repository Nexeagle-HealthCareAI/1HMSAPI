using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class OtpVerifyHandler : IRequestHandler<OtpVerifyRequestModel, OtpVerifyResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IJwtAuthService _jwtAuthService;
        private readonly IConfiguration _configuration;

        public OtpVerifyHandler(AppDbContext context, IJwtAuthService jwtAuthService, IConfiguration configuration)
        {
            _context = context;
            _jwtAuthService = jwtAuthService;
            _configuration = configuration;
        }

        public async Task<OtpVerifyResponseModel> Handle(OtpVerifyRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.MobileNumber) || string.IsNullOrEmpty(request.Otp))
            {
                return new OtpVerifyResponseModel
                {
                    Success = false,
                    Message = "Mobile number and OTP are required."
                };
            }
            else
            {
                var currentDateTime = DateTime.Now;
                var user = await _context.Users.FirstOrDefaultAsync(u => u.MobileNumber == request.MobileNumber && u.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);
                if (user != null)
                {
                    var userAuth = await _context.UserAuths.FirstOrDefaultAsync(ua => ua.UserID == user.UserID , cancellationToken);
                    if (userAuth != null)
                    {
                        var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.UserID == user.UserID, cancellationToken);
                        // Compute hash of incoming OTP with the same pepper and compare against stored Base64 hash
                        var pepper = _configuration["Security:OtpPepper"] ?? string.Empty;
                        var key = Encoding.UTF8.GetBytes(pepper);
                        var data = Encoding.UTF8.GetBytes(request.Otp);
                        var incomingHash = HMACSHA256.HashData(key, data);

                        bool isMatch = false;
                        if (!string.IsNullOrEmpty(userAuth.Otp))
                        {
                            try
                            {
                                //var storedHash = Convert.FromBase64String(userAuth.Otp);
                                //isMatch = CryptographicOperations.FixedTimeEquals(incomingHash, storedHash);
                                if(userAuth.Otp == request.Otp)
                                {
                                    isMatch = true;
                                }
                                
                            }
                            catch
                            {
                                isMatch = false;
                            }
                        }

                        if (isMatch && userAuth.IsOtpUsed == false)
                        {
                            userAuth.IsOtpUsed = true;
                            userAuth.IsLocked = false;
                            userAuth.UserStatusId = (int)UserStatusEnum.Active;
                            user.UserStatusId = (int)UserStatusEnum.Active;
                            if (userProfile != null)
                            {
                                userProfile.UserStatusId = (int)UserStatusEnum.Active;
                            }
                            
                            var userHistory = new UserHistory
                            {
                                UserId = user.UserID,
                                UserStatusId = (int)UserStatusEnum.Active,
                                UpdatedBy = user.UserID,
                                UpdatedDate = currentDateTime
                            };
                            _context.UserHistories.Add(userHistory);

                            await _context.SaveChangesAsync(cancellationToken);

                            List<Claim> claims = new()
                            {
                                new Claim(ClaimTypes.MobilePhone, request.MobileNumber),
                                new Claim("userId", user.UserID.ToString()),
                            };
                            var accessToken = _jwtAuthService.GenerateJwtToken(claims);

                            return new OtpVerifyResponseModel
                            {
                                Success = true,
                                Message = "OTP verified successfully.",
                                UserId = user.UserID,
                                AccessToken = accessToken
                            };
                        }
                        else
                        {
                            if (!userAuth.OtpExpireAt.HasValue || userAuth.OtpExpireAt.Value < currentDateTime)
                            {
                                return new OtpVerifyResponseModel
                                {
                                    Success = false,
                                    Message = "OTP has expired."
                                };
                            }
                            return new OtpVerifyResponseModel
                            {
                                Success = false,
                                Message = "Invalid or already used OTP."
                            };
                        }
                    }
                    else
                    {
                        return new OtpVerifyResponseModel
                        {
                            Success = false,
                            Message = "UserAuth not found for user."
                        };
                    }
                }
                else
                {
                    return new OtpVerifyResponseModel
                    {
                        Success = false,
                        Message = "Invalid Mobile No."
                    };
                }
            }
        }
    }
}