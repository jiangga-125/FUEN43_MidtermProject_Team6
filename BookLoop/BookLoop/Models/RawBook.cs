using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Models;

public partial class RawBook
{
    [Key]
    public int RawID { get; set; }

    [StringLength(20)]
    public string ISBN { get; set; } = null!;

    [StringLength(500)]
    public string Title { get; set; } = null!;

    [StringLength(500)]
    public string? Authors { get; set; }

    [StringLength(200)]
    public string? Publisher { get; set; }

    [StringLength(50)]
    public string? PublishedDate { get; set; }

    [StringLength(200)]
    public string? Category { get; set; }

    public DateTime CreatedAt { get; set; }
}
