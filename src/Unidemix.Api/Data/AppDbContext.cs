using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Models;

namespace Unidemix.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<LessonProgress> LessonProgress => Set<LessonProgress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Course>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<Lesson>().HasIndex(x => new { x.CourseId, x.Order }).IsUnique();
        modelBuilder.Entity<Exercise>().HasIndex(x => new { x.LessonId, x.Order }).IsUnique();
        modelBuilder.Entity<LessonProgress>().HasIndex(x => new { x.UserId, x.LessonId }).IsUnique();
        modelBuilder.Entity<LessonProgress>().HasOne(x => x.User).WithMany(x => x.Progress)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LessonProgress>().HasOne(x => x.Lesson).WithMany(x => x.Progress)
            .HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
    }
}
