using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BookLoop.Models.Dto
{
    public class PenaltyDto
    {
        public int PenaltyID { get; set; }
        public int ListingID { get; set; }
        public string BookTitle { get; set; } = string.Empty;
       
        public decimal Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }

        public decimal? totalMoney { get; set; }//新增欄位 總金額
        //這三個僅用來顯示用
        [BindNever, ValidateNever]
        public string MemberName { get; set; }
        [BindNever, ValidateNever]
        public string ReasonCode { get; set; }
        [BindNever, ValidateNever]
        public string ChargeType { get; set; }
    }
}
