using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertPathologyReportKeywordHandler : IRequestHandler<UpsertPathologyReportKeywordRequestModel, UpsertPathologyReportKeywordResponseModel>
    {
        private readonly AppDbContext _context;

        public UpsertPathologyReportKeywordHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertPathologyReportKeywordResponseModel> Handle(UpsertPathologyReportKeywordRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.Keyword) || string.IsNullOrWhiteSpace(request.ContentJson))
                    return new UpsertPathologyReportKeywordResponseModel { Success = false, Message = "HospitalId, Keyword, and ContentJson are required." };

                var trimmedKeyword = request.Keyword.Trim();

                // Not a DB constraint: TestId is nullable, and SQL Server treats every NULL as
                // distinct under a unique index, so a naive constraint wouldn't even catch two
                // identically-named global keywords. Checked here instead, case-insensitively,
                // scoped to the same TestId (or same "global" scope), excluding self on update.
                var duplicate = await _context.PathologyReportKeyword.FirstOrDefaultAsync(k =>
                    k.HospitalId == request.HospitalId &&
                    k.TestId == request.TestId &&
                    k.Keyword.ToLower() == trimmedKeyword.ToLower() &&
                    (!request.KeywordId.HasValue || k.KeywordId != request.KeywordId.Value),
                    cancellationToken);
                if (duplicate != null)
                    return new UpsertPathologyReportKeywordResponseModel { Success = false, Message = $"A keyword \"{trimmedKeyword}\" already exists for this scope." };

                var now = DateTime.UtcNow;

                if (request.KeywordId.HasValue && request.KeywordId != Guid.Empty)
                {
                    var existing = await _context.PathologyReportKeyword
                        .FirstOrDefaultAsync(k => k.KeywordId == request.KeywordId && k.HospitalId == request.HospitalId, cancellationToken);
                    if (existing == null)
                        return new UpsertPathologyReportKeywordResponseModel { Success = false, Message = "Keyword not found." };

                    existing.TestId = request.TestId;
                    existing.Keyword = trimmedKeyword;
                    existing.ContentJson = request.ContentJson;
                    existing.IsActive = request.IsActive;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = request.LoggedInUserName;

                    await _context.SaveChangesAsync(cancellationToken);
                    return new UpsertPathologyReportKeywordResponseModel { Success = true, Message = "Keyword updated.", KeywordId = existing.KeywordId };
                }

                var keyword = new PathologyReportKeyword
                {
                    KeywordId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    TestId = request.TestId,
                    Keyword = trimmedKeyword,
                    ContentJson = request.ContentJson,
                    IsActive = request.IsActive,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.PathologyReportKeyword.Add(keyword);
                await _context.SaveChangesAsync(cancellationToken);

                return new UpsertPathologyReportKeywordResponseModel { Success = true, Message = "Keyword created.", KeywordId = keyword.KeywordId };
            }
            catch (Exception ex)
            {
                return new UpsertPathologyReportKeywordResponseModel { Success = false, Message = $"Error saving keyword: {ex.Message}" };
            }
        }
    }
}
