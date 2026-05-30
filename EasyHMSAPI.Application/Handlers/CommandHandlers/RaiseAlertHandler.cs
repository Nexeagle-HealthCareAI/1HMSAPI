using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RaiseAlertHandler : IRequestHandler<RaiseAlertRequestModel, RaiseAlertResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;

        public RaiseAlertHandler(AppDbContext context, ISmsService smsService)
        {
            _context = context;
            _smsService = smsService;
        }

        public async Task<RaiseAlertResponseModel> Handle(RaiseAlertRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.AlertCode) || string.IsNullOrWhiteSpace(request.Title))
                {
                    return new RaiseAlertResponseModel { Success = false, Message = "hospitalId, alertCode and title are required." };
                }

                var now = DateTime.UtcNow;
                var alert = new Alert
                {
                    AlertId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AlertCode = request.AlertCode,
                    Severity = string.IsNullOrWhiteSpace(request.Severity) ? "INFO" : request.Severity!,
                    Title = request.Title,
                    Body = request.Body,
                    PatientId = request.PatientId,
                    AdmissionId = request.AdmissionId,
                    EncounterId = request.EncounterId,
                    AudienceRoles = (request.AudienceRoles != null && request.AudienceRoles.Count > 0)
                        ? string.Join(",", request.AudienceRoles)
                        : null,
                    AudienceUserId = request.AudienceUserId,
                    AudienceWardCode = request.AudienceWardCode,
                    Status = "ACTIVE",
                    RaisedAt = now,
                    RaisedBy = request.LoggedInUserName,
                    RaisedByUserId = request.LoggedInUserId,
                    SourceModule = request.SourceModule,
                    SourceRefId = request.SourceRefId,
                    DispatchSms = request.DispatchSms ?? false,
                    DispatchWhatsApp = request.DispatchWhatsApp ?? false,
                    DispatchInApp = request.DispatchInApp ?? true,
                    DispatchToPhone = request.DispatchToPhone,
                    PayloadJson = request.PayloadJson,
                    CreatedAt = now,
                };

                bool smsSent = false;
                if (alert.DispatchSms)
                {
                    var phone = request.DispatchToPhone;
                    if (string.IsNullOrWhiteSpace(phone) && !string.IsNullOrWhiteSpace(request.PatientId))
                    {
                        phone = await _context.PatientRegistrations.AsNoTracking()
                            .Where(p => p.PatientId == request.PatientId)
                            .Select(p => p.Mobile)
                            .FirstOrDefaultAsync(cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(phone))
                    {
                        var message = string.IsNullOrWhiteSpace(alert.Body) ? alert.Title : $"{alert.Title}: {alert.Body}";
                        try
                        {
                            smsSent = await _smsService.SendInvitationSmsAsync(phone!, message);
                            alert.DispatchedAt = smsSent ? DateTime.UtcNow : null;
                            if (!smsSent) alert.DispatchError = "SMS provider returned failure.";
                        }
                        catch (Exception smsEx)
                        {
                            alert.DispatchError = smsEx.Message;
                        }
                    }
                    else
                    {
                        alert.DispatchError = "No phone number available for SMS dispatch.";
                    }
                }

                _context.Alert.Add(alert);
                await _context.SaveChangesAsync(cancellationToken);

                return new RaiseAlertResponseModel
                {
                    Success = true,
                    Message = "Alert raised.",
                    AlertId = alert.AlertId,
                    SmsSent = smsSent,
                    WhatsAppSent = false,   // WhatsApp free-text dispatch not yet supported.
                };
            }
            catch (Exception ex)
            {
                return new RaiseAlertResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
