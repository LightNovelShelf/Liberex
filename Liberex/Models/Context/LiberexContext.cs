#nullable disable

using Microsoft.EntityFrameworkCore;

namespace Liberex.Models.Context;

public class LiberexContext : DbContext
{
    public DbSet<Library> Librarys { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<Series> Series { get; set; }

    public LiberexContext(DbContextOptions<LiberexContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

    }
}

public class Library
{
    public long Id { get; set; }

    public string LibraryId { get; set; }

    public string FullPath { get; set; }

    public DateTime AddTime { get; set; } = DateTime.Now;

    public DateTime LastUpdateTime { get; set; } = DateTime.Now;
}

public class Series
{
    public long Id { get; set; }

    public string SeriesId { get; set; }

    public string FullPath { get; set; }

    public string LibraryId { get; set; }

    public DateTime AddTime { get; set; } = DateTime.Now;

    public DateTime LastUpdateTime { get; set; } = DateTime.Now;
}

public class Book
{
    public long Id { get; set; }

    public string Hash { get; set; }

    public string FullPath { get; set; }

    public long FileSize { get; set; }

    public DateTime AddTime { get; set; } = DateTime.Now;

    public DateTime ModifyTime { get; set; } = DateTime.Now;

    public string BookId { get; set; }

    public string SeriesId { get; set; }

    public string LibraryId { get; set; }


    public string Title { get; set; }

    public string Author { get; set; }

    public string Summary { get; set; }

    public string OEBPS { get; set; }
}