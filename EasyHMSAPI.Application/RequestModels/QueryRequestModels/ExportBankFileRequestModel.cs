using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class ExportBankFileRequestModel : IRequest<ExportBankFileResponseModel>
    {
        public Guid HrPayrollRunId { get; set; }
        public string BankFormat { get; set; } = "GENERIC"; // "HDFC", "SBI", "GENERIC"
    }
}
