using System.ComponentModel.DataAnnotations;

namespace BorrowSystem.ViewModels
{
    public class ListingCommonViewModel
    {
        public int ListingID { get; set; }
        [Display(Name = "書名")]
        public string Title { get; set; } = null!;
        public string ISBN { get; set; } = null!;
        [Display(Name = "書況")]
        public string? Condition { get; set; }
        [Display(Name = "有庫存")]
        public bool IsAvailable { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}
