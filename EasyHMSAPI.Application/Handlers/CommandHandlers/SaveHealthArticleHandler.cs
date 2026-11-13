using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>Create / partially update a Health Wiki article from the CMS. The rules live in IHealthArticleService.</summary>
    public class SaveHealthArticleHandler : IRequestHandler<SaveHealthArticleRequestModel, SaveHealthArticleResponseModel>
    {
        private readonly IHealthArticleService _articles;

        public SaveHealthArticleHandler(IHealthArticleService articles)
        {
            _articles = articles;
        }

        public Task<SaveHealthArticleResponseModel> Handle(SaveHealthArticleRequestModel request, CancellationToken cancellationToken) =>
            _articles.SaveAsync(request, cancellationToken);
    }
}
