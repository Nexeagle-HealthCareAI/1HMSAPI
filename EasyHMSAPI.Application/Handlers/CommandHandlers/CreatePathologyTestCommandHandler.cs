using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class CreatePathologyTestCommandHandler : IRequestHandler<CreatePathologyTestRequestModel, Guid>
    {
        private readonly AppDbContext _context;

        public CreatePathologyTestCommandHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> Handle(CreatePathologyTestRequestModel request, CancellationToken cancellationToken)
        {
            if (await _context.PathologyTestMaster.AnyAsync(x => x.HospitalId == request.HospitalId && x.TestCode == request.TestCode, cancellationToken))
            {
                throw new ApplicationException($"Test code {request.TestCode} already exists for this hospital.");
            }

            var test = new PathologyTestMaster
            {
                TestId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                TestCode = request.TestCode,
                TestName = request.TestName,
                Category = request.Category,
                ChargeId = request.ChargeId,
                SampleType = request.SampleType,
                ContainerType = request.ContainerType,
                ParameterSchemaJson = request.ParameterSchemaJson,
                DefaultTemplateId = request.DefaultTemplateId,
                IsOutsourced = request.IsOutsourced,
                DefaultExternalLabId = request.IsOutsourced ? request.DefaultExternalLabId : null,
                CostPrice = request.IsOutsourced ? request.CostPrice : null,
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.LoggedInUserName ?? "System",
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = request.LoggedInUserName ?? "System"
            };

            _context.PathologyTestMaster.Add(test);
            await _context.SaveChangesAsync(cancellationToken);

            return test.TestId;
        }
    }
}
