using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models;


public partial class BorrowRecord
{
    [Key]
	public int RecordID { get; set; }

    public int ListingID { get; set; }

    public int MemberID { get; set; }

    public int? ReservationID { get; set; }
    public virtual Reservation? Reservation { get; set; }

    public DateTime BorrowDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    public DateTime DueDate { get; set; }

    public byte StatusCode { get; set; }
    // 以下這些都不是資料庫欄位 → 一律 NotMapped
    [NotMapped]
    public BorrowStatus Status
    {
        get => (BorrowStatus)StatusCode;
        set => StatusCode = (byte)value;
    }
    
    public enum BorrowStatus : byte
    {
        Overdue = 0,
        Borrowed = 1,
        Returned = 2
    }
    [NotMapped]
    public BorrowStatus EffectiveStatus =>
     Status == BorrowStatus.Borrowed && DateTime.Today > DueDate
         ? BorrowStatus.Overdue
         : Status;
    [NotMapped]
    public string StatusName => EffectiveStatus switch
    {
        BorrowStatus.Borrowed => "借出",
        BorrowStatus.Overdue => "逾期",
        BorrowStatus.Returned => "歸還",
        _ => "-"
    };
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Listing Listing { get; set; } = null!;

    public virtual Member Member { get; set; } = null!;

    public virtual ICollection<PenaltyTransaction> PenaltyTransactions { get; set; } = new List<PenaltyTransaction>();


}
