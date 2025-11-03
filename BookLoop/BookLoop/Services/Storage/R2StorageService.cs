using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;

namespace BookLoop.Services.Storage
{
    public class R2StorageService : IFileStorage
    {
        private readonly IAmazonS3 _s3;
        private readonly string _bucket;
        private readonly string _publicBase;

        public R2StorageService(IConfiguration config)
        {
            var sec = config.GetSection("Storage:R2");
            var accountId = sec["AccountId"] ?? throw new InvalidOperationException("Missing Storage:R2:AccountId");
            var ak = sec["AccessKeyId"] ?? throw new InvalidOperationException("Missing Storage:R2:AccessKeyId");
            var sk = sec["SecretAccessKey"] ?? throw new InvalidOperationException("Missing Storage:R2:SecretAccessKey");
            _bucket = sec["Bucket"] ?? throw new InvalidOperationException("Missing Storage:R2:Bucket");
            _publicBase = (sec["PublicBase"] ?? "").TrimEnd('/');

            var endpoint = $"https://{accountId}.r2.cloudflarestorage.com";
            var cfg = new AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true };
            _s3 = new AmazonS3Client(new BasicAWSCredentials(ak, sk), cfg);
        }

        public string BuildKey(params string[] segments)
            => string.Join("/", segments.Where(s => !string.IsNullOrWhiteSpace(s))
                                        .Select(s => s.Replace("\\", "/").Trim('/')));

        public async Task<string> UploadAsync(string key, byte[] bytes, string contentType, CancellationToken ct = default)
        {
            using var ms = new MemoryStream(bytes);
            var req = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = ms,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
            };
            req.Headers.CacheControl = "public, max-age=31536000, immutable"; // 靜態快取建議

            await _s3.PutObjectAsync(req, ct);
            return $"{_publicBase}/{key}";
        }
    }
}
