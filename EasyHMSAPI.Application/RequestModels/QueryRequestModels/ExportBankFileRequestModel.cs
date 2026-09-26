using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class ExportBankFileRequestModel : IRequest<ExportBankFileResponseModel>
    {
        public Guid HrPayrollRunId { get; set; }
        public string BankFormat { get; set; } = "GENERIC"; // "HDFC", "SBI", "GENERIC"
        /// <summary>Always the caller's own identity (set by the controller, never client-supplied); used to
        /// verify they may manage payroll at the run's hospital.</summary>
        public Guid LoggedInUserId { get; set; }
    }
}
