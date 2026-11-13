using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>Sets an employee's income-tax regime (NEW / OLD) and, for OLD, the total annual declared deductions.</summary>
    [ExcludeFromCodeCoverage]
    public class UpdateEmployeeTaxProfileRequestModel : IRequest<UpdateEmployeeTaxProfileResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public Guid HrEmployeeId { get; set; }
        public string? TaxRegime { get; set; }
        public decimal AnnualDeclaredDeductions { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
