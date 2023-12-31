﻿using Microsoft.EntityFrameworkCore;

namespace Liberex.Models.Context;

public class LiberexContext : DbContext
{
    public DbSet<Library> Librarys { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<Series> Series { get; set; }
    public DbSet<BookCover> BookCovers { get; set; }

    public LiberexContext(DbContextOptions<LiberexContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Series>()
            .HasOne(e => e.Library)
            .WithMany(x => x.Series)
            .HasForeignKey(x => x.LibraryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Book>()
            .HasOne(e => e.Series)
            .WithMany(x => x.Books)
            .HasForeignKey(x => x.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BookCover>(ob =>
        {
            ob.HasKey(x => x.BookId);
            ob.HasOne(x => x.Book)
                .WithOne(x => x.Cover)
                .HasForeignKey<BookCover>(x => x.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
