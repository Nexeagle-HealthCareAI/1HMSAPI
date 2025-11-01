//using System.Threading;
//using System.Threading.Tasks;
//using MediatR;
//using EasyHMSAPI.Application.Services.Interfaces;
//using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
//using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

//namespace EasyHMSAPI.Application.Handlers.QueryHandlers
//{
//    public class GetImageUrlHandler : IRequestHandler<GetImageUrlRequestModel, GetImageUrlResponseModel>
//    {
//        private readonly IBlobStorageService _blobStorageService;
//        public GetImageUrlHandler(IBlobStorageService blobStorageService)
//        {
//            _blobStorageService = blobStorageService;
//        }

//        public Task<GetImageUrlResponseModel> Handle(GetImageUrlRequestModel request, CancellationToken cancellationToken)
//        {
//            var url = _blobStorageService.GetImageUrl(request.FileName, request.ContainerName);
//            return Task.FromResult(new GetImageUrlResponseModel
//            {
//                Url = url,
//                Success = true,
//                Message = "Image URL retrieved successfully."
//            });
//        }
//    }
//}