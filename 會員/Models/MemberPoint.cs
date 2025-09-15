using System;
using System.Collections.Generic;

namespace 會員.Models;

public partial class MemberPoint
{
    public int MemberId { get; set; }

    public int Balance { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Member Member { get; set; } = null!;
}
