using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetBloodBankLedgerHandler : IRequestHandler<GetBloodBankLedgerRequestModel, GetBloodBankLedgerResponseModel>
    {
        private readonly AppDbContext _context;

        public GetBloodBankLedgerHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetBloodBankLedgerResponseModel> Handle(GetBloodBankLedgerRequestModel request, CancellationToken cancellationToken)
        {
            var rows = await (
                from t in _context.TransfusionEvent
                join b in _context.BloodBag on t.BloodBagId equals b.BloodBagId
                where t.HospitalId == request.HospitalId
                orderby t.StartedAt descending
                select new BloodBankLedgerRow
                {
                    TransfusionEventId = t.TransfusionEventId,
                    BagNumber = b.BagNumber,
                    Component = b.Component,
                    BloodGroup = b.BloodGroup,
                    PatientId = t.PatientId,
                    StartedAt = t.StartedAt,
                    VolumeGivenMl = t.VolumeGivenMl,
                    Reaction = t.Reaction,
                    AdministeredBy = t.AdministeredBy,
                    WitnessName = t.WitnessName,
                })
                .Take(500)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
                return new GetBloodBankLedgerResponseModel { Transfusions = rows };

            var patientIds = rows.Where(r => r.PatientId != null).Select(r => r.PatientId!).Distinct().ToList();
            var namesByPatientId = patientIds.Count == 0
                ? new System.Collections.Generic.Dictionary<string, string>()
                : await _context.PatientRegistrations
                    .Where(p => patientIds.Contains(p.PatientId))
                    .Select(p => new { p.PatientId, p.FullName })
                    .ToDictionaryAsync(p => p.PatientId, p => p.FullName, cancellationToken);

            foreach (var row in rows)
            {
                if (row.PatientId != null && namesByPatientId.TryGetValue(row.PatientId, out var name))
                    row.PatientName = name;
            }

            return new GetBloodBankLedgerResponseModel { Transfusions = rows };
        }
    }
}
