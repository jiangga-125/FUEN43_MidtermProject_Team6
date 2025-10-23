using BookLoop.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace BookLoop.ViewModels
{
    public class ListingseditViewModel
    {

        // 識別
        public int ListingId { get; set; }

        // 基本欄位
        [Required(ErrorMessage = "書名必填")]
        [Display(Name = "書名")]
        public string Title { get; set; } = "";

        // 若你的 ISBN 不一定以 9 開頭，建議把正則放寬為 13 碼數字
        //[RegularExpression(@"^9\d{12}$", ErrorMessage = "請輸入正確的 ISBN（以 9 開頭的 13 碼數字）")]
        [RegularExpression(@"^\d{13}$", ErrorMessage = "ISBN 必須是 13 位數字")]
        [StringLength(13, MinimumLength = 13, ErrorMessage = "ISBN 必須是 13 位數")]
        [Required(ErrorMessage = "請輸入 ISBN")]
        public string ISBN { get; set; } = "";

        [Display(Name = "書況")]
        public string? Condition { get; set; }

        [Display(Name = "現況")]
        public byte Status { get; set; }

        // 關聯（下拉）
        [Required] public int CategoryId { get; set; }
        public int PublisherId { get; set; } // 若出版社可不填就用 nullable；若必填改成 int

        // 作者（只編主作者）
        [Required(ErrorMessage = "作者必填")]
        public string AuthorName { get; set; } = "";

        // 圖片編輯
        // cloud | local | none（不變更）
        public string ImageSource { get; set; } = "none";
        public string? CloudImageUrl { get; set; }
        public IFormFile? LocalImage { get; set; }

        // 目前封面（用於預覽顯示，不參與驗證/綁定）
        [ValidateNever]
        public string? ImageUrl { get; set; }

    }
    }

