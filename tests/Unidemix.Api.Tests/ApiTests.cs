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
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/social/follows/{partner.UserId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/social/follows/{partner.UserId}", null)).StatusCode);

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

        using var testImage = new Image<Rgba32>(4, 3);
        await using var imageStream = new MemoryStream(); await testImage.SaveAsPngAsync(imageStream);
        using var upload = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageStream.ToArray()); imageContent.Headers.ContentType = new("image/png");
        upload.Add(imageContent, "file", "community-test.png");
        var uploadResponse = await demo.Client.PostAsync("/api/social/uploads/images", upload); uploadResponse.EnsureSuccessStatusCode();
        var storedImage = await uploadResponse.Content.ReadFromJsonAsync<ImageUploadResponse>(); Assert.NotNull(storedImage);
        var createdResponse = await demo.Client.PostAsJsonAsync("/api/social/posts",
            new CreatePostRequest(storedImage.StorageKey, "تمرین امروز با @ali و دوباره @ali #آلمانی"));
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

        var commentResponse = await ali.Client.PostAsJsonAsync($"/api/social/posts/{post.Id}/comments", new CreateCommentRequest("عالی بود @demo @demo"));
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
        var suggestions = await demo.Client.GetFromJsonAsync<List<MentionSuggestionResponse>>("/api/social/mentions?query=ali");
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
            Assert.Equal(20, imported.RootElement.GetProperty("exercises").GetArrayLength());
            Assert.Equal(8, imported.RootElement.GetProperty("questionBankCount").GetInt32());
            Assert.Equal("script-ready-audio-deferred", imported.RootElement.GetProperty("audio").GetProperty("status").GetString());
            Assert.Equal(2, imported.RootElement.GetProperty("learningContext").GetProperty("canDoObjectives").GetArrayLength());
            var guided = imported.RootElement.GetProperty("exercises").EnumerateArray().ToArray();
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
