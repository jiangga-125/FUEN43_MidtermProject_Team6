using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using BookLoop.Models;

namespace BookLoop.ViewModels
{
    public class BorrowRecordsViewModel
    {
        [Display(Name ="讀者ID")]
        public int RecordID { get; set; }
        [Display(Name = "借閱人")]
        public string MemberName { get; set; } = "";
        public int ListingID { get; set; }
       
        
        public int MemberID { get; set; }

        [Display(Name = "預約編號")]
        public int? ReservationID { get; set; }

        [Display(Name = "借閱日")]
        [DisplayFormat(DataFormatString = "{0:yyyy-M-d}", ApplyFormatInEditMode = true)]
        public DateTime BorrowDate { get; set; }

      
        [Display(Name = "歸還日")]
        [DisplayFormat(DataFormatString = "{0:yyyy-M-d}", ApplyFormatInEditMode = false)]
        public DateTime? ReturnDate { get; set; }

        [DisplayFormat(DataFormatString = "{0:yyyy-M-d}", ApplyFormatInEditMode = false)]
        [Display(Name = "必須歸還日")]
        public DateTime DueDate { get; set; }

        [Display(Name = "狀態代碼")]
        public byte StatusCode { get; set; }
        [BindNever]
        [Display(Name = "借閱書籍名稱")]
        public string BookTitle { get; set; } = string.Empty;

        [Display(Name = "預約狀態")]
        public ReservationStatus? ReservationStatus { get; set; }
    
        [Display(Name = "紀錄生成日")]
        public DateTime CreatedAt { get; set; }

        

        
        

        public const int LoanDays = 3;

        public enum BorrowCondition : byte
        {
            Overdue = 0,
            Borrowed = 1,
            Returned = 2
        }

        public BorrowCondition StatusEnum
        {
            get => (BorrowCondition)StatusCode;
            set => StatusCode = (byte)value;
        }

        /// <summary>
        /// 計算後的狀態 (處理逾期判斷)
        /// </summary>
        public BorrowCondition ViewEffectiveStatus =>
            StatusEnum == BorrowCondition.Borrowed && DateTime.Today > DueDate
                ? BorrowCondition.Overdue
                : StatusEnum;

        /// <summary>
        /// 狀態中文名稱 (給 Index 顯示)
        /// </summary>
        [Display(Name = "借閱狀態")]
        public string ConditionName => ViewEffectiveStatus switch
        {
            BorrowCondition.Borrowed => "借出",
            BorrowCondition.Overdue => "逾期",
            BorrowCondition.Returned => "歸還",
            _ => "-"
        };

     }   
   
}
