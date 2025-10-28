namespace BookLoop.Services
{
    public class ReservationExpiryWorker: BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

        public ReservationExpiryWorker(IServiceProvider sp)
        {
            _sp = sp;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                using var scope = _sp.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ReservationExpiryService>();
                await svc.ExpireOverdueAsync(stoppingToken);
            }
        }

    }
}
