namespace BookLoop.Models.Dto
{
    public class BorrowDto
    {
        public int ListingID { get; set; }
        public int RecordID { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public int MemberID { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public DateTime BorrowDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public DateTime DueDate { get; set; }
        public int StatusCode { get; set; }
        public byte ?ReturnCondition { get; set; }
        public string ConditionName { get; set; } = "-";
    }
}
