using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class SetOrResetPasswordHandler : IRequestHandler<SetOrResetPasswordRequestModel, SetOrResetPasswordResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMaskingService _maskingService;

        public SetOrResetPasswordHandler(AppDbContext context, IMaskingService maskingService)
        {
            _context = context;
            _maskingService = maskingService;
        }

        public async Task<SetOrResetPasswordResponseModel> Handle(SetOrResetPasswordRequestModel request, CancellationToken cancellationToken)
        {
            var scope = request.Scope?.ToLowerInvariant();
            SetOrResetPasswordResponseModel response = new();

            var user = await _context.Users
                .Where(x => x.UserID == request.UserId && x.UserStatusId != (int)UserStatusEnum.Revoked)
                .FirstOrDefaultAsync(cancellationToken);
            if (user != null) 
            {
                var userAuth = await _context.UserAuths
                    .Where(x => x.UserID == user.UserID)
                    .FirstOrDefaultAsync(cancellationToken);

                if (userAuth != null)
                {
                    if (scope?.ToLower() == "set-password")
                    {
                        if(!string.IsNullOrEmpty(request.Email))
                        {
                            bool emailExists = await _context.Users.AnyAsync(x => x.Email == request.Email.ToLower() && x.UserID != user.UserID, cancellationToken);
                            if(emailExists)
                            {
                                return new SetOrResetPasswordResponseModel
                                {
                                    Success = false,
                                    Message = "Email is already in use by another user."
                                };
                            }
                            else
                            {
                                if(user.Email != request.Email.ToLower())
                                {
                                    user.Email = request.Email.ToLower();
                                }
                                else
                                {
                                    return new SetOrResetPasswordResponseModel
                                    {
                                        Success = false,
                                        Message = "Email cannot be same as the current email."
                                    };
                                }
                            }
                        }
                        else
                        {
                            return new SetOrResetPasswordResponseModel
                            {
                                Success = false,
                                Message = "Email cannot be empty."
                            };
                        }

                        if (!string.IsNullOrEmpty(request.Password))
                        {
                            // Hash the incoming password once
                            var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.Password));
                            var hashedPassword = BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();

                            // Compare stored password (which may be masked) with incoming password
                            bool passwordMatch = false;
                            if (_maskingService.IsMaskingEnabled())
                            {
                                // Mask the incoming password and compare with stored masked password
                                var maskedIncomingPassword = _maskingService.Mask(hashedPassword);
                                passwordMatch = userAuth.HashedPassword == maskedIncomingPassword;
                            }
                            else
                            {
                                // Direct comparison when masking is disabled
                                passwordMatch = userAuth.HashedPassword == hashedPassword;
                            }

                            if (passwordMatch)
                            {
                                return new SetOrResetPasswordResponseModel
                                {
                                    Success = false,
                                    Message = "New password cannot be same as the current password."
                                };
                            }
                            else
                            {
                                // Apply masking if enabled
                                if (_maskingService.IsMaskingEnabled())
                                {
                                    userAuth.HashedPassword = _maskingService.Mask(hashedPassword);
                                }
                                else
                                {
                                    userAuth.HashedPassword = hashedPassword;
                                }

                                await _context.SaveChangesAsync(cancellationToken);

                                return new SetOrResetPasswordResponseModel
                                {
                                    Success = true,
                                    Message = "Email and password successfully updated."
                                };
                            }
                        }
                        else
                        {
                            return new SetOrResetPasswordResponseModel
                            {
                                Success = false,
                                Message = "Password cannot be empty"
                            };
                        }
                    }
                    else if (scope?.ToLower() == "reset-password")
                    {
                        if (!string.IsNullOrEmpty(request.Password))
                        {
                            // Hash the incoming password once
                            var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.Password));
                            var hashedPassword = BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();

                            // Only compare passwords if there's an existing stored password
                            if (!string.IsNullOrEmpty(userAuth.HashedPassword))
                            {
                                // Compare stored password (which may be masked) with incoming password
                                bool passwordMatch = false;
                                if (_maskingService.IsMaskingEnabled())
                                {
                                    // Mask the incoming password and compare with stored masked password
                                    var maskedIncomingPassword = _maskingService.Mask(hashedPassword);
                                    passwordMatch = userAuth.HashedPassword == maskedIncomingPassword;
                                }
                                else
                                {
                                    // Direct comparison when masking is disabled
                                    passwordMatch = userAuth.HashedPassword == hashedPassword;
                                }

                                if (passwordMatch)
                                {
                                    return new SetOrResetPasswordResponseModel
                                    {
                                        Success = false,
                                        Message = "New password cannot be same as the current password."
                                    };
                                }
                            }

                            // Update password (whether stored password was empty or different)
                            // Apply masking if enabled
                            if (_maskingService.IsMaskingEnabled())
                            {
                                userAuth.HashedPassword = _maskingService.Mask(hashedPassword);
                            }
                            else
                            {
                                userAuth.HashedPassword = hashedPassword;
                            }

                            await _context.SaveChangesAsync(cancellationToken);

                            return new SetOrResetPasswordResponseModel
                            {
                                Success = true,
                                Message = "Password successfully reset."
                            };
                        }
                        else
                        {
                            return new SetOrResetPasswordResponseModel
                            {
                                Success = false,
                                Message = "Password cannot be empty"
                            };
                        }
                    }
                    else
                    {
                        return new SetOrResetPasswordResponseModel
                        {
                            Success = false,
                            Message = "Invalid scope provided."
                        };
                    }
                }
                else
                {
                    return new SetOrResetPasswordResponseModel
                    {
                        Success = false,
                        Message = "User not found."
                    };
                }
            }
            else
            {
                return new SetOrResetPasswordResponseModel
                {
                    Success = false,
                    Message = "User not found."
                };
            }
        }

        private static string HashPassword(string password)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
        }
    }

}
