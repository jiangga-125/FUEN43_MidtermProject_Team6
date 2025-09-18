using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
namespace BorrowSystem.ViewModels
    
{
    public class ReservationsViewmodel
    {
        public int ReservationID { get; set; }

        public int ListingID { get; set; }

        [Required(ErrorMessage = "請選擇預約者")]
        public int MemberID { get; set; }

       
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name ="預約時間")]
        public DateTime ReservationDay { get; set; }

        
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = false)]
        [Display(Name = "逾期時間")]
        public DateTime? ExpiresDay { get; set; }

        //只有這個可能是Null
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = false)]
        [Display(Name ="可取書日")]

        public DateTime? ReadyDay { get; set; }
        [Display(Name = "預約狀態")]
        public ReservationStatus? ReservationStatus { get; set; }
        [Display(Name = "生成日")]
        public DateTime CreateDay { get; set; }

        [Display(Name = "預約人")]
        public string MemberName {  get; set; } = string.Empty;
        [Display(Name = "書名")]
        public string BookTitle {  get; set; } = string.Empty;

        public IEnumerable<SelectListItem> Members { get; set; } = Enumerable.Empty<SelectListItem>();    
    }

    //定義列舉
    public enum ReservationStatus : byte
    {
        [Display(Name = "預約中")] Reserved = 0,
        [Display(Name = "取消")] Cancelled = 1,
        [Display(Name = "逾期取消")] AutoExpired = 2,
        [Display(Name = "等待取書")] Wait = 3,
        [Display(Name = "借閱完成")] Complete = 4
    }
}
