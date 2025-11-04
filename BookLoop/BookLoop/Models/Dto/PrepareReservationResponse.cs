namespace BookLoop.Models.Dto
{
    public class PrepareReservationResponse
    {
        public int ListingId { get; set; }
        public string BookTitle { get; set; } = "";
        public DateTime DefaultPickupDate { get; set; }
        public TimeSpan DefaultPickupTime { get; set; }
        public List<MemberOptionDto> Members { get; set; } = new();
    }
}
