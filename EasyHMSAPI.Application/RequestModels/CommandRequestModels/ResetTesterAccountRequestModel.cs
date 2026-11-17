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
        /// <summary>The signed-in user resetting their own test account from the Dev UI. When set, MobileNumber is ignored and the user's own number is used.</summary>
        public Guid? UserId { get; set; }
        /// <summary>True = only report what would be deleted; nothing is changed.</summary>
        public bool DryRun { get; set; }
    }

    /// <summary>Is the signed-in user a tester on this Development server? Lets the Dev UI offer "Reset my test account".</summary>
    [ExcludeFromCodeCoverage]
    public class TesterAccountStatusRequestModel : IRequest<TesterAccountStatusResponseModel>
    {
        public Guid UserId { get; set; }
    }
}
