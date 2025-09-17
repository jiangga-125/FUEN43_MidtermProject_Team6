using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using NuGet.Configuration;
using System.ComponentModel.DataAnnotations;
namespace BorrowSystem.ViewModels
{
    public class ListingsCrudForCreateEdit
    {
        public int ListingID { get; set; }
        [Display(Name = "書籍種類")]
        [Required(ErrorMessage = "請選擇{0}")]
        public int? CategoryID { get; set; }
        [Required(ErrorMessage = "請選擇{0}")]
        [Display(Name = "出版社")]
        public int? PublisherID { get; set; }

        [Display(Name = "書名")]
        [Required(ErrorMessage="書名必填")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "ISBN必填")]
        [MinLength(10, ErrorMessage = "ISBN 至少需要 10 位數字")]
        [MaxLength(13, ErrorMessage = "ISBN 最多 13 位數字")]
        [RegularExpression(@"^9\d{9}(\d{3})?$", ErrorMessage = "ISBN 必須是以 9 開頭的13 位數字")]
        public string ISBN { get; set; } = null!;

        [Display(Name = "書況")]
        public string? Condition { get; set; }

        [Display(Name = "上架時間")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "有庫存")]
        public bool IsAvailable { get; set; } = true;
        [Display(Name = "種類")]
        [ValidateNever]
        //[Required(ErrorMessage = "種類必填")]
        public string CategoryName { get; set; } = null!;
        [Display(Name = "出版社")]
        [ValidateNever]
        //[Required(ErrorMessage = "出版社必填")]
        public string PublisherName { get; set; } = null!;

        [Display(Name = "作者")]
        [MinLength(1, ErrorMessage = "至少需要 1 位作者")]
        public List<AuthorItem> Authors { get; set; } = new();

        // 單選哪一位作者是主作者
        [Display(Name = "主作者")]
        [Required(ErrorMessage = "請選擇主作者")]
        public int? PrimaryIndex { get; set; }


        public IEnumerable<SelectListItem> CategoryOptions { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> PublisherOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    }
    public class AuthorItem
    {
        public int? ListingAuthorID { get; set; }
        [Required(ErrorMessage = "作者必填")]
        public string AuthorName { get; set; } = null!;
        [ValidateNever]
        public bool IsPrimary { get; set; }
    }


}
