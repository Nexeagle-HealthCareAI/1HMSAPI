using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>Wipe a tester number's account so it can register again (Development servers only, see TesterAccounts).</summary>
    [ExcludeFromCodeCoverage]
    public class ResetTesterAccountRequestModel : IRequest<ResetTesterAccountResponseModel>
    {
        public string? MobileNumber { get; set; }
        /// <summary>True = only report what would be deleted; nothing is changed.</summary>
        public bool DryRun { get; set; }
    }
}
