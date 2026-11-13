using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RecordAbdmProfileShareHandler : IRequestHandler<RecordAbdmProfileShareRequestModel, RecordAbdmProfileShareResponseModel>
    {
        private const int MaxRawPayloadChars = 20000;
        private readonly AppDbContext _context;
        private readonly ILogger<RecordAbdmProfileShareHandler> _logger;

        public RecordAbdmProfileShareHandler(AppDbContext context, ILogger<RecordAbdmProfileShareHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<RecordAbdmProfileShareResponseModel> Handle(RecordAbdmProfileShareRequestModel request, CancellationToken cancellationToken)
        {
            ParsedProfileShare parsed;
            try
            {
                using var doc = JsonDocument.Parse(request.RawBody);
                parsed = AbdmProfileShareParser.Parse(doc.RootElement);
            }
            catch (JsonException)
            {
                _logger.LogWarning("ABDM profile share callback body was not valid JSON.");
                return new RecordAbdmProfileShareResponseModel { Accepted = false, Message = "Body was not valid JSON." };
            }

            var callbackRequestId = !string.IsNullOrWhiteSpace(request.HeaderRequestId) ? request.HeaderRequestId!.Trim() : parsed.RequestId;
            // No id anywhere: derive one from the body so an identical ABDM retry still de-duplicates.
            var storedRequestId = callbackRequestId ?? "body-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RawBody)))[..32];

            if (string.IsNullOrWhiteSpace(parsed.HipId))
            {
                _logger.LogWarning("ABDM profile share {RequestId} carried no recognizable HIP id — raw body: {Body}", storedRequestId, Truncate(request.RawBody));
                return new RecordAbdmProfileShareResponseModel { Accepted = false, CallbackRequestId = callbackRequestId, Message = "No HIP id in payload." };
            }

            var hipLower = parsed.HipId.ToLower();
            var facility = await _context.AbdmFacilities.AsNoTracking()
                .FirstOrDefaultAsync(f => f.HipId.ToLower() == hipLower, cancellationToken);
            if (facility == null)
            {
                _logger.LogWarning("ABDM profile share {RequestId} is for HIP id {HipId}, which no hospital has registered.", storedRequestId, parsed.HipId);
                return new RecordAbdmProfileShareResponseModel { Accepted = false, CallbackRequestId = callbackRequestId, Message = "Unknown HIP id." };
            }

            var already = await _context.AbdmProfileShares.AnyAsync(
                s => s.HospitalId == facility.HospitalId && s.RequestId == storedRequestId, cancellationToken);
            if (already)
                return new RecordAbdmProfileShareResponseModel { Accepted = true, Duplicate = true, CallbackRequestId = callbackRequestId, AbhaAddress = parsed.AbhaAddress };

            _context.AbdmProfileShares.Add(new AbdmProfileShare
            {
                ProfileShareId = Guid.NewGuid(),
                HospitalId = facility.HospitalId,
                HipId = parsed.HipId,
                CounterId = parsed.CounterId,
                RequestId = storedRequestId,
                AbhaNumber = parsed.AbhaNumber,
                AbhaAddress = parsed.AbhaAddress,
                FullName = parsed.FullName,
                Gender = parsed.Gender,
                DateOfBirth = parsed.DateOfBirth,
                Mobile = parsed.Mobile,
                Address = parsed.Address,
                LinkToken = parsed.LinkToken,
                StatusCode = "NEW",
                RawPayload = Truncate(request.RawBody),
                ReceivedAt = DateTime.UtcNow
            });

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A concurrent retry of the same callback won the unique (HospitalId, RequestId) race.
                return new RecordAbdmProfileShareResponseModel { Accepted = true, Duplicate = true, CallbackRequestId = callbackRequestId, AbhaAddress = parsed.AbhaAddress };
            }

            return new RecordAbdmProfileShareResponseModel { Accepted = true, CallbackRequestId = callbackRequestId, AbhaAddress = parsed.AbhaAddress };
        }

        private static string Truncate(string s) => s.Length <= MaxRawPayloadChars ? s : s[..MaxRawPayloadChars];
    }

    public class HandleAbdmProfileShareHandler : IRequestHandler<HandleAbdmProfileShareRequestModel, HandleAbdmProfileShareResponseModel>
    {
        private readonly AppDbContext _context;

        public HandleAbdmProfileShareHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HandleAbdmProfileShareResponseModel> Handle(HandleAbdmProfileShareRequestModel request, CancellationToken cancellationToken)
        {
            var share = await _context.AbdmProfileShares.FirstOrDefaultAsync(
                s => s.ProfileShareId == request.ProfileShareId && s.HospitalId == request.HospitalId, cancellationToken);
            if (share == null)
                return new HandleAbdmProfileShareResponseModel { Success = false, Message = "Scanned profile not found." };

            if (share.StatusCode != "HANDLED")
            {
                share.StatusCode = "HANDLED";
                share.HandledAt = DateTime.UtcNow;
                share.HandledBy = request.LoggedInUserName;
            }

            // Keep the ABHA on record, same upsert-by-(hospital, ABHA number) as the OTP link flow.
            if (!string.IsNullOrWhiteSpace(share.AbhaNumber))
            {
                var account = await _context.AbhaAccount.FirstOrDefaultAsync(
                    a => a.HospitalId == share.HospitalId && a.AbhaNumber == share.AbhaNumber, cancellationToken);
                if (account != null)
                {
                    account.AbhaAddress = share.AbhaAddress ?? account.AbhaAddress;
                    account.FullName = share.FullName ?? account.FullName;
                    account.Gender = share.Gender ?? account.Gender;
                    account.DateOfBirth = share.DateOfBirth ?? account.DateOfBirth;
                    account.Mobile = share.Mobile ?? account.Mobile;
                }
                else
                {
                    _context.AbhaAccount.Add(new AbhaAccount
                    {
                        AbhaAccountId = Guid.NewGuid(),
                        HospitalId = share.HospitalId,
                        AbhaNumber = share.AbhaNumber!,
                        AbhaAddress = share.AbhaAddress,
                        FullName = share.FullName,
                        Gender = share.Gender,
                        DateOfBirth = share.DateOfBirth,
                        Mobile = share.Mobile,
                        Source = "FacilityQR",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = request.LoggedInUserName
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return new HandleAbdmProfileShareResponseModel { Success = true, Message = "Marked as handled." };
        }
    }

    public class SaveAbdmFacilityHandler : IRequestHandler<SaveAbdmFacilityRequestModel, SaveAbdmFacilityResponseModel>
    {
        private readonly AppDbContext _context;

        public SaveAbdmFacilityHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SaveAbdmFacilityResponseModel> Handle(SaveAbdmFacilityRequestModel request, CancellationToken cancellationToken)
        {
            var hipId = request.HipId?.Trim();
            if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(hipId))
                return new SaveAbdmFacilityResponseModel { Success = false, Message = "Hospital and HIP ID are required." };

            var hipLower = hipId.ToLower();
            var takenByOther = await _context.AbdmFacilities.AnyAsync(
                f => f.HipId.ToLower() == hipLower && f.HospitalId != request.HospitalId, cancellationToken);
            if (takenByOther)
                return new SaveAbdmFacilityResponseModel { Success = false, Message = "That HIP ID is already registered to another hospital." };

            var existing = await _context.AbdmFacilities.FirstOrDefaultAsync(f => f.HospitalId == request.HospitalId, cancellationToken);
            if (existing != null)
            {
                existing.HipId = hipId;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = request.LoggedInUserName;
            }
            else
            {
                _context.AbdmFacilities.Add(new AbdmFacility
                {
                    HospitalId = request.HospitalId,
                    HipId = hipId,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = request.LoggedInUserName
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            return new SaveAbdmFacilityResponseModel { Success = true, Message = "HIP ID saved." };
        }
    }
}
