using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Liberex.Models.Context;

[Index(nameof(FullPath), IsUnique = true)]
public class Library
{
    public Library()
    {
        this.Series = new HashSet<Series>();
    }

    [MaxLength(13)]
    public string Id { get; set; }
    public string FullPath { get; set; }
    public string Name { get; set; }
    public DateTime AddTime { get; private set; } = DateTime.Now;
    // public DateTime LastUpdateTime { get; set; } = DateTime.Now;

    public virtual ICollection<Series> Series { get; set; }
}