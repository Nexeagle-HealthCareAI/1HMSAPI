using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>Lists doctors across the caller's owned chain, each with the chain hospitals they work at.</summary>
    public class GetChainDoctorsHandler : IRequestHandler<GetChainDoctorsRequestModel, GetChainDoctorsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetChainDoctorsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetChainDoctorsResponseModel> Handle(GetChainDoctorsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.UserId == Guid.Empty)
                    return new GetChainDoctorsResponseModel { Success = false, Message = "UserId is required." };

                var chain = await _context.HospitalChains
                    .Where(c => c.OwnerUserId == request.UserId)
                    .OrderBy(c => c.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (chain == null)
                    return new GetChainDoctorsResponseModel { Success = true, ChainId = null };

                var chainHospitalIds = await _context.Hospitals
                    .Where(h => h.ChainId == chain.ChainId)
                    .Select(h => h.HospitalID)
                    .ToListAsync(cancellationToken);

                // Doctor memberships within the chain (a user with both a HospitalUser row and a Doctor row).
                var rows = await (
                    from hu in _context.HospitalUsers
                    where chainHospitalIds.Contains(hu.HospitalID)
                    join d in _context.Doctors on hu.UserID equals d.UserID
                    join h in _context.Hospitals on hu.HospitalID equals h.HospitalID
                    select new { d.DoctorID, d.UserID, hu.HospitalID, HospitalName = h.Name }
                ).ToListAsync(cancellationToken);

                var userIds = rows.Select(r => r.UserID).Distinct().ToList();
                var names = await _context.UserProfiles
                    .Where(up => userIds.Contains(up.UserID))
                    .OrderByDescending(up => up.UpdatedAt)
                    .Select(up => new { up.UserID, up.FullName })
                    .ToListAsync(cancellationToken);
                var nameByUser = names
                    .GroupBy(n => n.UserID)
                    .ToDictionary(g => g.Key, g => g.First().FullName);

                var doctors = rows
                    .GroupBy(r => new { r.DoctorID, r.UserID })
                    .Select(g => new ChainDoctorItem
                    {
                        DoctorId = g.Key.DoctorID,
                        UserId = g.Key.UserID,
                        FullName = nameByUser.TryGetValue(g.Key.UserID, out var n) ? n : null,
                        Hospitals = g
                            .GroupBy(x => x.HospitalID)
                            .Select(hg => new ChainDoctorHospital { HospitalId = hg.Key, Name = hg.First().HospitalName })
                            .OrderBy(h => h.Name)
                            .ToList(),
                    })
                    .OrderBy(d => d.FullName)
                    .ToList();

                return new GetChainDoctorsResponseModel { Success = true, ChainId = chain.ChainId, Doctors = doctors };
            }
            catch (Exception)
            {
                return new GetChainDoctorsResponseModel { Success = false, Message = "Error loading chain doctors." };
            }
        }
    }
}
