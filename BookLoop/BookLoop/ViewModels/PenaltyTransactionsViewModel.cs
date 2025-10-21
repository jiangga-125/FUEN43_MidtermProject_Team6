using BookLoop.Models;
using System.ComponentModel.DataAnnotations;

namespace BookLoop.ViewModels
{
    public class PenaltyTransactionsViewModel
    {
        public int PenaltyID { get; set; }

        public int RecordID { get; set; }

        public int MemberID { get; set; }

        public int RuleID { get; set; }

        public DateTime CreatedAt { get; set; }
        [Range(1, 300)]
        public int Quantity { get; set; }=1;//預設值1
        public decimal? totalMoney { get; set; }//新增欄位 總金額
        public DateTime? PaidAt { get; set; }

        public string ReasonCode { get; set; }
        [Required(ErrorMessage = "ChargeType 為必填")]
        public string ChargeType { get; set; }
        [Required(ErrorMessage = "UnitAmount 為必填")]
        [Range(1, int.MaxValue, ErrorMessage = "UnitAmount 必須大於 0")]
        public int UnitAmount { get; set; }

        public string MemberName { get; set; }

    }
}
