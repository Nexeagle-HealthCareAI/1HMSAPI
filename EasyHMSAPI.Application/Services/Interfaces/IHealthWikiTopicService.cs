using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>
    /// A contributor's own topic requests: list, suggest, and answer "needs detail". Shared by the Doctor Dekho pages (the contributor's
    /// session) and EasyHMS (a hospital doctor signed in to EasyHMS), so both follow the same rules.
    /// </summary>
    public interface IHealthWikiTopicService
    {
        Task<GetContributorTopicsResponseModel> ListAsync(Guid contributorId, CancellationToken cancellationToken);

        /// <summary>Needs an accepted profile, topic, outline and why it matters, at most 5 open requests, and only what the person may write.
        /// A doctor may ask for a writer (RequestWriter); for anyone else the flag is ignored.</summary>
        Task<ContributorTopicResponseModel> CreateAsync(CreateContributorTopicRequestModel request, CancellationToken cancellationToken);

        Task<ContributorTopicResponseModel> AddDetailAsync(AddContributorTopicDetailRequestModel request, CancellationToken cancellationToken);
    }
}
