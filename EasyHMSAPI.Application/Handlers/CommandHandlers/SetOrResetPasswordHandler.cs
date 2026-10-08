using EasyHMSAPI.Application.Services;
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
                        // The admin's name (registration). Validated and applied together with the email/password below; nothing is saved
                        // unless the whole step succeeds.
                        var fullName = request.FullName?.Trim();
                        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserID == user.UserID, cancellationToken);
                        if (!string.IsNullOrEmpty(fullName))
                        {
                            if (fullName.Length < 2 || fullName.Length > 100)
                                return new SetOrResetPasswordResponseModel { Success = false, Message = "Enter your full name (2 to 100 characters)." };
                            if (profile != null) profile.FullName = fullName;
                        }
                        else if (profile != null && string.IsNullOrWhiteSpace(profile.FullName))
                        {
                            return new SetOrResetPasswordResponseModel { Success = false, Message = "Your name is required." };
                        }

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
                            bool passwordMatch = PasswordHasher.Verify(request.Password, userAuth.HashedPassword, _maskingService);

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
                                userAuth.HashedPassword = PasswordHasher.Hash(request.Password);

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
                            // Only compare passwords if there's an existing stored password
                            if (!string.IsNullOrEmpty(userAuth.HashedPassword)
                                && PasswordHasher.Verify(request.Password, userAuth.HashedPassword, _maskingService))
                            {
                                return new SetOrResetPasswordResponseModel
                                {
                                    Success = false,
                                    Message = "New password cannot be same as the current password."
                                };
                            }

                            // Update password (whether stored password was empty or different)
                            userAuth.HashedPassword = PasswordHasher.Hash(request.Password);

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
                    else if (scope?.ToLower() == "change-password")
                    {
                        if (string.IsNullOrEmpty(request.CurrentPassword))
                        {
                            return new SetOrResetPasswordResponseModel
                            {
                                Success = false,
                                Message = "Current password is required."
                            };
                        }
                        if (string.IsNullOrEmpty(request.Password))
                        {
                            return new SetOrResetPasswordResponseModel
                            {
                                Success = false,
                                Message = "Password cannot be empty"
                            };
                        }

                        bool currentMatches = PasswordHasher.Verify(request.CurrentPassword, userAuth.HashedPassword, _maskingService);
                        if (!currentMatches)
                        {
                            return new SetOrResetPasswordResponseModel
                            {
                                Success = false,
                                Message = "Current password is incorrect."
                            };
                        }

                        bool sameAsCurrent = PasswordHasher.Verify(request.Password, userAuth.HashedPassword, _maskingService);
                        if (sameAsCurrent)
                        {
                            return new SetOrResetPasswordResponseModel
                            {
                                Success = false,
                                Message = "New password cannot be same as the current password."
                            };
                        }

                        userAuth.HashedPassword = PasswordHasher.Hash(request.Password);
                        await _context.SaveChangesAsync(cancellationToken);

                        return new SetOrResetPasswordResponseModel
                        {
                            Success = true,
                            Message = "Password changed successfully."
                        };
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

    }

}
