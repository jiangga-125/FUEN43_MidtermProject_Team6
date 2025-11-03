namespace BookLoop.Services.Mail
{
    public interface IMailJobRunner
    {
        Task RunAsync(long jobId, CancellationToken ct = default);
    }
}
