#nullable disable

using Microsoft.EntityFrameworkCore;

namespace Liberex.Models.Context;

public class LiberexContext : DbContext
{
    public DbSet<Library> Librarys { get; set; }

    public LiberexContext(DbContextOptions<LiberexContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

    }
}

public class Library
{
    public long Id { get; set; }

    public string LibraryId { get; set; }

    public string Path { get; set; }

    public DateTime AddTime { get; set; }
}