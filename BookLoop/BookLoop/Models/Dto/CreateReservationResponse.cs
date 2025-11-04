using BookLoop.ViewModels;
using System.Text.Json.Serialization;

namespace BookLoop.Models.Dto
{
    public class CreateReservationResponse
    {
        public bool Ok { get; set; }
        public string? Message { get; set; }
        public int ListingId { get; set; }
        public int NewStatus { get; set; }
        public DateTime ReadyAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ReservationStatus? ReservationStatus { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ReservationType? ReservationType { get; set; }
    }
}
