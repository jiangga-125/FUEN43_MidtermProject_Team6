using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models.Dto
{
    public class ListingFrontDto
    {
        public int ListingId { get; set; }

        public int CategoryId { get; set; }

        public int PublisherId { get; set; }
        [Required(ErrorMessage = "書名必填")]
        [Display(Name = "書名")]
        public string Title { get; set; }
  
        public string ISBN { get; set; }

        [Display(Name = "書況")]
        public string? Condition { get; set; }

        public byte Status { get; set; }

        public string PublisherName { get; set; } = null!;


        public string CategoryName { get; set; } = null!;

        [Required(ErrorMessage = "作者必填")]
        public string AuthorName { get; set; } = null!;

        public string? ImageUrl { get; set; }
        public virtual ICollection<ListingAuthor> ListingAuthors { get; set; } = new List<ListingAuthor>();

        public virtual ICollection<ListingImage> ListingImages { get; set; } = new List<ListingImage>();

        public string ImageSource { get; set; } = "none"; 
        public string? CloudImageUrl { get; set; }
        public IFormFile? LocalImage { get; set; }
    }
}
