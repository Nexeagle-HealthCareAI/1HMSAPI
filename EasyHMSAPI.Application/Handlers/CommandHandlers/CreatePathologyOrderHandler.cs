using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Services;
using EasyHMSAPI.Application.Services;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class CreatePathologyOrderHandler : IRequestHandler<CreatePathologyOrderRequestModel, CreatePathologyOrderResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public CreatePathologyOrderHandler(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<CreatePathologyOrderResponseModel> Handle(CreatePathologyOrderRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var billingPolicy = await _context.BillingPolicy.FirstOrDefaultAsync(c => c.HospitalId == request.HospitalId, cancellationToken);
                bool autoBill = billingPolicy?.LabPathTrigger == "ON_ORDER";

                // 1. Assign Number Series
                string orderNo = string.Empty;
                var now = DateTime.UtcNow;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                            _context, request.HospitalId, BillingConstants.NumberSeriesCode.LabAccession, request.LoggedInUserName, cancellationToken);
                        numberSeries.CurrentValue++;
                        orderNo = NumberSeriesFormatter.Format(
                            numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);
                        numberSeries.UpdatedAt = now;
                        numberSeries.UpdatedBy = request.LoggedInUserName;
                        break;
                    }
                    catch (DbUpdateException)
                    {
                        _context.ChangeTracker.Clear();
                        if (attempt == 4) throw;
                    }
                }

                var order = new PathologyOrder
                {
                    HospitalId = request.HospitalId,
                    PatientId = request.PatientId,
                    EncounterId = request.EncounterId,
                    AdmissionId = request.AdmissionId,
                    OrderedByDoctorId = request.OrderedByDoctorId,
                    Notes = request.Notes,
                    OrderNo = orderNo,
                    OrderDate = DateTime.UtcNow,
                    Status = "PLACED",
                    CreatedBy = request.LoggedInUserName
                };

                _context.PathologyOrder.Add(order);

                var orderLines = new List<PathologyOrderLine>();
                foreach (var testId in request.TestIds)
                {
                    var orderLine = new PathologyOrderLine
                    {
                        HospitalId = request.HospitalId,
                        OrderId = order.OrderId,
                        TestId = testId,
                        Status = "PENDING",
                        CreatedBy = request.LoggedInUserName
                    };
                    orderLines.Add(orderLine);
                }
                
                _context.PathologyOrderLine.AddRange(orderLines);
                await _context.SaveChangesAsync(cancellationToken);

                // 2. Billing Integration
                if (autoBill && request.EncounterId.HasValue)
                {
                    var tests = await _context.PathologyTestMaster
                        .Where(t => request.TestIds.Contains(t.TestId) && t.HospitalId == request.HospitalId)
                        .Include(t => t.ChargeId)
                        .ToListAsync(cancellationToken);

                    var charges = new List<ChargeDetail>();
                    foreach (var test in tests)
                    {
                        if (test.ChargeId.HasValue)
                        {
                            charges.Add(new ChargeDetail
                            {
                                ChargeId = test.ChargeId.Value,
                                Qty = 1,
                                Rate = 0, // Gets mapped from ChargeMaster
                                CategoryCode = "LAB_PATH",
                                SourceModule = BillingConstants.SourceModule.LabPath,
                                SourceRefId = order.OrderId.ToString(),
                                AttributedDoctorId = request.OrderedByDoctorId
                            });
                        }
                    }

                    if (charges.Any())
                    {
                        var chargeRequest = new AddChargeEventRequestModel
                        {
                            HospitalId = request.HospitalId,
                            EncounterId = request.EncounterId.Value,
                            Charges = charges,
                            LoggedInUserId = request.LoggedInUserId,
                            LoggedInUserName = request.LoggedInUserName
                        };

                        await _mediator.Send(chargeRequest, cancellationToken);
                    }
                }

                return new CreatePathologyOrderResponseModel
                {
                    Success = true,
                    OrderId = order.OrderId,
                    OrderNo = orderNo
                };
            }
            catch (Exception ex)
            {
                return new CreatePathologyOrderResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
