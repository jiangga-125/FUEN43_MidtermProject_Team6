namespace BookLoop.Models.Dto
{
    public class ReservationsDto
    {
        public int ReservationID { get; set; }
        public int ListingID { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public DateTime ReservationAt { get; set; }
        public byte Status { get; set; }
        public string StatusName { get; set; } 
    }
}

