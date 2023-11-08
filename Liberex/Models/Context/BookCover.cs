using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Liberex.Models.Context;

public class BookCover
{
    [MaxLength(13)]
    public string BookId { get; set; }
    public byte[] Data { get; set; }
    public int Height { get; set; }
    public int Width { get; set; }
    public byte[] Thumbnail { get; set; }
    [MaxLength(32)]
    public string Placeholder { get; set; }

    public virtual Book Book { get; set; }
}