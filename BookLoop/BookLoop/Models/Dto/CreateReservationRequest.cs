using System.ComponentModel.DataAnnotations;
namespace BookLoop.Models.Dto
{
    public class CreateReservationRequest
    {
        [Required] public int ListingId { get; set; }
        [Required(ErrorMessage = "請選擇預約者")] public int MemberId { get; set; }
        [Required] public DateTime RequestedPickupDate { get; set; }
        [Required] public TimeSpan RequestedPickupTime { get; set; }
    }
}
