using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetImplantLogHandler : IRequestHandler<GetImplantLogRequestModel, GetImplantLogResponseModel>
    {
        private readonly AppDbContext _context;

        public GetImplantLogHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetImplantLogResponseModel> Handle(GetImplantLogRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.IntraOpItemUsage
                .Where(u => u.HospitalId == request.HospitalId && u.Category == IpdConstants.IntraOpItemCategory.Implant);

            if (!string.IsNullOrWhiteSpace(request.LotNumber))
                query = query.Where(u => u.LotNumber == request.LotNumber.Trim());
            if (!string.IsNullOrWhiteSpace(request.SerialNumber))
                query = query.Where(u => u.SerialNumber == request.SerialNumber.Trim());

            var usages = await query.OrderByDescending(u => u.RecordedAt).ToListAsync(cancellationToken);

            var caseIds = usages.Select(u => u.SurgeryCaseId).Distinct().ToList();
            var casesQuery = _context.SurgeryCase.Where(s => caseIds.Contains(s.SurgeryCaseId));
            if (request.AdmissionId.HasValue)
                casesQuery = casesQuery.Where(s => s.AdmissionId == request.AdmissionId.Value);
            var casesById = await casesQuery.ToDictionaryAsync(s => s.SurgeryCaseId, cancellationToken);

            usages = usages.Where(u => casesById.ContainsKey(u.SurgeryCaseId)).ToList();

            var patientIds = casesById.Values.Select(c => c.PatientId).Where(p => p != null).Distinct().ToList();
            var patientsById = await _context.PatientRegistrations
                .Where(p => p.HospitalId == request.HospitalId && patientIds.Contains(p.PatientId))
                .ToDictionaryAsync(p => p.PatientId!, cancellationToken);

            var entries = usages.Select(u =>
            {
                casesById.TryGetValue(u.SurgeryCaseId, out var surgeryCase);
                string? patientName = null;
                if (surgeryCase?.PatientId != null && patientsById.TryGetValue(surgeryCase.PatientId, out var patient))
                    patientName = patient.FullName;

                return new ImplantLogEntryDataModel
                {
                    IntraOpItemUsageId = u.IntraOpItemUsageId,
                    SurgeryCaseId = u.SurgeryCaseId,
                    AdmissionId = surgeryCase?.AdmissionId ?? Guid.Empty,
                    PatientId = surgeryCase?.PatientId,
                    PatientName = patientName,
                    ProcedureName = surgeryCase?.ProcedureName,
                    ItemName = u.ItemName,
                    Qty = u.Qty,
                    LotNumber = u.LotNumber,
                    SerialNumber = u.SerialNumber,
                    RecordedAt = u.RecordedAt,
                };
            }).ToList();

            return new GetImplantLogResponseModel { Entries = entries };
        }
    }
}
