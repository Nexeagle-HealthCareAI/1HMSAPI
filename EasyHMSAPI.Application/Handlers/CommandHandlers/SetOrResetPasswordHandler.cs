using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
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

        public SetOrResetPasswordHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SetOrResetPasswordResponseModel> Handle(SetOrResetPasswordRequestModel request, CancellationToken cancellationToken)
        {
            var scope = request.Scope?.ToLowerInvariant();
            SetOrResetPasswordResponseModel response = new();

            var user = await _context.Users
                .Where(x => x.UserID == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);
            if (user != null) 
            {
                var userAuth = await _context.UserAuths
                    .Where(x => x.UserID == request.UserId)
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
                            var incomingHashed = BitConverter.ToString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Password))).Replace("-", "").ToLower();
                            if (userAuth.HashedPassword == incomingHashed)
                            {
                                return new SetOrResetPasswordResponseModel
                                {
                                    Success = false,
                                    Message = "New password cannot be same as the current password."
                                };
                            }
                            else
                            {
                                var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.Password));
                                userAuth.HashedPassword = BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();

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
                        if (!string.IsNullOrEmpty(userAuth.HashedPassword))
                        {
                            var incomingHashed = BitConverter.ToString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Password))).Replace("-", "").ToLower();
                            if (userAuth.HashedPassword == incomingHashed)
                            {
                                return new SetOrResetPasswordResponseModel
                                {
                                    Success = false,
                                    Message = "New password cannot be same as the current password."
                                };
                            }
                            else
                            {
                                var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.Password));
                                userAuth.HashedPassword = BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();

                                await _context.SaveChangesAsync(cancellationToken);

                                return new SetOrResetPasswordResponseModel
                                {
                                    Success = true,
                                    Message = "Password successfully reset."
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
