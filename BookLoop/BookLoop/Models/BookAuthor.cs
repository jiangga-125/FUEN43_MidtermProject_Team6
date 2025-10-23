using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Models;

[PrimaryKey("BookID", "AuthorID")]
public partial class BookAuthor
{
    [Key]
    public int BookID { get; set; }

    [Key]
    public int AuthorID { get; set; }

    public int AuthorOrder { get; set; }

    [ForeignKey("AuthorID")]
    [InverseProperty("BookAuthors")]
    public virtual Author Author { get; set; } = null!;

    [ForeignKey("BookID")]
    [InverseProperty("BookAuthors")]
    public virtual Book Book { get; set; } = null!;
}
