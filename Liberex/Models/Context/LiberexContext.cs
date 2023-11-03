using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;

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
