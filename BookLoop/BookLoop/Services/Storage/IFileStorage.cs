namespace BookLoop.Services.Storage
{
    public interface IFileStorage
    {
        Task<string> UploadAsync(string key, byte[] bytes, string contentType, CancellationToken ct = default);
        string BuildKey(params string[] segments);
    }
}
