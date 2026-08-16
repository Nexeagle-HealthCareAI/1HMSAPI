using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdatePathologyTestCommandHandler : IRequestHandler<UpdatePathologyTestRequestModel, bool>
    {
        private readonly AppDbContext _context;

        public UpdatePathologyTestCommandHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(UpdatePathologyTestRequestModel request, CancellationToken cancellationToken)
        {
            var test = await _context.PathologyTestMaster
                .FirstOrDefaultAsync(x => x.TestId == request.TestId && x.HospitalId == request.HospitalId, cancellationToken);

            if (test == null)
            {
                throw new ApplicationException("Pathology test not found.");
            }

            if (test.TestCode != request.TestCode)
            {
                if (await _context.PathologyTestMaster.AnyAsync(x => x.HospitalId == request.HospitalId && x.TestCode == request.TestCode, cancellationToken))
                {
                    throw new ApplicationException($"Test code {request.TestCode} already exists for this hospital.");
                }
            }

            test.TestCode = request.TestCode;
            test.TestName = request.TestName;
            test.Category = request.Category;
            test.ChargeId = request.ChargeId;
            test.SampleType = request.SampleType;
            test.ContainerType = request.ContainerType;
            test.ParameterSchemaJson = request.ParameterSchemaJson;
            test.DefaultTemplateId = request.DefaultTemplateId;
            test.IsActive = request.IsActive;
            test.SortOrder = request.SortOrder;
            test.UpdatedAt = DateTime.UtcNow;
            test.UpdatedBy = request.LoggedInUserName ?? "System";

            _context.PathologyTestMaster.Update(test);
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
