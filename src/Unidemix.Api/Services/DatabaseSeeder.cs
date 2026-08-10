using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Services;

public sealed class DatabaseSeeder(AppDbContext db, IWebHostEnvironment environment)
{
    private const string TestPassword = "Demo123!";

    public async Task SeedAsync()
    {
        await SeedBusinessAsync();
        await SeedUsersAsync();
        await SeedCatalogAsync();
        await db.SaveChangesAsync();
        var learningImporter = new LearningContentPackageImporter(db, environment);
        foreach (var package in new[]
        {
            "lesson-00-alphabet-pronunciation.json",
            "lesson-01-first-contact.json",
            "lesson-02-people-around-me.json",
            "lesson-03-shopping.json",
            "lesson-04-free-time-plans.json",
            "lesson-05-food-hospitality.json",
            "lesson-06-getting-around.json",
            "lesson-07-my-day-week.json",
            "lesson-08-what-happened.json",
            "lesson-09-home-neighborhood.json",
            "lesson-10-health-appointments.json",
            "lesson-11-clothes-choices.json",
            "lesson-12-plans-weather-celebrations.json"
        })
            await learningImporter.ImportAsync($"Content/Learning/German/A1/{package}");
        await SeedLearningExtensionsAsync();
        await db.SaveChangesAsync();
        await SeedCitiesAsync();
        await db.SaveChangesAsync();
        await SeedSocialAsync();
        await db.SaveChangesAsync();
        await SeedCommunityAsync();
        await db.SaveChangesAsync();
        await SeedMessagingAsync();
        await db.SaveChangesAsync();
        await SeedProgressAsync();
        await db.SaveChangesAsync();
    }

    private async Task SeedCitiesAsync()
    {
        var cities = new[]
        {
            ("DE","berlin","برلین","Berlin","Berlin","berlin|berleen"), ("DE","hamburg","هامبورگ","Hamburg","Hamburg","hamburg|hamborg"),
            ("DE","munich","مونیخ","Munich","München","munich|munchen|muenchen|monikh"), ("DE","cologne","کلن","Cologne","Köln","cologne|koln|koeln|keln"),
            ("DE","frankfurt","فرانکفورت","Frankfurt","Frankfurt","frankfurt|frankfort"), ("DE","dresden","درسدن","Dresden","Dresden","dresden"),
            ("AT","vienna","وین","Vienna","Wien","vienna|wien|vin"), ("AT","graz","گراتس","Graz","Graz","graz|grats"), ("AT","salzburg","سالزبورگ","Salzburg","Salzburg","salzburg"),
            ("CH","zurich","زوریخ","Zurich","Zürich","zurich|zurich|zuerich"), ("CH","geneva","ژنو","Geneva","Genf","geneva|geneve|genf"), ("CH","basel","بازل","Basel","Basel","basel"),
            ("IR","tehran","تهران","Tehran",null,"tehran|teheran"), ("IR","mashhad","مشهد","Mashhad",null,"mashhad|mashad"), ("IR","isfahan","اصفهان","Isfahan",null,"isfahan|esfahan"), ("IR","shiraz","شیراز","Shiraz",null,"shiraz"), ("IR","tabriz","تبریز","Tabriz",null,"tabriz"),
            ("TR","istanbul","استانبول","Istanbul",null,"istanbul|estambul"), ("TR","ankara","آنکارا","Ankara",null,"ankara"), ("TR","izmir","ازمیر","Izmir",null,"izmir"),
            ("GB","london","لندن","London",null,"london"), ("GB","manchester","منچستر","Manchester",null,"manchester"),
            ("FR","paris","پاریس","Paris",null,"paris"), ("FR","lyon","لیون","Lyon",null,"lyon"),
            ("NL","amsterdam","آمستردام","Amsterdam",null,"amsterdam"), ("NL","rotterdam","روتردام","Rotterdam",null,"rotterdam"),
            ("US","new-york","نیویورک","New York",null,"new york|newyork|nyc"), ("US","los-angeles","لس‌آنجلس","Los Angeles",null,"los angeles|la"),
            ("CA","toronto","تورنتو","Toronto",null,"toronto"), ("CA","vancouver","ونکوور","Vancouver",null,"vancouver"),
            ("AU","sydney","سیدنی","Sydney",null,"sydney"), ("AU","melbourne","ملبورن","Melbourne",null,"melbourne")
        };
        var existing = (await db.Cities.Select(x => new { x.CountryCode, x.CanonicalName }).ToListAsync()).Select(x => (x.CountryCode, x.CanonicalName)).ToHashSet();
        foreach (var city in cities.Where(x => !existing.Contains((x.Item1, x.Item2))))
            db.Cities.Add(new City { CountryCode = city.Item1, CanonicalName = city.Item2, PersianName = city.Item3, EnglishName = city.Item4, GermanName = city.Item5, SearchAliases = city.Item6 });
    }

    private async Task SeedBusinessAsync()
    {
        var existingFeatureKeys = await db.ProductFeatureFlags.Select(x => x.Key).ToListAsync();
        foreach (var key in ProductFeatureKeys.All.Except(existingFeatureKeys))
            db.ProductFeatureFlags.Add(new ProductFeatureFlag { Key = key, IsEnabled = false });
        if (!await db.Languages.AnyAsync())
            db.Languages.AddRange(new[] { ("en","English","English","🇬🇧"),("de","German","Deutsch","🇩🇪"),("fr","French","Français","🇫🇷"),("es","Spanish","Español","🇪🇸"),("it","Italian","Italiano","🇮🇹"),("tr","Turkish","Türkçe","🇹🇷"),("nl","Dutch","Nederlands","🇳🇱"),("pt","Portuguese","Português","🇵🇹"),("ar","Arabic","العربية","🇸🇦"),("zh","Chinese","中文","🇨🇳") }.Select((x,i)=>new Language { Code=x.Item1,Name=x.Item2,NativeName=x.Item3,FlagEmoji=x.Item4,SortOrder=i+1 }));
        if (!await db.SubscriptionPlans.AnyAsync())
            db.SubscriptionPlans.AddRange(
                new SubscriptionPlan { Code="free",Name="رایگان",Description="تمام آموزش‌های آفلاین A1 تا C1 برای یک زبان",MonthlyPrice=0,YearlyPrice=0,Currency="TOMAN",LanguageLimit=1,MonthlyAiCredits=5 },
                new SubscriptionPlan { Code="premium",Name="Premium",Description="AI، چند زبان و آزمون‌های رسمی",MonthlyPrice=249000m,YearlyPrice=2490000m,Currency="TOMAN",LanguageLimit=10,MonthlyAiCredits=500,HasMockExams=true },
                new SubscriptionPlan { Code="premium-plus",Name="Premium Plus",Description="ظرفیت بالاتر AI و تحلیل پیشرفته",MonthlyPrice=499000m,YearlyPrice=4990000m,Currency="TOMAN",LanguageLimit=10,MonthlyAiCredits=2000,HasMockExams=true });
        if (!await db.AdPlacements.AnyAsync())
            db.AdPlacements.AddRange(
                new AdPlacement { Code="dashboard-banner",Name="بنر داشبورد",Page="dashboard",Position="between-sections",DailyPrice=20,Width=1200,Height=180 },
                new AdPlacement { Code="courses-card",Name="کارت میان دوره‌ها",Page="courses",Position="in-feed",DailyPrice=12,Width=600,Height=400 },
                new AdPlacement { Code="floating-bottom",Name="تبلیغ شناور پایین",Page="global",Position="floating-bottom",DailyPrice=30,Width=320,Height=250 });
        if (!await db.ContentItems.AnyAsync())
        {
            for (var i=1;i<=8;i++) db.ContentItems.Add(new ContentItem { ExternalId=$"news-{i}",Type="news",Slug=$"daily-germany-{i}",Title=$"خبر روز آلمان شماره {i}",Summary="خلاصه‌ای کوتاه از مهم‌ترین رویدادهای روز برای زبان‌آموزان و مهاجران.",Body="این متن نمونه از همان قرارداد JSON است که n8n می‌تواند هر روز به‌روزرسانی کند.",Category=i%2==0?"جامعه":"آلمان",Source="n8n Demo",PublishedAt=DateTimeOffset.UtcNow.AddHours(-i) });
            for (var i=1;i<=6;i++) db.ContentItems.Add(new ContentItem { ExternalId=$"blog-{i}",Type="blog",Slug=$"language-learning-{i}",Title=$"راهنمای یادگیری زبان شماره {i}",Summary="روش‌های عملی برای یادگیری پایدار و سریع‌تر زبان.",Body="مقاله نمونه وبلاگ. محتوای کامل از JSON خروجی n8n در این فیلد قرار می‌گیرد.",Category="یادگیری",Source="Unidemix Blog",PublishedAt=DateTimeOffset.UtcNow.AddDays(-i) });
        }
        await db.SaveChangesAsync();
    }

    private async Task SeedUsersAsync()
    {
        var people = new[]
        {
            ("demo@unidemix.local", "سارا احمدی", "A2", "daily-life", 20),
            ("ali@unidemix.local", "علی رضایی", "A1", "immigration", 15),
            ("mina@unidemix.local", "مینا کریمی", "B1", "work", 30),
            ("reza@unidemix.local", "رضا محمدی", "A2", "study", 20),
            ("nazanin@unidemix.local", "نازنین مرادی", "B2", "work", 45),
            ("amir@unidemix.local", "امیر حسینی", "A1", "daily-life", 10),
            ("leila@unidemix.local", "لیلا اکبری", "B1", "travel", 25),
            ("parsa@unidemix.local", "پارسا یوسفی", "A2", "exam", 30),
            ("niloofar@unidemix.local", "نیلوفر موسوی", "A1", "family", 15),
            ("arash@unidemix.local", "آرش شریفی", "B2", "university", 40),
            ("shirin@unidemix.local", "شیرین قاسمی", "A2", "immigration", 20),
            ("mehdi@unidemix.local", "مهدی نادری", "B1", "work", 30),
            ("yasaman@unidemix.local", "یاسمن جعفری", "A1", "travel", 15),
            ("pouya@unidemix.local", "پویا رستمی", "A2", "daily-life", 20),
            ("sahar@unidemix.local", "سحر کاظمی", "B1", "exam", 35),
            ("admin@unidemix.local", "مدیر یونیدمیکس", "C1", "administration", 30)
        };

        var existing = (await db.Users.Select(x => x.Email).ToListAsync()).ToHashSet();
        foreach (var person in people.Where(x => !existing.Contains(x.Item1)))
        {
            var user = new User
            {
                Email = person.Item1, DisplayName = person.Item2, PasswordHash = "", Level = person.Item3,
                Goal = person.Item4, DailyGoalMinutes = person.Item5,
                Role = person.Item1.StartsWith("admin") ? "Admin" : "User"
            };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, TestPassword);
            db.Users.Add(user);
        }
    }

    private async Task SeedCatalogAsync()
    {
        var catalogs = new[]
        {
            Catalog("german-for-real-life", "آلمانی برای زندگی واقعی", "موقعیت‌های روزمره از خرید تا مراجعه به پزشک", "A2", "روزمره", "🏠",
                "معرفی خود و دیگران", "خرید از سوپرمارکت", "پرسیدن مسیر در شهر", "قرار ملاقات با پزشک", "اجاره خانه", "کارهای بانکی", "اداره پست", "تماس تلفنی"),
            Catalog("german-starter", "شروع آلمانی از صفر", "پایه‌های ضروری برای زبان‌آموزان تازه‌کار", "A1", "مقدماتی", "🌱",
                "الفبا و تلفظ", "سلام و احوال‌پرسی", "اعداد و ساعت", "خانواده من", "رنگ‌ها و وسایل", "روزهای هفته", "غذا و نوشیدنی", "مرور سطح A1"),
            Catalog("german-at-work", "آلمانی در محیط کار", "ارتباط حرفه‌ای، جلسه و مکاتبه اداری", "B1", "کار", "💼",
                "روز اول کاری", "معرفی در تیم", "نوشتن ایمیل", "جلسه کاری", "مرخصی و بیماری", "ارائه کوتاه", "حل اختلاف", "مصاحبه شغلی"),
            Catalog("german-grammar", "گرامر کاربردی آلمانی", "گرامر همراه با مثال‌های قابل استفاده", "A2", "گرامر", "🧩",
                "افعال جداشدنی", "حالت Akkusativ", "حالت Dativ", "افعال Modal", "گذشته Perfekt", "جملات وابسته", "صفات و مقایسه", "مرور جامع گرامر"),
            Catalog("german-for-travel", "آلمانی برای سفر", "مکالمات فرودگاه، هتل و گردش در شهر", "A2", "سفر", "✈️",
                "در فرودگاه", "تحویل بار", "رزرو هتل", "در رستوران", "خرید بلیت", "مشکل در سفر", "جاذبه‌های گردشگری", "بازگشت به خانه"),
            Catalog("german-exam-b1", "آمادگی آزمون B1", "تمرین مهارت‌های اصلی و نمونه آزمون", "B1", "آزمون", "🎓",
                "درک مطلب ۱", "درک مطلب ۲", "شنیداری ۱", "شنیداری ۲", "نامه رسمی", "نامه دوستانه", "مکالمه آزمون", "آزمون آزمایشی کامل")
        };

        foreach (var catalog in catalogs)
        {
            var course = await db.Courses.Include(x => x.Lessons).SingleOrDefaultAsync(x => x.Slug == catalog.Slug);
            if (course is null)
            {
                course = new Course { Slug = catalog.Slug, Title = catalog.Title, Description = catalog.Description, LanguageCode = "de", Level = catalog.Level };
                db.Courses.Add(course);
            }
            var track = TrackMetadata(catalog.Slug);
            course.Kind = track.Kind;
            course.PathCode = track.PathCode;

            var usedOrders = course.Lessons.Select(x => x.Order).ToHashSet();
            for (var index = 0; index < catalog.Lessons.Length; index++)
            {
                var order = index + 1;
                if (usedOrders.Contains(order)) continue;
                var lesson = MakeLesson(catalog.Lessons[index], catalog.Category, order,
                    12 + (order * 2), 15 + (order * 5), catalog.Icon);
                lesson.Course = course;
                db.Lessons.Add(lesson);
            }
            foreach (var lesson in course.Lessons.Concat(db.Lessons.Local.Where(x => x.Course == course)))
            {
                lesson.SectionTitle = SectionTitle(catalog.Slug, lesson.Order);
                lesson.SectionOrderJson ??= JsonSerializer.Serialize(new[] { "vocabulary", "listening", "speaking", "reading", "writing", "grammar", "final-practice" });
            }
        }
    }

    private async Task SeedProgressAsync()
    {
        var users = await db.Users.OrderBy(x => x.Email).ToListAsync();
        var lessons = await db.Lessons.OrderBy(x => x.CourseId).ThenBy(x => x.Order).ToListAsync();
        var existing = (await db.LessonProgress.Select(x => new { x.UserId, x.LessonId }).ToListAsync())
            .Select(x => (x.UserId, x.LessonId)).ToHashSet();

        for (var userIndex = 0; userIndex < users.Count; userIndex++)
        {
            var activityCount = 4 + (userIndex % 18);
            for (var lessonIndex = 0; lessonIndex < Math.Min(activityCount, lessons.Count); lessonIndex++)
            {
                var key = (users[userIndex].Id, lessons[lessonIndex].Id);
                if (existing.Contains(key)) continue;
                var completed = lessonIndex < activityCount - 2;
                db.LessonProgress.Add(new LessonProgress
                {
                    UserId = key.Item1, LessonId = key.Item2,
                    Percent = completed ? 100 : 25 + ((userIndex + lessonIndex) % 3 * 25),
                    BestScore = completed ? 65 + ((userIndex * 7 + lessonIndex * 3) % 36) : 20 + ((userIndex + lessonIndex) % 45),
                    IsCompleted = completed,
                    UpdatedAt = DateTimeOffset.UtcNow.AddDays(-(userIndex + lessonIndex) % 21)
                });
            }
        }
    }

    private async Task SeedLearningExtensionsAsync()
    {
        var vocabulary = new[]
        {
            ("sich vorstellen", "/zɪç ˈfoːɐ̯ˌʃtɛlən/", "خود را معرفی کردن", "Ich möchte mich kurz vorstellen.", "می‌خواهم کوتاه خودم را معرفی کنم.", "فعل انعکاسی است: sich vorstellen"),
            ("Bescheid geben", "/bəˈʃaɪ̯t ˌɡeːbn̩/", "اطلاع دادن", "Bitte gib mir morgen Bescheid.", "لطفاً فردا به من اطلاع بده.", "یک ترکیب پرکاربرد با geben است."),
            ("sich verlaufen", "/zɪç fɛɐ̯ˈlaʊ̯fn̩/", "راه را گم کردن", "Ich habe mich in der Stadt verlaufen.", "من در شهر راه را گم کردم.", "فعل انعکاسی است: sich verlaufen"),
            ("Ich hätte gern ...", "/ɪç ˈhɛtə ɡɛʁn/", "مایلم ... / لطفاً ...", "Ich hätte gern einen Kaffee.", "لطفاً یک قهوه می‌خواهم.", "برای درخواست مؤدبانه استفاده می‌شود."),
            ("Es kommt darauf an.", "/ɛs kɔmt daˈʁaʊ̯f an/", "بستگی دارد.", "Es kommt auf die Situation an.", "به موقعیت بستگی دارد.", "فعل جداشدنی: auf etwas ankommen"),
            ("sich krankmelden", "/zɪç ˈkʁaŋkˌmɛldn̩/", "مرخصی استعلاجی را اطلاع دادن", "Ich muss mich heute krankmelden.", "امروز باید بیماری‌ام را به محل کار اطلاع بدهم.", "در موقعیت‌های کاری بسیار رایج است.")
        };

        var lessons = await db.Lessons.Include(x => x.Exercises).OrderBy(x => x.CourseId).ThenBy(x => x.Order).ToListAsync();
        var seededLessonIds = (await db.VocabularyItems.Select(x => x.LessonId).Distinct().ToListAsync()).ToHashSet();
        foreach (var lesson in lessons.Where(x => !seededLessonIds.Contains(x.Id)))
        {
            for (var offset = 0; offset < 3; offset++)
            {
                var item = vocabulary[(lesson.Order + offset - 1) % vocabulary.Length];
                db.VocabularyItems.Add(new VocabularyItem
                {
                    LessonId = lesson.Id, Term = item.Item1, ContentType = VocabularyType(item.Item1), Pronunciation = item.Item2, Meaning = item.Item3,
                    Example = item.Item4, ExampleTranslation = item.Item5, Note = item.Item6, Order = offset + 1
                });
            }
        }

        await EnsureExamProvider("de", "goethe", "Goethe-Institut", ["A1", "A2", "B1", "B2", "C1"]);
        await EnsureExamProvider("de", "telc", "telc", ["A1", "A2", "B1", "B2", "C1"]);
        await EnsureExamProvider("en", "ielts", "IELTS", ["B1", "B2", "C1"]);
        await EnsureExamProvider("en", "toefl", "TOEFL", ["B1", "B2", "C1"]);
    }

    private async Task EnsureExamProvider(string languageCode, string code, string name, string[] levels)
    {
        var provider = await db.ExamProviders.Include(x => x.Programs).ThenInclude(x => x.Sections)
            .Include(x => x.Programs).ThenInclude(x => x.LevelMappings)
            .SingleOrDefaultAsync(x => x.LanguageCode == languageCode && x.Code == code);
        if (provider is null)
        {
            provider = new ExamProvider { LanguageCode = languageCode, Code = code, Name = name };
            db.ExamProviders.Add(provider);
        }
        foreach (var level in levels)
        {
            var program = provider.Programs.SingleOrDefault(x => x.Level == level);
            if (program is null)
            {
                program = new ExamProgram { Level = level, Name = $"{name} {level}" };
                provider.Programs.Add(program);
            }
            program.Code = $"{code}-{level.ToLowerInvariant()}";
            if (!program.LevelMappings.Any(x => x.CefrLevel == level)) program.LevelMappings.Add(new ExamLevelMapping { CefrLevel = level });
            AddExamSection(program, "listening", languageCode == "de" ? "Hören / Listening" : "Listening", 1);
            AddExamSection(program, "reading", languageCode == "de" ? "Lesen / Reading" : "Reading", 2);
            AddExamSection(program, "writing", languageCode == "de" ? "Schreiben / Writing" : "Writing", 3);
            AddExamSection(program, "speaking", languageCode == "de" ? "Sprechen / Speaking" : "Speaking", 4);
        }
    }

    private static void AddExamSection(ExamProgram program, string code, string name, int order)
    {
        if (!program.Sections.Any(x => x.Code == code)) program.Sections.Add(new ExamSection { Code = code, Name = name, Order = order });
    }

    private async Task SeedSocialAsync()
    {
        var socialPeople = new[]
        {
            ("demo@unidemix.local", "fa", "de", "A2", "daily-life", "تمرین مکالمه روزمره", "عصرها", "برلین", true, "برای زندگی در آلمان، مکالمه روزمره تمرین می‌کنم."),
            ("ali@unidemix.local", "de", "fa", "A2", "daily-life", "مکالمه فارسی و آلمانی", "عصرها و آخر هفته", "هامبورگ", true, "آلمانی‌زبانم و دوست دارم فارسی را در گفت‌وگوی واقعی تمرین کنم."),
            ("mina@unidemix.local", "de", "fa", "B1", "work", "گفت‌وگوی کاری", "آخر هفته", "مونیخ", true, "به زبان و فرهنگ فارسی علاقه دارم و برای محیط کار تمرین می‌کنم."),
            ("reza@unidemix.local", "fa", "de", "A2", "study", "مکالمه و تلفظ", "عصرهای دوشنبه و چهارشنبه", "کلن", true, "دانشجوی زبان آلمانی هستم و روی تلفظ تمرکز دارم."),
            ("nazanin@unidemix.local", "de", "fa", "B2", "work", "اصطلاحات روزمره", "صبح آخر هفته", "فرانکفورت", true, "برای ارتباط با دوستان فارسی‌زبانم فارسی یاد می‌گیرم."),
            ("amir@unidemix.local", "fa", "de", "A1", "daily-life", "شروع مکالمه", "هر شب", "درسدن", true, "تازه آلمانی را شروع کرده‌ام و دنبال تمرین منظم هستم."),
            ("leila@unidemix.local", "de", "fa", "B1", "travel", "مکالمه سفر", "جمعه‌ها", "لایپزیگ", true, "برای سفر و آشنایی با فرهنگ ایران فارسی تمرین می‌کنم."),
            ("parsa@unidemix.local", "fa", "de", "B1", "exam", "آمادگی آزمون و مکالمه", "عصر و آخر هفته", "بن", true, "برای آزمون B1 آماده می‌شوم و پارتنر جدی می‌خواهم."),
            ("niloofar@unidemix.local", "de", "fa", "A2", "family", "گفت‌وگوی خانوادگی", "یکشنبه‌ها", "هانوفر", true, "می‌خواهم با خانواده فارسی‌زبانم روان‌تر صحبت کنم."),
            ("arash@unidemix.local", "fa", "de", "B2", "university", "بحث دانشگاهی", "بعدازظهرها", "برلین", false, "فعلاً برای تمرکز روی دانشگاه در discovery نمایش داده نمی‌شوم.")
        };

        var emails = socialPeople.Select(x => x.Item1).ToArray();
        var users = await db.Users.Where(x => emails.Contains(x.Email)).ToDictionaryAsync(x => x.Email);
        var existingIds = (await db.SocialProfiles.Select(x => x.UserId).ToListAsync()).ToHashSet();
        foreach (var person in socialPeople)
        {
            var user = users[person.Item1];
            user.NativeLanguage = person.Item2;
            user.LearningLanguage = person.Item3;
            user.Level = person.Item4;
            user.Goal = person.Item5;
            if (existingIds.Contains(user.Id)) continue;
            db.SocialProfiles.Add(new SocialProfile
            {
                UserId = user.Id,
                Bio = person.Item10,
                PracticeGoal = person.Item6,
                Availability = person.Item7,
                City = person.Item8,
                LookingForPartner = person.Item9
            });
        }
        await db.SaveChangesAsync();
        var cityByName = await db.Cities.ToDictionaryAsync(x => x.PersianName);
        var profiles = await db.SocialProfiles.Where(x => x.CityId == null && x.City != null).ToListAsync();
        foreach (var profile in profiles)
            if (profile.City is not null && cityByName.TryGetValue(profile.City, out var city)) profile.CityId = city.Id;
    }

    private async Task SeedMessagingAsync()
    {
        var users = await db.Users.Where(x => x.Email == "demo@unidemix.local" || x.Email == "ali@unidemix.local" || x.Email == "mina@unidemix.local")
            .ToDictionaryAsync(x => x.Email);
        var demo = users["demo@unidemix.local"];
        var ali = users["ali@unidemix.local"];
        var mina = users["mina@unidemix.local"];

        if (!await db.MessageRequests.AnyAsync(x => x.SenderId == ali.Id && x.RecipientId == demo.Id && x.Status == "Pending"))
        {
            db.MessageRequests.Add(new MessageRequest
            {
                SenderId = ali.Id,
                RecipientId = demo.Id,
                Introduction = "سلام، من فارسی A2 می‌خونم. خوشحال می‌شم باهم آلمانی و فارسی تمرین کنیم."
            });
            db.Notifications.Add(new Notification
            {
                UserId = demo.Id, ActorId = ali.Id, Type = "MessageRequest",
                Text = $"{ali.DisplayName} درخواست گفتگو فرستاد.", Destination = "/social/messages?tab=requests"
            });
        }

        var one = demo.Id.CompareTo(mina.Id) < 0 ? demo.Id : mina.Id;
        var two = demo.Id.CompareTo(mina.Id) < 0 ? mina.Id : demo.Id;
        var conversation = await db.Conversations.Include(x => x.Messages).SingleOrDefaultAsync(x => x.UserOneId == one && x.UserTwoId == two);
        if (conversation is null)
        {
            conversation = new Conversation { UserOneId = one, UserTwoId = two };
            conversation.Messages.Add(new ChatMessage
            {
                SenderId = mina.Id,
                Text = "Hallo! دوست داری این هفته یک مکالمه کوتاه آلمانی تمرین کنیم؟"
            });
            db.Conversations.Add(conversation);
            db.Notifications.Add(new Notification
            {
                UserId = demo.Id, ActorId = mina.Id, Type = "NewMessage",
                Text = $"پیام جدید از {mina.DisplayName}", Destination = $"/social/messages/{conversation.Id}"
            });
        }
    }

    private async Task SeedCommunityAsync()
    {
        const string seedPrefix = "https://placehold.co/900x900/e8f5e9/166534?text=Unidemix+";
        if (await db.SocialPosts.AnyAsync(x => x.ImageUrl.StartsWith(seedPrefix))) return;

        var emails = new[] { "demo@unidemix.local", "ali@unidemix.local", "mina@unidemix.local", "reza@unidemix.local", "leila@unidemix.local" };
        var users = await db.Users.Where(x => emails.Contains(x.Email)).ToDictionaryAsync(x => x.Email);
        var demo = users[emails[0]]; var ali = users[emails[1]]; var mina = users[emails[2]]; var reza = users[emails[3]]; var leila = users[emails[4]];
        var posts = new[]
        {
            new SocialPost { UserId = demo.Id, ImageUrl = seedPrefix + "Study+Desk", Caption = "امروز واژگان درس A2 را مرور کردم. @ali تو برای مرور لغت چه روشی داری؟", Context = "StudyToday", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) },
            new SocialPost { UserId = mina.Id, ImageUrl = seedPrefix + "Coffee+German", Caption = "Eine kleine Kaffeepause mit meinem Persisch-Heft.", Context = "LearningMoment", CreatedAt = DateTimeOffset.UtcNow.AddHours(-16) },
            new SocialPost { UserId = reza.Id, ImageUrl = seedPrefix + "Notebook", Caption = "تمرین تلفظ امروز تمام شد؛ قدم‌های کوچک اما پیوسته.", Context = "Achievement", CreatedAt = DateTimeOffset.UtcNow.AddHours(-8) },
            new SocialPost { UserId = leila.Id, ImageUrl = seedPrefix + "Language+Class", Caption = "Heute haben wir Reisevokabeln geübt.", Context = "StudyToday", CreatedAt = DateTimeOffset.UtcNow.AddHours(-3) }
        };
        db.SocialPosts.AddRange(posts); await db.SaveChangesAsync();
        db.PostLikes.AddRange(new PostLike { UserId = ali.Id, PostId = posts[0].Id }, new PostLike { UserId = demo.Id, PostId = posts[1].Id }, new PostLike { UserId = leila.Id, PostId = posts[2].Id });
        var comment = new PostComment { UserId = ali.Id, PostId = posts[0].Id, Text = "من با فلش‌کارت تمرین می‌کنم @demo" };
        db.PostComments.AddRange(comment, new PostComment { UserId = demo.Id, PostId = posts[1].Id, Text = "ترکیب قهوه و تمرین زبان عالی است!" });
        db.ContentMentions.AddRange(
            new ContentMention { MentionedUserId = ali.Id, PostId = posts[0].Id },
            new ContentMention { MentionedUserId = demo.Id, Comment = comment });
    }

    private static Lesson MakeLesson(string title, string category, int order, int duration, int xp, string icon)
    {
        var lesson = new Lesson
        {
            Title = title, Description = $"آموزش و تمرین کاربردی: {title}", Category = category,
            Order = order, DurationMinutes = duration, XpReward = xp, Icon = icon
        };
        lesson.Exercises =
        [
            Exercise("multiple-choice", $"گزینه درست را برای «{title}» انتخاب کنید.", "richtig", order * 10 + 1,
                ["richtig", "falsch", "vielleicht"], "به ساختار جمله و جایگاه فعل توجه کنید."),
            Exercise("translation", $"ترجمه کنید: این تمرین درباره {title} است.", $"Diese Übung handelt von {title}.", order * 10 + 2),
            Exercise("fill-blank", "Ich ___ heute Deutsch.", "lerne", order * 10 + 3,
                ["lerne", "lernst", "lernt"], "برای فاعل ich، فعل lernen به صورت lerne صرف می‌شود."),
            Exercise("listening", "عبارت شنیده‌شده را بنویسید: Guten Morgen!", "Guten Morgen!", order * 10 + 4)
        ];
        return lesson;
    }

    private static Exercise Exercise(string type, string prompt, string answer, int order, string[]? options = null, string? explanation = null) =>
        new() { Type = type, SectionCode = ExerciseSection(type), Prompt = prompt, CorrectAnswer = answer, Order = order, OptionsJson = options is null ? null : JsonSerializer.Serialize(options), Explanation = explanation };

    private static string ExerciseSection(string type) => type switch
    {
        "listening" => "listening",
        "translation" => "writing",
        _ => "grammar"
    };

    private static CourseCatalog Catalog(string slug, string title, string description, string level, string category, string icon, params string[] lessons) =>
        new(slug, title, description, level, category, icon, lessons);

    private sealed record CourseCatalog(string Slug, string Title, string Description, string Level, string Category, string Icon, string[] Lessons);

    private static string? SectionTitle(string slug, int order) => slug switch
    {
        "german-at-work" when order <= 2 => "شروع کار",
        "german-at-work" when order <= 4 => "ارتباط کاری",
        "german-at-work" when order <= 6 => "جلسات",
        "german-at-work" => "مسیر شغلی",
        "german-for-travel" when order <= 2 => "رفت‌وآمد",
        "german-for-travel" when order <= 5 => "اقامت و تجربه سفر",
        "german-for-travel" => "حل مسئله در سفر",
        _ => null
    };

    private static (string Kind, string? PathCode) TrackMetadata(string slug) => slug switch
    {
        "german-starter" => ("Core", null),
        "german-for-real-life" => ("Core", null),
        "german-at-work" => ("Specialized", "work"),
        "german-for-travel" => ("Specialized", "travel"),
        "german-exam-b1" => ("Specialized", "exam"),
        "german-grammar" => ("Specialized", "grammar"),
        _ => ("Core", null)
    };

    private static string VocabularyType(string term) => term.Contains(' ') || term.Contains("...")
        ? (term.EndsWith('.') ? "Expression" : "Chunk")
        : "Word";
}
