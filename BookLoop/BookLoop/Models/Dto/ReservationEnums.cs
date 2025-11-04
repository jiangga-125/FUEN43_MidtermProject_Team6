namespace BookLoop.Models.Dto
{
    public class ReservationEnums
    {
        public enum ReservationStatus : byte { Reserved = 0, Cancelled = 1, AutoExpired = 2, Wait = 3, Complete = 4 }
        public enum ReservationType : byte { Normal = 0, Waitlist = 1 }
    }
}
