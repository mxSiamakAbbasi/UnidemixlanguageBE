using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unidemix.Api.Contracts;
using Unidemix.Api.Data;
using Unidemix.Api.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Unidemix.Api.Tests;

public sealed class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_is_available()
    {
        var response = await factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_requires_token()
    {
        var response = await factory.CreateClient().GetAsync("/api/courses");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Demo_user_can_login_and_read_seeded_course()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("demo@unidemix.local", "Demo123!"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var response = await client.GetAsync("/api/courses");
        response.EnsureSuccessStatusCode();
        Assert.Contains("german-for-real-life", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Registration_rejects_duplicate_email()
    {
        var client = factory.CreateClient();
        var request = new RegisterRequest("new@unidemix.local", "Password123!", "New User");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
    }

    [Fact]
    public async Task Learning_orientation_is_structured_editable_and_persisted()
    {
        var client = factory.CreateClient();
        var email = $"orientation-{Guid.NewGuid():N}@unidemix.local";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Password123!", "Learner"));
        registration.EnsureSuccessStatusCode();
        var auth = await registration.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth); Assert.False(auth.User.LearningOnboardingCompleted);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.PutAsJsonAsync("/api/profile", new UpdateProfileRequest(
            "Learner", "fa", "de", "A1", "migration", 15,
            "work-migration", "B1", null, true, false));
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(profile); Assert.Equal("migration", profile.Goal); Assert.Equal("work-migration", profile.GoalSubtype);
        Assert.Equal("B1", profile.TargetLevel); Assert.True(profile.LearningOnboardingCompleted); Assert.False(profile.LearningPathGuideDismissed);

        var persisted = await client.GetFromJsonAsync<UserResponse>("/api/profile");
        Assert.Equal(profile, persisted);
    }

    [Fact]
    public async Task Supplementary_learning_has_five_valid_categories_per_cefr_and_is_idempotent()
    {
        var demo = await AuthenticatedClient("demo@unidemix.local");
        var modules = await demo.Client.GetFromJsonAsync<JsonElement>("/api/learning/supplementary?languageCode=de");
        Assert.Equal(25, modules.GetArrayLength());
        foreach (var level in new[] { "A1", "A2", "B1", "B2", "C1" })
        {
            var levelModules = modules.EnumerateArray().Where(x => x.GetProperty("cefrLevel").GetString() == level).ToArray();
            Assert.Equal(SupplementaryContentImporter.Categories.Order(), levelModules.Select(x => x.GetProperty("category").GetString()!).Order());
            Assert.All(levelModules, module => { Assert.True(module.GetProperty("items").GetArrayLength() >= 8); Assert.Equal("SupplementaryContextualTutor", module.GetProperty("ai").GetProperty("capability").GetString()); Assert.Equal("GermanLanguageLearningOnly", module.GetProperty("ai").GetProperty("scope").GetString()); });
        }
        using var scope = factory.Services.CreateScope();
        var importer = ActivatorUtilities.CreateInstance<SupplementaryContentImporter>(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.SupplementaryModules.CountAsync();
        await importer.ImportAsync("Content/Learning/German/Supplementary/supplementary-v1.json");
        Assert.Equal(before, await db.SupplementaryModules.CountAsync());
    }

    [Fact]
    public async Task Social_username_uses_email_local_part_and_guarantees_unique_normalized_values()
    {
        async Task<(HttpClient Client, AuthResponse Auth)> Register(string email)
        {
            var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Password123!", "نام نمایشی"));
            response.EnsureSuccessStatusCode();
            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(auth);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            return (client, auth);
        }

        var first = await Register("siamak.abbasi@example.com");
        var second = await Register("siamak.abbasi@example.org");
        var profiles = new[]
        {
            await first.Client.GetFromJsonAsync<SocialProfileResponse>("/api/social/profile"),
            await second.Client.GetFromJsonAsync<SocialProfileResponse>("/api/social/profile")
        };

        Assert.All(profiles, Assert.NotNull);
        Assert.Contains(profiles, x => x!.Username == "siamak.abbasi");
        Assert.Contains(profiles, x => x!.Username.StartsWith("siamak.abbasi", StringComparison.Ordinal) && x.Username != "siamak.abbasi");
        Assert.Equal(2, profiles.Select(x => x!.Username.ToLowerInvariant()).Distinct().Count());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.DoesNotContain(await db.SocialProfiles.Select(x => x.Username).ToListAsync(), x =>
            System.Text.RegularExpressions.Regex.IsMatch(x, "^member[0-9]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    [Fact]
    public async Task Social_discovery_follow_report_and_block_rules_are_enforced()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("demo@unidemix.local", "Demo123!"));
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var discovery = await client.GetFromJsonAsync<PagedResponse<PartnerSummaryResponse>>("/api/social/discover?page=1&pageSize=20");
        Assert.NotNull(discovery);
        Assert.DoesNotContain(discovery.Items, x => x.UserId == auth.User.Id);
        Assert.DoesNotContain(discovery.Items, x => x.DisplayName == "آرش شریفی");
        var partner = discovery.Items.First(x => x.NativeLanguageCode == "de" && x.LearningLanguageCode == "fa");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/social/follows/{auth.User.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/social/follows/{partner.UserId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/social/follows/{partner.UserId}", null)).StatusCode);

        var report = new CreateUserReportRequest(partner.UserId, "Spam", "Development test report");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/social/reports", report)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/social/reports", report)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/social/blocks/{partner.UserId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/social/follows/{partner.UserId}", null)).StatusCode);
        var afterBlock = await client.GetFromJsonAsync<PagedResponse<PartnerSummaryResponse>>("/api/social/discover?page=1&pageSize=20");
        Assert.NotNull(afterBlock);
        Assert.DoesNotContain(afterBlock.Items, x => x.UserId == partner.UserId);
    }

    [Fact]
    public async Task Private_profile_follow_requests_enforce_lifecycle_authorization_notifications_and_blocks()
    {
        var requester = await AuthenticatedClient("parsa@unidemix.local");
        var owner = await AuthenticatedClient("niloofar@unidemix.local");
        var outsider = await AuthenticatedClient("demo@unidemix.local");
        await requester.Client.DeleteAsync($"/api/social/blocks/{owner.Auth.User.Id}");
        await owner.Client.DeleteAsync($"/api/social/blocks/{requester.Auth.User.Id}");
        await requester.Client.DeleteAsync($"/api/social/follows/{owner.Auth.User.Id}");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stale = await db.FollowRequests.Where(x => x.RequesterId == requester.Auth.User.Id && x.TargetUserId == owner.Auth.User.Id).ToListAsync();
            db.FollowRequests.RemoveRange(stale);
            var profile = await db.SocialProfiles.SingleAsync(x => x.UserId == owner.Auth.User.Id);
            profile.Privacy = "Private"; await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await requester.Client.PostAsync($"/api/social/follows/{requester.Auth.User.Id}", null)).StatusCode);
        var first = await requester.Client.PostAsync($"/api/social/follows/{owner.Auth.User.Id}", null); first.EnsureSuccessStatusCode();
        Assert.Equal("Requested", (await first.Content.ReadFromJsonAsync<FollowActionResponse>())!.State);
        var duplicate = await requester.Client.PostAsync($"/api/social/follows/{owner.Auth.User.Id}", null); duplicate.EnsureSuccessStatusCode();
        var incoming = await owner.Client.GetFromJsonAsync<PagedResponse<FollowRequestResponse>>("/api/social/follow-requests");
        Assert.NotNull(incoming); Assert.Single(incoming.Items.Where(x => x.RequesterId == requester.Auth.User.Id));
        var request = incoming.Items.Single(x => x.RequesterId == requester.Auth.User.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.PostAsync($"/api/social/follow-requests/{request.Id}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.PostAsync($"/api/social/follow-requests/{request.Id}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.PostAsync($"/api/social/follow-requests/{request.Id}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.PostAsync($"/api/social/follow-requests/{request.Id}/decline", null)).StatusCode);
        var notifications = await requester.Client.GetFromJsonAsync<PagedResponse<NotificationResponse>>("/api/notifications?pageSize=50");
        Assert.NotNull(notifications); Assert.Contains(notifications.Items, x => x.Type == "FollowRequestAccepted");

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/social/blocks/{requester.Auth.User.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await requester.Client.PostAsync($"/api/social/follows/{owner.Auth.User.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync($"/api/social/blocks/{requester.Auth.User.Id}")).StatusCode);
        var ownerProfile = await requester.Client.GetFromJsonAsync<SocialProfileResponse>($"/api/social/profiles/{owner.Auth.User.Id}");
        Assert.NotNull(ownerProfile); Assert.False(ownerProfile.IsFollowing);
    }

    [Fact]
    public async Task Social_seed_is_idempotent()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.SocialProfiles.CountAsync();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        var after = await db.SocialProfiles.CountAsync();
        Assert.Equal(before, after);
        Assert.True(after >= 8);
    }

    [Fact]
    public async Task Messaging_request_accept_chat_notifications_report_and_block_flow_works()
    {
        var sender = factory.CreateClient();
        var senderLogin = await sender.PostAsJsonAsync("/api/auth/login", new LoginRequest("reza@unidemix.local", "Demo123!"));
        var senderAuth = await senderLogin.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(senderAuth);
        sender.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", senderAuth.AccessToken);

        var recipient = factory.CreateClient();
        var recipientLogin = await recipient.PostAsJsonAsync("/api/auth/login", new LoginRequest("leila@unidemix.local", "Demo123!"));
        var recipientAuth = await recipientLogin.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(recipientAuth);
        recipient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", recipientAuth.AccessToken);

        var created = await sender.PostAsJsonAsync("/api/social/message-requests",
            new CreateMessageRequestRequest(recipientAuth.User.Id, "سلام، برای تمرین آلمانی گفتگو کنیم؟"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(await recipient.GetFromJsonAsync<int>("/api/notifications/unread-count") > 0);

        var incoming = await recipient.GetFromJsonAsync<PagedResponse<MessageRequestResponse>>("/api/social/message-requests?box=incoming");
        Assert.NotNull(incoming);
        var request = incoming.Items.Single(x => x.SenderId == senderAuth.User.Id && x.Status == "Pending");
        var accepted = await recipient.PostAsync($"/api/social/message-requests/{request.Id}/accept", null);
        accepted.EnsureSuccessStatusCode();
        var conversation = await accepted.Content.ReadFromJsonAsync<ConversationSummaryResponse>();
        Assert.NotNull(conversation);

        var sent = await sender.PostAsJsonAsync($"/api/social/conversations/{conversation.Id}/messages", new SendMessageRequest("Hallo! Wie geht's?"));
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        var opened = await recipient.GetFromJsonAsync<ConversationResponse>($"/api/social/conversations/{conversation.Id}");
        Assert.NotNull(opened);
        Assert.Contains(opened.Messages, x => x.Text == "Hallo! Wie geht's?" && x.ReadAt is not null);

        Assert.Equal(HttpStatusCode.NoContent, (await sender.PostAsJsonAsync($"/api/social/conversations/{conversation.Id}/report",
            new CreateConversationReportRequest("Harassment", "Test conversation report"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await recipient.PostAsync($"/api/social/blocks/{senderAuth.User.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sender.PostAsJsonAsync($"/api/social/conversations/{conversation.Id}/messages", new SendMessageRequest("blocked"))).StatusCode);
    }

    [Fact]
    public async Task Messaging_request_guards_reject_authorization_and_notification_types_are_enforced()
    {
        var parsa = await AuthenticatedClient("parsa@unidemix.local");
        var niloofar = await AuthenticatedClient("niloofar@unidemix.local");
        Assert.Equal(HttpStatusCode.BadRequest, (await parsa.Client.PostAsJsonAsync("/api/social/message-requests",
            new CreateMessageRequestRequest(parsa.Auth.User.Id, "درخواست گفتگو با خودم"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await parsa.Client.PostAsJsonAsync("/api/social/message-requests",
            new CreateMessageRequestRequest(niloofar.Auth.User.Id, "سلام، برای تمرین زبان گفتگو کنیم؟"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await parsa.Client.PostAsJsonAsync("/api/social/message-requests",
            new CreateMessageRequestRequest(niloofar.Auth.User.Id, "این درخواست تکراری است."))).StatusCode);

        var amir = await AuthenticatedClient("amir@unidemix.local");
        var nazanin = await AuthenticatedClient("nazanin@unidemix.local");
        Assert.Equal(HttpStatusCode.NoContent, (await amir.Client.PostAsync($"/api/social/blocks/{nazanin.Auth.User.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await nazanin.Client.PostAsJsonAsync("/api/social/message-requests",
            new CreateMessageRequestRequest(amir.Auth.User.Id, "این درخواست باید به دلیل بلاک رد شود."))).StatusCode);

        var arash = await AuthenticatedClient("arash@unidemix.local");
        var ali = await AuthenticatedClient("ali@unidemix.local");
        var firstRequestResponse = await arash.Client.PostAsJsonAsync("/api/social/message-requests",
            new CreateMessageRequestRequest(ali.Auth.User.Id, "سلام، برای تمرین زبان وقت دارید؟"));
        firstRequestResponse.EnsureSuccessStatusCode();
        var firstRequest = await firstRequestResponse.Content.ReadFromJsonAsync<MessageRequestResponse>();
        Assert.NotNull(firstRequest);
        Assert.Equal(HttpStatusCode.NoContent, (await ali.Client.PostAsync($"/api/social/message-requests/{firstRequest.Id}/reject", null)).StatusCode);
        var afterReject = await arash.Client.GetFromJsonAsync<PagedResponse<ConversationSummaryResponse>>("/api/social/conversations");
        Assert.NotNull(afterReject);
        Assert.DoesNotContain(afterReject.Items, x => x.PartnerId == ali.Auth.User.Id);

        var acceptedRequestResponse = await arash.Client.PostAsJsonAsync("/api/social/message-requests",
            new CreateMessageRequestRequest(ali.Auth.User.Id, "درخواست دوم برای ایجاد گفتگو."));
        var acceptedRequest = await acceptedRequestResponse.Content.ReadFromJsonAsync<MessageRequestResponse>();
        Assert.NotNull(acceptedRequest);
        var acceptedResponse = await ali.Client.PostAsync($"/api/social/message-requests/{acceptedRequest.Id}/accept", null);
        acceptedResponse.EnsureSuccessStatusCode();
        var conversation = await acceptedResponse.Content.ReadFromJsonAsync<ConversationSummaryResponse>();
        Assert.NotNull(conversation);
        Assert.Equal(HttpStatusCode.Conflict, (await ali.Client.PostAsJsonAsync("/api/social/message-requests",
            new CreateMessageRequestRequest(arash.Auth.User.Id, "نباید گفتگوی دوم ایجاد شود."))).StatusCode);

        var demo = await AuthenticatedClient("demo@unidemix.local");
        Assert.Equal(HttpStatusCode.NotFound, (await demo.Client.GetAsync($"/api/social/conversations/{conversation.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await arash.Client.PostAsJsonAsync($"/api/social/conversations/{conversation.Id}/messages",
            new SendMessageRequest("یک پیام متنی ساده"))).StatusCode);
        var aliNotifications = await ali.Client.GetFromJsonAsync<PagedResponse<NotificationResponse>>("/api/notifications");
        Assert.NotNull(aliNotifications);
        Assert.Contains(aliNotifications.Items, x => x.Type == "MessageRequest");
        Assert.Contains(aliNotifications.Items, x => x.Type == "NewMessage");
        var arashNotifications = await arash.Client.GetFromJsonAsync<PagedResponse<NotificationResponse>>("/api/notifications");
        Assert.NotNull(arashNotifications);
        Assert.Contains(arashNotifications.Items, x => x.Type == "RequestAccepted");
    }

    [Fact]
    public async Task Community_feed_like_comment_mentions_reports_ownership_and_block_rules_work()
    {
        var demo = await AuthenticatedClient("demo@unidemix.local");
        var ali = await AuthenticatedClient("ali@unidemix.local");
        await demo.Client.DeleteAsync($"/api/social/blocks/{ali.Auth.User.Id}");
        await ali.Client.DeleteAsync($"/api/social/blocks/{demo.Auth.User.Id}");
        var aliProfile = await ali.Client.GetFromJsonAsync<SocialProfileResponse>("/api/social/profile");
        var demoProfile = await demo.Client.GetFromJsonAsync<SocialProfileResponse>("/api/social/profile");
        Assert.NotNull(aliProfile); Assert.NotNull(demoProfile);

        using var testImage = new Image<Rgba32>(4, 3);
        await using var imageStream = new MemoryStream(); await testImage.SaveAsPngAsync(imageStream);
        using var upload = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageStream.ToArray()); imageContent.Headers.ContentType = new("image/png");
        upload.Add(imageContent, "file", "community-test.png");
        var uploadResponse = await demo.Client.PostAsync("/api/social/uploads/images", upload); uploadResponse.EnsureSuccessStatusCode();
        var storedImage = await uploadResponse.Content.ReadFromJsonAsync<ImageUploadResponse>(); Assert.NotNull(storedImage);
        var createdResponse = await demo.Client.PostAsJsonAsync("/api/social/posts",
            new CreatePostRequest(storedImage.StorageKey, $"تمرین امروز با @{aliProfile.Username} و دوباره @{aliProfile.Username} #آلمانی"));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var post = await createdResponse.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(post);

        var feed = await ali.Client.GetFromJsonAsync<PagedResponse<PostResponse>>("/api/social/feed?page=1&pageSize=2");
        Assert.NotNull(feed); Assert.True(feed.Items.Count <= 2); Assert.True(feed.TotalCount >= 1);
        Assert.Equal(HttpStatusCode.NoContent, (await ali.Client.PostAsync($"/api/social/posts/{post.Id}/likes", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ali.Client.PostAsync($"/api/social/posts/{post.Id}/likes", null)).StatusCode);
        var liked = await ali.Client.GetFromJsonAsync<PostDetailResponse>($"/api/social/posts/{post.Id}");
        Assert.NotNull(liked); Assert.Equal(1, liked.Post.LikeCount);
        Assert.Equal(HttpStatusCode.NoContent, (await ali.Client.DeleteAsync($"/api/social/posts/{post.Id}/likes")).StatusCode);

        var commentResponse = await ali.Client.PostAsJsonAsync($"/api/social/posts/{post.Id}/comments", new CreateCommentRequest($"عالی بود @{demoProfile.Username} @{demoProfile.Username}"));
        Assert.Equal(HttpStatusCode.Created, commentResponse.StatusCode);
        var comment = await commentResponse.Content.ReadFromJsonAsync<CommentResponse>(); Assert.NotNull(comment);
        Assert.Equal(HttpStatusCode.Forbidden, (await demo.Client.DeleteAsync($"/api/social/comments/{comment.Id}")).StatusCode);

        var mentionNotifications = await ali.Client.GetFromJsonAsync<PagedResponse<NotificationResponse>>("/api/notifications?pageSize=50");
        Assert.NotNull(mentionNotifications); Assert.Equal(1, mentionNotifications.Items.Count(x => x.Type == "Mentioned" && x.Destination == $"/social/posts/{post.Id}"));
        var demoNotifications = await demo.Client.GetFromJsonAsync<PagedResponse<NotificationResponse>>("/api/notifications?pageSize=50");
        Assert.NotNull(demoNotifications); Assert.Contains(demoNotifications.Items, x => x.Type == "PostLiked"); Assert.Contains(demoNotifications.Items, x => x.Type == "PostCommented");
        Assert.Equal(1, demoNotifications.Items.Count(x => x.Type == "Mentioned" && x.Destination == $"/social/posts/{post.Id}"));

        var report = new CreateContentReportRequest("Post", post.Id, "SpamOrAdvertising", null);
        Assert.Equal(HttpStatusCode.Created, (await ali.Client.PostAsJsonAsync("/api/social/content-reports", report)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ali.Client.PostAsJsonAsync("/api/social/content-reports", report)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ali.Client.DeleteAsync($"/api/social/posts/{post.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await demo.Client.PostAsync($"/api/social/blocks/{ali.Auth.User.Id}", null)).StatusCode);
        var suggestions = await demo.Client.GetFromJsonAsync<List<MentionSuggestionResponse>>($"/api/social/mentions?query={aliProfile.Username}");
        Assert.NotNull(suggestions); Assert.DoesNotContain(suggestions, x => x.UserId == ali.Auth.User.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await ali.Client.PostAsync($"/api/social/posts/{post.Id}/likes", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await demo.Client.DeleteAsync($"/api/social/blocks/{ali.Auth.User.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ali.Client.DeleteAsync($"/api/social/comments/{comment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await demo.Client.DeleteAsync($"/api/social/posts/{post.Id}")).StatusCode);
    }

    [Fact]
    public async Task Advertising_features_are_disabled_by_default_and_admin_can_enable_without_losing_placements()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/ads/placements")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/ads/active")).StatusCode);

        var admin = await AuthenticatedClient("admin@unidemix.local");
        var placementsBefore = await admin.Client.GetFromJsonAsync<List<Unidemix.Api.Models.AdPlacement>>("/api/admin/ad-placements");
        Assert.NotNull(placementsBefore);
        Assert.NotEmpty(placementsBefore);

        foreach (var key in new[] { "AdvertisingEnabled", "FixedAdReservationEnabled" })
        {
            var enabled = await admin.Client.PutAsJsonAsync($"/api/admin/product-features/{key}", new { isEnabled = true });
            enabled.EnsureSuccessStatusCode();
        }

        (await client.GetAsync("/api/ads/placements")).EnsureSuccessStatusCode();

        foreach (var key in new[] { "AdvertisingEnabled", "FixedAdReservationEnabled" })
        {
            var disabled = await admin.Client.PutAsJsonAsync($"/api/admin/product-features/{key}", new { isEnabled = false });
            disabled.EnsureSuccessStatusCode();
        }

        var placementsAfter = await admin.Client.GetFromJsonAsync<List<Unidemix.Api.Models.AdPlacement>>("/api/admin/ad-placements");
        Assert.Equal(placementsBefore.Count, placementsAfter?.Count);
    }

    [Fact]
    public async Task Vocabulary_review_schedules_cards_and_exam_catalog_is_data_driven()
    {
        var demo = await AuthenticatedClient("demo@unidemix.local");
        var summaryBefore = JsonDocument.Parse(await demo.Client.GetStringAsync("/api/vocabulary/review/summary"));
        var dueBefore = summaryBefore.RootElement.GetProperty("dueCount").GetInt32();
        Assert.True(dueBefore > 0);

        var cards = JsonDocument.Parse(await demo.Client.GetStringAsync("/api/vocabulary/review/today?limit=5"));
        var first = cards.RootElement[0];
        var itemId = first.GetProperty("id").GetGuid();
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("term").GetString()));
        (await demo.Client.PostAsJsonAsync($"/api/vocabulary/{itemId}/review", new { known = true })).EnsureSuccessStatusCode();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var review = await db.VocabularyReviews.SingleAsync(x => x.UserId == demo.Auth.User.Id && x.VocabularyItemId == itemId);
            Assert.Equal(2, review.IntervalDays);
            Assert.True(review.NextReviewAt > DateTimeOffset.UtcNow.AddDays(1));
        }

        var exams = await demo.Client.GetStringAsync("/api/exams?languageCode=de&level=A1");
        Assert.Contains("goethe", exams, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("telc", exams, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cefrMappings", exams, StringComparison.OrdinalIgnoreCase);

        var courses = JsonDocument.Parse(await demo.Client.GetStringAsync("/api/courses"));
        var a1Core = courses.RootElement.EnumerateArray().First(x => x.GetProperty("level").GetString() == "A1" && x.GetProperty("kind").GetString() == "Core");
        var courseLessons = a1Core.GetProperty("lessons").EnumerateArray().ToArray();
        Assert.Equal(12, courseLessons.Count(x => x.GetProperty("order").GetInt32() > 0));
        Assert.Equal(Enumerable.Range(1, 12), courseLessons.Where(x => x.GetProperty("order").GetInt32() > 0).Select(x => x.GetProperty("order").GetInt32()).Order());
        foreach (var lessonSummary in courseLessons.Where(x => x.GetProperty("order").GetInt32() > 1))
        {
            var imported = JsonDocument.Parse(await demo.Client.GetStringAsync($"/api/courses/lessons/{lessonSummary.GetProperty("id").GetGuid()}"));
            Assert.Equal(7, imported.RootElement.GetProperty("sections").GetArrayLength());
            Assert.Equal(19, imported.RootElement.GetProperty("exercises").GetArrayLength());
            Assert.Equal(8, imported.RootElement.GetProperty("questionBankCount").GetInt32());
            Assert.Equal("script-ready-audio-deferred", imported.RootElement.GetProperty("audio").GetProperty("status").GetString());
            Assert.Equal(2, imported.RootElement.GetProperty("learningContext").GetProperty("canDoObjectives").GetArrayLength());
            var guided = imported.RootElement.GetProperty("exercises").EnumerateArray().ToArray();
            Assert.Single(guided.Where(x => x.GetProperty("type").GetString() == "introduction"));
            Assert.True(
                Array.FindIndex(guided, x => x.GetProperty("type").GetString() == "grammar-explanation") <
                Array.FindIndex(guided, x => x.GetProperty("type").GetString() == "speaking"));
        }
        var foundationSummary = courseLessons.Single(x => x.GetProperty("order").GetInt32() == 0);
        var foundationId = foundationSummary.GetProperty("id").GetGuid();
        var foundation = JsonDocument.Parse(await demo.Client.GetStringAsync($"/api/courses/lessons/{foundationId}"));
        Assert.Equal(7, foundation.RootElement.GetProperty("sections").GetArrayLength());
        var foundationActivities = foundation.RootElement.GetProperty("exercises").EnumerateArray().ToArray();
        Assert.True(foundationActivities.Length >= 9);
        Assert.Contains(foundationActivities, x => x.GetProperty("sectionCode").GetString() == "final-practice");
        Assert.Contains(foundationActivities, x => x.GetProperty("kind").GetString() == "Instruction");
        Assert.Contains(foundationActivities, x => x.GetProperty("kind").GetString() == "Exercise");
        Assert.Contains("الفبا", foundation.RootElement.GetProperty("title").GetString());

        var lessonId = courseLessons.Single(x => x.GetProperty("order").GetInt32() == 1).GetProperty("id").GetGuid();
        var lesson = JsonDocument.Parse(await demo.Client.GetStringAsync($"/api/courses/lessons/{lessonId}"));
        Assert.Equal(7, lesson.RootElement.GetProperty("sections").GetArrayLength());
        Assert.Equal("script-ready-audio-deferred", lesson.RootElement.GetProperty("audio").GetProperty("status").GetString());
        Assert.False(lesson.RootElement.GetProperty("audio").GetProperty("isProductionReady").GetBoolean());
        Assert.Equal("A1", lesson.RootElement.GetProperty("learningContext").GetProperty("cefr").GetString());
        Assert.True(lesson.RootElement.GetProperty("learningContext").GetProperty("knownVocabulary").GetArrayLength() > 0);
        Assert.Equal(8, lesson.RootElement.GetProperty("questionBankCount").GetInt32());
        var activities = lesson.RootElement.GetProperty("exercises").EnumerateArray().ToArray();
        Assert.True(activities.Length >= 20);
        Assert.All(activities, activity => Assert.False(string.IsNullOrWhiteSpace(activity.GetProperty("sectionCode").GetString())));
        Assert.Contains(activities, activity => activity.GetProperty("kind").GetString() == "Instruction");
        Assert.Contains(activities, activity => activity.GetProperty("kind").GetString() == "Exercise");
        Assert.All(activities.Where(activity => activity.GetProperty("kind").GetString() == "Instruction"),
            activity => Assert.Equal(JsonValueKind.Null, activity.GetProperty("correctAnswer").ValueKind));
        Assert.True(activities.Select(activity => activity.GetProperty("sectionCode").GetString()).Distinct().Count() > 1);
        var firstPractice = JsonDocument.Parse(await demo.Client.GetStringAsync($"/api/courses/lessons/{lessonId}/practice-session?size=5&attempt=0"));
        var retryPractice = JsonDocument.Parse(await demo.Client.GetStringAsync($"/api/courses/lessons/{lessonId}/practice-session?size=5&attempt=1"));
        Assert.Equal(8, firstPractice.RootElement.GetProperty("bankCount").GetInt32());
        Assert.Equal(5, firstPractice.RootElement.GetProperty("items").GetArrayLength());
        Assert.NotEqual(
            firstPractice.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid(),
            retryPractice.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        (await demo.Client.PutAsJsonAsync($"/api/progress/lessons/{lessonId}/sections/grammar", new { percent = 100 })).EnsureSuccessStatusCode();
        var resumed = JsonDocument.Parse(await demo.Client.GetStringAsync($"/api/courses/lessons/{lessonId}"));
        Assert.Contains(resumed.RootElement.GetProperty("sections").EnumerateArray(), x => x.GetProperty("code").GetString() == "grammar" && x.GetProperty("isCompleted").GetBoolean());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var before = await db.VocabularyItems.CountAsync();
            var activitiesBefore = await db.Exercises.CountAsync();
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
            Assert.Equal(before, await db.VocabularyItems.CountAsync());
            Assert.Equal(activitiesBefore, await db.Exercises.CountAsync());
        }
    }

    [Fact]
    public async Task Telc_mock_attempt_hides_answers_and_transcripts_and_returns_provisional_result()
    {
        var demo = await AuthenticatedClient("demo@unidemix.local");
        using var catalog = JsonDocument.Parse(await demo.Client.GetStringAsync("/api/exams?languageCode=de&level=B1"));
        var telc = catalog.RootElement.EnumerateArray().Single(x => x.GetProperty("code").GetString() == "telc");
        var program = telc.GetProperty("programs").EnumerateArray().Single();
        Assert.True(program.GetProperty("isPublished").GetBoolean());
        var programId = program.GetProperty("id").GetGuid();

        using var definition = JsonDocument.Parse(await demo.Client.GetStringAsync($"/api/exams/{programId}"));
        var variant = definition.RootElement.GetProperty("mockVariants")[0];
        var listening = variant.GetProperty("tasks").EnumerateArray().First(x => x.GetProperty("section").GetString() == "listening");
        Assert.Equal(JsonValueKind.Null, listening.GetProperty("script").ValueKind);
        Assert.False(listening.TryGetProperty("correctAnswer", out _));

        var started = await demo.Client.PostAsJsonAsync($"/api/exams/{programId}/attempts", new { variantKey = variant.GetProperty("key").GetString() });
        started.EnsureSuccessStatusCode();
        var attempt = await started.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = attempt.GetProperty("id").GetGuid();
        var taskKey = listening.GetProperty("key").GetString()!;
        var maximum = listening.GetProperty("playbackCount").GetInt32();
        for (var play = 1; play <= maximum; play++)
        {
            var playback = await demo.Client.PostAsync($"/api/exams/attempts/{attemptId}/playback/{taskKey}", null);
            playback.EnsureSuccessStatusCode();
            var state = await playback.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(string.IsNullOrWhiteSpace(state.GetProperty("script").GetString()));
            Assert.Equal("de-DE", state.GetProperty("language").GetString());
            Assert.Equal(play, state.GetProperty("used").GetInt32());
        }
        Assert.Equal(HttpStatusCode.Conflict, (await demo.Client.PostAsync($"/api/exams/attempts/{attemptId}/playback/{taskKey}", null)).StatusCode);
        var persisted = await demo.Client.GetFromJsonAsync<JsonElement>($"/api/exams/attempts/{attemptId}");
        Assert.Equal(maximum, persisted.GetProperty("playbackCounts").GetProperty(taskKey).GetInt32());
        Assert.False(persisted.GetProperty("answers").TryGetProperty($"__playback:{taskKey}", out _));
        (await demo.Client.PutAsJsonAsync($"/api/exams/attempts/{attemptId}", new { answers = new Dictionary<string, string>() })).EnsureSuccessStatusCode();
        var submitted = await demo.Client.PostAsJsonAsync($"/api/exams/attempts/{attemptId}/submit", new { answers = new Dictionary<string, string>() });
        submitted.EnsureSuccessStatusCode();
        var result = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Provisional", result.GetProperty("result").GetProperty("status").GetString());
        Assert.Contains(result.GetProperty("result").GetProperty("needsEvaluation").EnumerateArray(), x => x.GetString() == "writing");
        Assert.Contains(result.GetProperty("result").GetProperty("needsEvaluation").EnumerateArray(), x => x.GetString() == "speaking");
    }

    [Fact]
    public async Task Telc_A1_is_published_with_official_structure_scoring_rubrics_and_two_original_mocks()
    {
        var demo = await AuthenticatedClient("demo@unidemix.local");
        using var catalog = JsonDocument.Parse(await demo.Client.GetStringAsync("/api/exams?languageCode=de&level=A1"));
        var telc = catalog.RootElement.EnumerateArray().Single(x => x.GetProperty("code").GetString() == "telc");
        var program = telc.GetProperty("programs").EnumerateArray().Single();
        Assert.True(program.GetProperty("isPublished").GetBoolean());
        Assert.Equal(65, program.GetProperty("writtenDurationMinutes").GetInt32());
        Assert.Equal(15, program.GetProperty("speakingDurationMinutes").GetInt32());

        using var definition = JsonDocument.Parse(await demo.Client.GetStringAsync($"/api/exams/{program.GetProperty("id").GetGuid()}"));
        var root = definition.RootElement;
        var timing = root.GetProperty("timing");
        Assert.Equal(65, timing.GetProperty("writtenMinutes").GetInt32());
        Assert.Equal(10, timing.GetProperty("administrativeDurationMinutes").GetInt32());
        Assert.Equal(75, timing.GetProperty("sessionDurationMinutes").GetInt32());
        Assert.Equal(20, timing.GetProperty("listeningDurationMinutes").GetInt32());
        Assert.Equal(45, timing.GetProperty("readingWritingDurationMinutes").GetInt32());
        Assert.Equal(15, timing.GetProperty("speakingMinutes").GetInt32());
        Assert.Equal(new[] { 3, 3, 2, 3 }, root.GetProperty("sections").EnumerateArray().Select(x => x.GetProperty("parts").GetArrayLength()));
        Assert.Equal(2, root.GetProperty("mockVariants").GetArrayLength());
        Assert.True(root.GetProperty("isFullMockComplete").GetBoolean());
        Assert.All(root.GetProperty("mockVariants").EnumerateArray(), variant => Assert.Equal(39, variant.GetProperty("tasks").GetArrayLength()));
        foreach (var variant in root.GetProperty("mockVariants").EnumerateArray())
        {
            var tasks = variant.GetProperty("tasks").EnumerateArray().ToArray();
            Assert.Equal(6, tasks.Count(x => x.GetProperty("part").GetString() == "h1"));
            Assert.Equal(4, tasks.Count(x => x.GetProperty("part").GetString() == "h2"));
            Assert.Equal(5, tasks.Count(x => x.GetProperty("part").GetString() == "h3"));
            Assert.Equal(5, tasks.Count(x => x.GetProperty("part").GetString() == "r1"));
            Assert.Equal(5, tasks.Count(x => x.GetProperty("part").GetString() == "r2"));
            Assert.Equal(5, tasks.Count(x => x.GetProperty("part").GetString() == "r3"));
            Assert.Equal(5, tasks.Count(x => x.GetProperty("part").GetString() == "w1"));
            Assert.Equal(2, tasks.Where(x => x.GetProperty("section").GetString() == "writing").Select(x => x.GetProperty("part").GetString()).Distinct().Count());
            Assert.Equal(3, tasks.Count(x => x.GetProperty("section").GetString() == "speaking"));
        }
        var variants = root.GetProperty("mockVariants").EnumerateArray().ToArray();
        Assert.NotEqual(variants[0].GetRawText(), variants[1].GetRawText());
        Assert.Equal(22, root.GetProperty("practiceBank").GetArrayLength());
        var scoring = root.GetProperty("scoring");
        Assert.Equal(60, scoring.GetProperty("maxPoints").GetInt32());
        Assert.Equal(36, scoring.GetProperty("passPoints").GetInt32());
        Assert.Equal(5, scoring.GetProperty("resultBands").GetArrayLength());
        Assert.True(scoring.GetProperty("writingRubric").GetProperty("part2").GetProperty("requiresEvaluation").GetBoolean());
        Assert.True(scoring.GetProperty("speakingRubric").GetProperty("requiresEvaluation").GetBoolean());

        var firstVariant = variants[0].GetProperty("key").GetString();
        var started = await demo.Client.PostAsJsonAsync($"/api/exams/{program.GetProperty("id").GetGuid()}/attempts", new { variantKey = firstVariant });
        started.EnsureSuccessStatusCode();
        var attempt = await started.Content.ReadFromJsonAsync<JsonElement>();
        var submitted = await demo.Client.PostAsJsonAsync($"/api/exams/attempts/{attempt.GetProperty("id").GetGuid()}/submit", new { answers = new Dictionary<string, string>() });
        submitted.EnsureSuccessStatusCode();
        var result = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Provisional", result.GetProperty("result").GetProperty("status").GetString());
        Assert.Equal(35, result.GetProperty("result").GetProperty("automaticallyScoredMaximum").GetInt32());
    }

    [Theory]
    [InlineData("A2", 34)]
    [InlineData("B1", 64)]
    [InlineData("B2", 64)]
    public async Task Telc_A2_to_B2_variants_match_every_declared_official_part_count(string level, int totalTasks)
    {
        var demo = await AuthenticatedClient("demo@unidemix.local");
        using var catalog = JsonDocument.Parse(await demo.Client.GetStringAsync($"/api/exams?languageCode=de&level={level}"));
        var program = catalog.RootElement.EnumerateArray().Single(x => x.GetProperty("code").GetString() == "telc").GetProperty("programs")[0];
        using var definition = JsonDocument.Parse(await demo.Client.GetStringAsync($"/api/exams/{program.GetProperty("id").GetGuid()}"));
        var root = definition.RootElement;
        Assert.True(root.GetProperty("isFullMockComplete").GetBoolean());
        var expected = root.GetProperty("sections").EnumerateArray().SelectMany(section => section.GetProperty("parts").EnumerateArray()
            .Select(part => (Section: section.GetProperty("code").GetString()!, Part: part.GetProperty("key").GetString()!,
                Count: part.GetProperty("taskType").GetString() is "writing" or "speaking" ? 1 : part.GetProperty("itemCount").GetInt32()))).ToArray();
        foreach (var variant in root.GetProperty("mockVariants").EnumerateArray())
        {
            var tasks = variant.GetProperty("tasks").EnumerateArray().ToArray();
            Assert.Equal(totalTasks, tasks.Length);
            Assert.All(expected, item => Assert.Equal(item.Count, tasks.Count(x => x.GetProperty("section").GetString() == item.Section && x.GetProperty("part").GetString() == item.Part)));
        }
    }

    [Fact]
    public async Task Stored_telc_full_mock_validator_rejects_structural_drift()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var program = await db.ExamPrograms.AsNoTracking().Include(x => x.Provider).SingleAsync(x => x.Provider.Code == "telc" && x.Level == "A1");
        var package = new TelcExamPackage("telc", "de", "A1", program.Name, program.ContentVersion, program.SourceReference!,
            JsonSerializer.Deserialize<ExamTimingSource>(program.TimingJson!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!,
            JsonSerializer.Deserialize<ExamSectionSource[]>(program.BlueprintJson!)!,
            JsonSerializer.Deserialize<ExamTaskSource[]>(program.PracticeBankJson!)!,
            JsonSerializer.Deserialize<MockVariantSource[]>(program.MockVariantsJson!)!,
            JsonSerializer.Deserialize<ExamScoringSource>(program.ScoringJson!)!);
        Assert.True(TelcExamPackageImporter.ValidateFullMocks(package).IsFullMockComplete);

        var first = package.MockVariants[0];
        Assert.False(TelcExamPackageImporter.ValidateFullMocks(package with { MockVariants = [first with { Tasks = first.Tasks.Skip(1).ToArray() }, package.MockVariants[1]] }).IsFullMockComplete);
        Assert.False(TelcExamPackageImporter.ValidateFullMocks(package with { Timing = package.Timing with { WrittenMinutes = 64 } }).IsFullMockComplete);
        Assert.False(TelcExamPackageImporter.ValidateFullMocks(package with { Timing = package.Timing with { AdministrativeDurationMinutes = 9 } }).IsFullMockComplete);
        Assert.False(TelcExamPackageImporter.ValidateFullMocks(package with { Sections = package.Sections.Where(x => x.Code != "writing").ToArray(), MockVariants = package.MockVariants.Select(v => v with { Tasks = v.Tasks.Where(x => x.Section != "writing").ToArray() }).ToArray() }).IsFullMockComplete);
        Assert.False(TelcExamPackageImporter.ValidateFullMocks(package with { MockVariants = [first with { Tasks = first.Tasks.Select((x, i) => i == 0 ? x with { Type = "wrong-type" } : x).ToArray() }, package.MockVariants[1]] }).IsFullMockComplete);
        Assert.False(TelcExamPackageImporter.ValidateFullMocks(package with { MockVariants = [first with { Tasks = first.Tasks.Select((x, i) => i == 0 ? x with { PlaybackCount = 1 } : x).ToArray() }, package.MockVariants[1]] }).IsFullMockComplete);
        Assert.False(TelcExamPackageImporter.ValidateFullMocks(package with { MockVariants = [first with { Tasks = first.Tasks.Select((x, i) => i == 0 ? x with { Options = [x.Options![0], x.Options[0], x.Options[2]] } : x).ToArray() }, package.MockVariants[1]] }).IsFullMockComplete);
    }

    [Fact]
    public async Task Exam_practice_sessions_resume_by_provider_level_and_part_and_mock_retries_are_independent()
    {
        var demo = await AuthenticatedClient("demo@unidemix.local");
        using var telcCatalog = JsonDocument.Parse(await demo.Client.GetStringAsync("/api/exams?languageCode=de&level=A1"));
        var telc = telcCatalog.RootElement.EnumerateArray().Single(x => x.GetProperty("code").GetString() == "telc").GetProperty("programs")[0];
        var programId = telc.GetProperty("id").GetGuid();
        using var definition = JsonDocument.Parse(await demo.Client.GetStringAsync($"/api/exams/{programId}"));
        var practice = definition.RootElement.GetProperty("practiceBank").EnumerateArray();
        Assert.All(practice.Where(x => x.GetProperty("section").GetString() == "listening" && x.GetProperty("part").GetString() == "h1"), x => Assert.Equal("h1", x.GetProperty("part").GetString()));
        var started = await demo.Client.PostAsJsonAsync($"/api/exams/{programId}/practice-sessions", new { sectionKey = "listening", partKey = "h1" });
        started.EnsureSuccessStatusCode(); var session = await started.Content.ReadFromJsonAsync<JsonElement>();
        var answer = practice.First(x => x.GetProperty("part").GetString() == "h1");
        var savedAnswer = new Dictionary<string,string> { [answer.GetProperty("key").GetString()!] = answer.GetProperty("correctAnswer").GetString()! };
        (await demo.Client.PutAsJsonAsync($"/api/exams/practice-sessions/{session.GetProperty("id").GetGuid()}", new { currentItemIndex = 1, answers = savedAnswer, submittedItems = savedAnswer.Keys.ToArray() })).EnsureSuccessStatusCode();
        var resumed = await (await demo.Client.PostAsJsonAsync($"/api/exams/{programId}/practice-sessions", new { sectionKey = "listening", partKey = "h1" })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(session.GetProperty("id").GetGuid(), resumed.GetProperty("id").GetGuid()); Assert.Equal(1, resumed.GetProperty("currentItemIndex").GetInt32());

        var variant = definition.RootElement.GetProperty("mockVariants")[0].GetProperty("key").GetString();
        var first = await (await demo.Client.PostAsJsonAsync($"/api/exams/{programId}/attempts", new { variantKey = variant, executionMode = "Familiarization" })).Content.ReadFromJsonAsync<JsonElement>();
        var second = await (await demo.Client.PostAsJsonAsync($"/api/exams/{programId}/attempts", new { variantKey = variant, executionMode = "TimedSimulation" })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid()); Assert.Equal("Familiarization", first.GetProperty("executionMode").GetString()); Assert.Equal("TimedSimulation", second.GetProperty("executionMode").GetString());
        var firstId = first.GetProperty("id").GetGuid();
        (await demo.Client.PutAsJsonAsync($"/api/exams/attempts/{firstId}", new { currentItemIndex = 2, answers = savedAnswer })).EnsureSuccessStatusCode();
        var continued = await (await demo.Client.GetAsync($"/api/exams/attempts/{firstId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InProgress", continued.GetProperty("status").GetString());
        Assert.Equal(2, continued.GetProperty("currentItemIndex").GetInt32());
        Assert.Equal(savedAnswer.Single().Value, continued.GetProperty("answers").GetProperty(savedAnswer.Single().Key).GetString());

        using var goetheCatalog = JsonDocument.Parse(await demo.Client.GetStringAsync("/api/exams?languageCode=de&level=C1"));
        var goetheId = goetheCatalog.RootElement.EnumerateArray().Single(x => x.GetProperty("code").GetString() == "goethe").GetProperty("programs")[0].GetProperty("id").GetGuid();
        using var goethe = JsonDocument.Parse(await demo.Client.GetStringAsync($"/api/exams/{goetheId}"));
        Assert.NotEmpty(goethe.RootElement.GetProperty("practiceBank").EnumerateArray()); Assert.Equal(2, goethe.RootElement.GetProperty("mockVariants").GetArrayLength());
        Assert.All(goethe.RootElement.GetProperty("practiceBank").EnumerateArray(), x => Assert.StartsWith("goethe-c1-", x.GetProperty("key").GetString()));
    }

    [Fact]
    public async Task Grammar_session_persists_question_and_option_order_across_resume()
    {
        var demo = await AuthenticatedClient("demo@unidemix.local");
        using var topics = JsonDocument.Parse(await demo.Client.GetStringAsync("/api/learning/grammar?languageCode=de&level=A1"));
        var topicId = topics.RootElement.EnumerateArray().First().GetProperty("id").GetGuid();
        var startedResponse = await demo.Client.PostAsJsonAsync("/api/learning/grammar/sessions", new { languageCode = "de", cefrLevel = "A1", topicId, mode = "Topic", forceNew = true });
        startedResponse.EnsureSuccessStatusCode(); var started = await startedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = started.GetProperty("id").GetGuid();
        var resumed = await demo.Client.GetFromJsonAsync<JsonElement>($"/api/learning/grammar/sessions/{sessionId}");
        Assert.Equal(started.GetProperty("questions").GetRawText(), resumed.GetProperty("questions").GetRawText());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.GrammarPracticeSessions.AsNoTracking().SingleAsync(x => x.Id == sessionId);
        var optionOrder = JsonSerializer.Deserialize<Dictionary<string,string[]>>(stored.QuestionOptionOrderJson);
        Assert.NotNull(optionOrder); Assert.Equal(started.GetProperty("questions").GetArrayLength(), optionOrder.Count);
        var topic = await db.GrammarTopics.AsNoTracking().SingleAsync(x => x.Id == topicId);
        var bank = JsonSerializer.Deserialize<GrammarExercise[]>(topic.ExercisesJson)!;
        var positions = bank.Where(x => optionOrder.TryGetValue(x.Id, out var options) && options.Length > 0).Select(x => Array.FindIndex(optionOrder[x.Id], x.AcceptedAnswers.Contains)).ToArray();
        var counts = positions.GroupBy(x => x).Select(x => x.Count()).ToArray();
        Assert.True(counts.Max() - counts.Min() <= 1);
        Assert.DoesNotContain(new[] { 2, 3 }, size => positions.Length >= size * 2 && positions.Select((value, index) => index < size || value == positions[index % size]).All(x => x));
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> AuthenticatedClient(string email)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Demo123!"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }
}
