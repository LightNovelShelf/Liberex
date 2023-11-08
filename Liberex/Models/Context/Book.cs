using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace Liberex.Models.Context;

[Index(nameof(FullPath), IsUnique = true)]
public class Book
{
    [MaxLength(13)]
    public string Id { get; set; }
    public string SeriesId { get; set; }
    public bool IsDelete { get; set; }
    public string FullPath { get; set; }
    [MaxLength(32)]
    public string Hash { get; set; }
    public long FileSize { get; set; }
    public DateTime AddTime { get; private set; } = DateTime.Now;
    public DateTime ModifyTime { get; set; } = DateTime.Now;

    public string Title { get; set; }
    public string Author { get; set; }
    public string Summary { get; set; }
    public string Opf { get; set; }

    public virtual Series Series { get; set; }
    public virtual BookCover Cover { get; set; }
}