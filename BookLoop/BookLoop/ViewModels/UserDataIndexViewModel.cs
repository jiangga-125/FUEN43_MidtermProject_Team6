namespace BookLoop.ViewModels
{
    public class UserDataIndexViewModel
    {
        public IEnumerable<BorrowRecordsViewModel> BorrowRecords { get; set; } = Enumerable.Empty<BorrowRecordsViewModel>();

        public IEnumerable<ReservationsViewModel> Reservations { get; set; } = Enumerable.Empty<ReservationsViewModel>();

        public IEnumerable<PenaltyTransactionsViewModel> PenaltyTransactions { get; set; } = Enumerable.Empty<PenaltyTransactionsViewModel>();

    }
}
