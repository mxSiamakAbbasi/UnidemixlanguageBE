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
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<AdPlacement> AdPlacements => Set<AdPlacement>();
    public DbSet<AdBooking> AdBookings => Set<AdBooking>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();

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
        modelBuilder.Entity<Language>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<SubscriptionPlan>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<AdPlacement>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<ContentItem>().HasIndex(x => new { x.Type, x.ExternalId }).IsUnique();
        modelBuilder.Entity<AdPlacement>().Property(x => x.DailyPrice).HasPrecision(12, 2);
        modelBuilder.Entity<AdBooking>().Property(x => x.AgreedPrice).HasPrecision(12, 2);
        modelBuilder.Entity<SubscriptionPlan>().Property(x => x.MonthlyPrice).HasPrecision(12, 2);
        modelBuilder.Entity<SubscriptionPlan>().Property(x => x.YearlyPrice).HasPrecision(12, 2);
    }
}
