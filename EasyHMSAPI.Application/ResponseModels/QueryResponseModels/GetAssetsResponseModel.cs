namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetAssetsResponseModel
    {
        public Guid PrescriptionAssestId { get; set; }
        public Guid DoctorId { get; set; }
        public List<AssetsDataModel>? Assets { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
    public class AssetsDataModel
    {
        public Guid PrescriptionAssestId { get; set; }
        public string? AssetType { get; set; }
        public string? BlobAssetId { get; set; }
        public string? BlobUrl { get; set; }
    }
}
