using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
namespace BorrowSystem.ViewModels
   
{
    public class ListingFormViewModel : ListingCommonViewModel
    {
        [Required(ErrorMessage = "必填：種類")]
        public int? CategoryID { get; set; }

        [Required(ErrorMessage = "必填：出版社")]
        public int? PublisherID { get; set; }

        // 覆寫父類的屬性，加上驗證
        [Required, StringLength(100, MinimumLength = 2)]
        
        public new string Title { get; set; } = null!;

        [RegularExpression(@"^9\d{12}$", ErrorMessage = "ISBN 必須是以 9 開頭的 13 碼數字")]
        public new string ISBN { get; set; } = null!;

        [StringLength(200)]
        
        public new string? Condition { get; set; }

        // 下拉清單
        public IEnumerable<SelectListItem>? Categories { get; set; }
        public IEnumerable<SelectListItem>? Publishers { get; set; }
    }
}
