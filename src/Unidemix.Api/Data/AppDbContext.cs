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
    public DbSet<ProductFeatureFlag> ProductFeatureFlags => Set<ProductFeatureFlag>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<SocialProfile> SocialProfiles => Set<SocialProfile>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<UserFollow> UserFollows => Set<UserFollow>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<MessageRequest> MessageRequests => Set<MessageRequest>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ConversationReport> ConversationReports => Set<ConversationReport>();
    public DbSet<SocialPost> SocialPosts => Set<SocialPost>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<PostComment> PostComments => Set<PostComment>();
    public DbSet<ContentMention> ContentMentions => Set<ContentMention>();

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
        modelBuilder.Entity<ProductFeatureFlag>().HasKey(x => x.Key);
        modelBuilder.Entity<ContentItem>().HasIndex(x => new { x.Type, x.ExternalId }).IsUnique();
        modelBuilder.Entity<AdPlacement>().Property(x => x.DailyPrice).HasPrecision(12, 2);
        modelBuilder.Entity<AdBooking>().Property(x => x.AgreedPrice).HasPrecision(12, 2);
        modelBuilder.Entity<SubscriptionPlan>().Property(x => x.MonthlyPrice).HasPrecision(12, 2);
        modelBuilder.Entity<SubscriptionPlan>().Property(x => x.YearlyPrice).HasPrecision(12, 2);
        modelBuilder.Entity<SocialProfile>().HasKey(x => x.UserId);
        modelBuilder.Entity<SocialProfile>().HasOne(x => x.User).WithOne(x => x.SocialProfile).HasForeignKey<SocialProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SocialProfile>().HasOne(x => x.CityReference).WithMany().HasForeignKey(x => x.CityId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<City>().HasIndex(x => new { x.CountryCode, x.CanonicalName }).IsUnique();
        modelBuilder.Entity<UserFollow>().HasIndex(x => new { x.FollowerId, x.FollowedUserId }).IsUnique();
        modelBuilder.Entity<UserFollow>().HasOne(x => x.Follower).WithMany().HasForeignKey(x => x.FollowerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserFollow>().HasOne(x => x.FollowedUser).WithMany().HasForeignKey(x => x.FollowedUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserBlock>().HasIndex(x => new { x.BlockerId, x.BlockedUserId }).IsUnique();
        modelBuilder.Entity<UserBlock>().HasOne(x => x.Blocker).WithMany().HasForeignKey(x => x.BlockerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserBlock>().HasOne(x => x.BlockedUser).WithMany().HasForeignKey(x => x.BlockedUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Report>().HasIndex(x => new { x.ReporterId, x.TargetType, x.TargetId, x.Reason, x.Status }).IsUnique();
        modelBuilder.Entity<Report>().HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Report>().HasOne(x => x.ReportedUser).WithMany().HasForeignKey(x => x.ReportedUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MessageRequest>().HasIndex(x => new { x.SenderId, x.RecipientId, x.Status });
        modelBuilder.Entity<MessageRequest>().HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MessageRequest>().HasOne(x => x.Recipient).WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Conversation>().HasIndex(x => new { x.UserOneId, x.UserTwoId }).IsUnique();
        modelBuilder.Entity<Conversation>().HasOne(x => x.UserOne).WithMany().HasForeignKey(x => x.UserOneId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Conversation>().HasOne(x => x.UserTwo).WithMany().HasForeignKey(x => x.UserTwoId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChatMessage>().HasIndex(x => new { x.ConversationId, x.CreatedAt });
        modelBuilder.Entity<ChatMessage>().HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ChatMessage>().HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Notification>().HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
        modelBuilder.Entity<Notification>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Notification>().HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ConversationReport>().HasIndex(x => new { x.ReporterId, x.ConversationId, x.Reason, x.Status }).IsUnique();
        modelBuilder.Entity<ConversationReport>().HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ConversationReport>().HasOne(x => x.Conversation).WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SocialPost>().HasIndex(x => new { x.Status, x.CreatedAt });
        modelBuilder.Entity<SocialPost>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PostLike>().HasIndex(x => new { x.UserId, x.PostId }).IsUnique();
        modelBuilder.Entity<PostLike>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PostLike>().HasOne(x => x.Post).WithMany(x => x.Likes).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PostComment>().HasIndex(x => new { x.PostId, x.Status, x.CreatedAt });
        modelBuilder.Entity<PostComment>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PostComment>().HasOne(x => x.Post).WithMany(x => x.Comments).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ContentMention>().HasIndex(x => new { x.PostId, x.MentionedUserId }).IsUnique().HasFilter("\"PostId\" IS NOT NULL");
        modelBuilder.Entity<ContentMention>().HasIndex(x => new { x.CommentId, x.MentionedUserId }).IsUnique().HasFilter("\"CommentId\" IS NOT NULL");
        modelBuilder.Entity<ContentMention>().HasOne(x => x.MentionedUser).WithMany().HasForeignKey(x => x.MentionedUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ContentMention>().HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ContentMention>().HasOne(x => x.Comment).WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.Cascade);
    }
}
