using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models;

[Index("ISBN", Name = "UQ__Books__447D36EAF2963BEA", IsUnique = true)]
[Index("Slug", Name = "UQ__Books__BC7B5FB620ADCE67", IsUnique = true)]

public partial class Book
{
	[Key]
	public int BookID { get; set; }

	[StringLength(13, MinimumLength = 13, ErrorMessage = "ISBN 必須是 13 碼")]
	public string ISBN { get; set; } = null!;

	[StringLength(500)]
	public string Title { get; set; } = null!;

    //public string? Subtitle { get; set; }

    public int? AuthorID { get; set; }

    public int PublisherID { get; set; }

    public int CategoryID { get; set; }

	public string? Description { get; set; }

	[StringLength(200)]
	public string? Slug { get; set; }

	public bool IsDeleted { get; set; }

	public decimal ListPrice { get; set; }

	public decimal? SalePrice { get; set; }


	//public string? LanguageCode { get; set; }

	//public byte Status { get; set; }

	//public bool IsListed { get; set; }

	//public DateTime? ListedAt { get; set; }


	//public DateTime? DeletedAt { get; set; }

	public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

	[ForeignKey("AuthorID")]
	[InverseProperty("Books")]
	public virtual Author? Author { get; set; }

	[InverseProperty("Book")]
	public virtual ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();

	[InverseProperty("Book")]
	public virtual ICollection<BookImage> BookImages { get; set; } = new List<BookImage>();

	//[InverseProperty("Book")]
	//public virtual BookImage? BookImage { get; set; } // 一對一

	[ForeignKey("CategoryID")]
	[InverseProperty("Books")]
	public virtual Category? Category { get; set; }

	[ForeignKey("PublisherID")]
	[InverseProperty("Books")]
	public virtual Publisher? Publisher { get; set; }
	public DateTime? PublishDate { get; set; }

	public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
