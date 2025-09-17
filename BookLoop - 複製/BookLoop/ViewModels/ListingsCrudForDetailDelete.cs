using System.ComponentModel.DataAnnotations;

namespace BorrowSystem.ViewModels
{
    public class ListingsCrudForDetailDelete
    {

        public int ListingID { get; set; }
        [Display(Name = "書名")]
       
        public string Title { get; set; } = null!;

    
        public string ISBN { get; set; } = null!;

        [Display(Name = "書況")]
        public string? Condition { get; set; }
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = false)]
        [Display(Name = "上架時間")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "狀態")]
        public bool IsAvailable { get; set; } = true;
        [Display(Name = "種類")]
        
        public string CategoryName { get; set; } = null!;
        [Display(Name = "出版社")]
       
        public string PublisherName { get; set; } = null!;

        [Display(Name = "作者")]
        
        public List<string> AuthorNames { get; set; } = new();
    }
}

