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

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class OtpVerifyHandler : IRequestHandler<OtpVerifyRequestModel, OtpVerifyResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IJwtAuthService _jwtAuthService;
        private readonly IConfiguration _configuration;
        private readonly IMaskingService _maskingService;

        public OtpVerifyHandler(AppDbContext context, IJwtAuthService jwtAuthService, IConfiguration configuration, IMaskingService maskingService)
        {
            _context = context;
            _jwtAuthService = jwtAuthService;
            _configuration = configuration;
            _maskingService = maskingService;
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
                const int maxFailedOtpAttempts = 5;
                var currentDateTime = DateTime.UtcNow;
                var user = await _context.Users.FirstOrDefaultAsync(u => u.MobileNumber == request.MobileNumber && u.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);
                if (user != null)
                {
                    var userAuth = await _context.UserAuths.FirstOrDefaultAsync(ua => ua.UserID == user.UserID , cancellationToken);
                    if (userAuth != null)
                    {
                        var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.UserID == user.UserID, cancellationToken);

                        // Throttle brute force: too many wrong guesses lock the current OTP window.
                        // Requesting a fresh OTP (OtpSendHandler) clears this lock.
                        if (userAuth.IsLocked)
                        {
                            return new OtpVerifyResponseModel
                            {
                                Success = false,
                                Message = "Too many incorrect attempts. Please request a new OTP."
                            };
                        }

                        // Check if masking is enabled and compare accordingly
                        bool isMatch = false;
                        if (!string.IsNullOrEmpty(userAuth.Otp))
                        {
                            try
                            {
                                if (_maskingService.IsMaskingEnabled())
                                {
                                    // Mask the incoming OTP and compare with stored masked OTP
                                    var incomingMaskedOtp = _maskingService.Mask(request.Otp);
                                    isMatch = userAuth.Otp == incomingMaskedOtp;
                                }
                                else
                                {
                                    // Direct comparison when masking is disabled
                                    isMatch = userAuth.Otp == request.Otp;
                                }
                            }
                            catch
                            {
                                isMatch = false;
                            }
                        }

                        bool isExpired = !userAuth.OtpExpireAt.HasValue || userAuth.OtpExpireAt.Value < currentDateTime;

                        if (isMatch && userAuth.IsOtpUsed == false && !isExpired)
                        {
                            userAuth.IsOtpUsed = true;
                            userAuth.IsLocked = false;
                            userAuth.FailedLoginAttempts = 0;
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
                            if (isExpired)
                            {
                                return new OtpVerifyResponseModel
                                {
                                    Success = false,
                                    Message = "OTP has expired."
                                };
                            }

                            // Wrong (or replayed) OTP within the validity window: count it and lock
                            // the window once the attempt ceiling is hit.
                            userAuth.FailedLoginAttempts += 1;
                            if (userAuth.FailedLoginAttempts >= maxFailedOtpAttempts)
                            {
                                userAuth.IsLocked = true;
                            }
                            await _context.SaveChangesAsync(cancellationToken);

                            return new OtpVerifyResponseModel
                            {
                                Success = false,
                                Message = userAuth.IsLocked
                                    ? "Too many incorrect attempts. Please request a new OTP."
                                    : "Invalid or already used OTP."
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