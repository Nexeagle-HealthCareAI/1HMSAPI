using MediatR;
using System;
using System.Collections.Generic;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class RunMonthlyPayrollRequestModel : IRequest<RunMonthlyPayrollResponseModel>
    {
        public Guid HospitalId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public Guid ProcessedByUserId { get; set; }
    }
}
