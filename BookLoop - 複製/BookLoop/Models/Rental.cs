﻿using System;
using System.Collections.Generic;

namespace BookLoop.Ordersys.Models;

public partial class Rental
{
    public int RentalID { get; set; }

    public int OrderID { get; set; }

    public string ItemName { get; set; } = null!;

    public DateTime RentalStart { get; set; }

    public DateTime RentalEnd { get; set; }

    public DateTime? ReturnedDate { get; set; }

    public byte Status { get; set; }

    public virtual Order Order { get; set; } = null!;
}
