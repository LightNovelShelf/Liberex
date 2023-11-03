using System.ComponentModel.DataAnnotations;

namespace Liberex.Models.Context;

public class Series
{
    public Series()
    {
        this.Books = new HashSet<Book>();
    }

    [MaxLength(13)]
    public string Id { get; set; }

    public string FullPath { get; set; }

    public string LibraryId { get; set; }

    public bool IsDelete { get; set; }

    public DateTime AddTime { get; private set; } = DateTime.Now;

    public DateTime LastUpdateTime { get; set; } = DateTime.Now;

    public virtual Library Library { get; set; }

    public virtual ICollection<Book> Books { get; set; }
}