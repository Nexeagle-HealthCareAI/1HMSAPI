using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.Services.Interfaces
{
    /// <summary>
    /// The Health Wiki article save rules (type, author and reviewer, cover image, pending edit of a live article, who can publish).
    /// Shared by the CMS save handler and the contributor's own save handler.
    /// </summary>
    public interface IHealthArticleService
    {
        Task<SaveHealthArticleResponseModel> SaveAsync(SaveHealthArticleRequestModel request, CancellationToken cancellationToken);
    }
}
