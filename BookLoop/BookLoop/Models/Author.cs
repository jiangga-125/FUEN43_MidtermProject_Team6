using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Models;

[Index("AuthorName", Name = "UQ__Authors__4A1A120B71172EA7", IsUnique = true)]
[Index("Slug", Name = "UQ__Authors__BC7B5FB663E31F0A", IsUnique = true)]
public partial class Author
{
    [Key]
    public int AuthorID { get; set; }

    [StringLength(100)]
    public string AuthorName { get; set; } = null!;

    [StringLength(200)]
    public string Slug { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Author")]
    public virtual ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();

    [InverseProperty("Author")]
    public virtual ICollection<Book> Books { get; set; } = new List<Book>();
}
