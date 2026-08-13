using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Services;

public sealed class ExamBlueprintSeeder(AppDbContext db)
{
    private const string GoetheRoot = "https://www.goethe.de/pro/relaunch/prf/materialien";

    public async Task SeedAsync()
    {
        var programs = await db.ExamPrograms.Include(x => x.Provider).Include(x => x.Blueprints).Include(x => x.Sections).ToListAsync();
        foreach (var program in programs.Where(x => x.Provider.Code == "telc" && x.IsPublished && x.BlueprintJson is not null))
            Upsert(program, FromTelc(program));
        foreach (var level in new[] { "A1", "A2", "B1", "B2", "C1" })
        {
            var program = programs.Single(x => x.Provider.Code == "goethe" && x.Level == level);
            program.Code = $"goethe-zertifikat-{level.ToLowerInvariant()}";
            program.Name = $"Goethe-Zertifikat {level}";
            program.IsPublished = true;
            Upsert(program, Goethe(level));
        }
        await db.SaveChangesAsync();
    }

    private void Upsert(ExamProgram program, ExamBlueprintDocument document)
    {
        var entity = program.Blueprints.SingleOrDefault(x => x.Version == document.Version && x.Variant == document.Variant);
        if (entity is null)
        {
            entity = new ExamBlueprint { ExamProgramId = program.Id, ProviderCode = document.Provider, ExamKey = document.ExamKey,
                Variant = document.Variant, Cefr = document.Cefr, Version = document.Version,
                SourceReferencesJson = "[]", DefinitionJson = "{}", ValidFrom = document.ValidFrom };
            db.ExamBlueprints.Add(entity);
        }
        entity.Status = document.Status;
        entity.SourceReferencesJson = JsonSerializer.Serialize(document.SourceReferences);
        entity.DefinitionJson = JsonSerializer.Serialize(document);
        if (document.Provider == "goethe")
        {
            program.ContentVersion = document.Version;
            program.BlueprintJson = JsonSerializer.Serialize(document.Sections.Select(s => new ExamSectionSource(s.Key,s.Name,s.Order,
                s.Parts.Select(p => new ExamPartSource(p.Key,p.Key,p.TaskType,p.ItemCount)).ToArray())));
            program.PracticeBankJson ??= "[]";
            program.MockVariantsJson ??= "[]";
            program.PracticeBankJson = JsonSerializer.Serialize(BuildGoetheTasks(document, false));
            program.MockVariantsJson = JsonSerializer.Serialize(new[] {
                new MockVariantSource($"goethe-{document.Cefr.ToLowerInvariant()}-mock-1", $"Goethe {document.Cefr} Mock 1", BuildGoetheTasks(document, true, "m1")),
                new MockVariantSource($"goethe-{document.Cefr.ToLowerInvariant()}-mock-2", $"Goethe {document.Cefr} Mock 2", BuildGoetheTasks(document, true, "m2")) });
            program.ScoringJson = JsonSerializer.Serialize(new ExamScoringSource(document.Scoring.MaximumPoints,document.Scoring.PassPoints,false,document.Scoring.Rule));
            program.WrittenDurationMinutes = document.Sections.Where(x => x.Key != "speaking").Sum(x => x.TimingMinutes);
            program.SpeakingDurationMinutes = document.Sections.Single(x => x.Key == "speaking").TimingMinutes;
            program.PreparationDurationMinutes = document.Cefr == "C1" ? 20 : document.Cefr is "B1" or "B2" ? 15 : 0;
            program.SourceReference = document.SourceReferences[0];
            foreach (var section in document.Sections)
            {
                var stored = program.Sections.SingleOrDefault(x => x.Code == section.Key);
                if (stored is null) { stored = new ExamSection { ExamProgramId = program.Id, Code = section.Key, Name = section.Name }; program.Sections.Add(stored); }
                stored.Name = section.Name; stored.Order = section.Order;
            }
        }
    }

    private static ExamTaskSource[] BuildGoetheTasks(ExamBlueprintDocument document, bool fullMock, string variant = "practice")
    {
        var tasks = new List<ExamTaskSource>();
        foreach (var section in document.Sections.OrderBy(x => x.Order))
        foreach (var part in section.Parts.OrderBy(x => x.Order))
        {
            var count = fullMock && part.ResponseType != "productive" ? part.ItemCount : fullMock ? 1 : 2;
            for (var index = 1; index <= count; index++)
            {
                var key = $"goethe-{document.Cefr.ToLowerInvariant()}-{variant}-{section.Key}-{part.Key}-{index}";
                var productive = part.ResponseType == "productive";
                var listening = section.Key == "listening";
                var content = GoetheContent(document.Cefr, section.Key, part.TaskType, part.Order, index, variant);
                var options = productive ? null : part.OptionCount switch
                {
                    2 => new[] { "Richtig", "Falsch" },
                    4 => content.Options.Concat(["Keine der Antworten"]).Take(4).ToArray(),
                    _ => content.Options.Take(3).ToArray()
                };
                var prompt = content.Prompt;
                tasks.Add(new(key, section.Key, part.Key, part.TaskType, prompt,
                    listening || productive ? null : content.Source,
                    listening ? content.Source : null,
                    listening ? $"de-DE; Goethe {document.Cefr}; Teil {part.Order}" : null,
                    listening ? 35 : null, part.PlaybackRule?.Plays, listening ? "script-ready-audio-deferred" : null,
                    productive ? null : part.OptionCount == 2 ? (content.IsTrue ? "Richtig" : "Falsch") : options![0], options, productive ? 0 : 1, productive,
                    productive ? null : content.Explanation));
            }
        }
        var result = tasks.ToArray();
        if (fullMock) ExamContentQualityValidator.ValidateGermanMock(result);
        return result;
    }

    private static bool ContainsLegacyPlaceholder(string json) =>
        json.Contains("Originaler Unidemix-", StringComparison.Ordinal) ||
        json.Contains("originaler Unidemix-H", StringComparison.Ordinal) ||
        json.Contains("متن اصلی Unidemix", StringComparison.Ordinal);

    private static GoetheTaskContent GoetheContent(string cefr, string section, string taskType, int part, int index, string variant)
    {
        if (section == "writing")
        {
            var writingPrompt = (part + index) % 2 == 0
                ? "Ihr deutschsprachiger Freund schlägt ein Treffen am Wochenende vor. Schreiben Sie eine passende Antwort: Reagieren Sie auf die vorgeschlagene Zeit, machen Sie einen Gegenvorschlag und verwenden Sie eine passende Anrede und einen passenden Schluss."
                : "Schreiben Sie eine E-Mail an die Leitung eines Sprachkurses. Nennen Sie den Grund Ihrer Nachricht, Ihre möglichen Zeiten und stellen Sie eine konkrete Frage zum Kurs.";
            return new(writingPrompt, "", [], true, "");
        }
        if (section == "speaking")
        {
            var speakingPrompt = (part + index) % 2 == 0
                ? "Planen Sie gemeinsam eine Gruppenaktivität. Machen Sie einen Vorschlag, reagieren Sie auf Ihren Gesprächspartner und einigen Sie sich auf Zeit und Ort."
                : "Sprechen Sie kurz über das Thema ‚Sprachenlernen im Alltag‘. Berichten Sie von einer persönlichen Erfahrung und beantworten Sie eine Frage Ihres Gesprächspartners.";
            return new(speakingPrompt, "", [], true, "");
        }

        var samples = cefr switch
        {
            "A1" => new[] {
                new GoetheSample("Hallo Mina, der Deutschkurs beginnt heute um 18 Uhr in Raum 4. Bitte bring dein Kursbuch mit.", "Der Kurs beginnt um 18 Uhr.", "Wann beginnt der Kurs?", new[] { "Um 18 Uhr", "Um 16 Uhr", "Um 20 Uhr" }),
                new GoetheSample("Die Stadtbibliothek ist am Montag geschlossen. Am Dienstag öffnet sie wieder um 10 Uhr.", "Die Bibliothek ist am Montag geöffnet.", "Wann öffnet die Bibliothek wieder?", new[] { "Am Dienstag um 10 Uhr", "Am Montag um 10 Uhr", "Am Mittwoch um 8 Uhr" }) },
            "A2" => new[] {
                new GoetheSample("Liebe Nachbarn, wegen des Regens findet das Hoffest nicht am Samstag, sondern am Sonntag ab 15 Uhr im Gemeinschaftsraum statt. Getränke sind da; bitte bringen Sie etwas zu essen mit.", "Das Hoffest wurde auf Sonntag verschoben.", "Was sollen die Gäste mitbringen?", new[] { "Etwas zu essen", "Getränke", "Stühle" }),
                new GoetheSample("Der Regionalzug nach Mainz fährt heute von Gleis 7 statt von Gleis 4. Die Abfahrt bleibt um 16.25 Uhr. Reisende nach Wiesbaden steigen bitte in Mainz um.", "Die Abfahrtszeit des Zuges ändert sich.", "Von welchem Gleis fährt der Zug?", new[] { "Von Gleis 7", "Von Gleis 4", "Von Gleis 16" }) },
            "B1" => new[] {
                new GoetheSample("Im Stadtteilzentrum startet im September eine Reparaturwerkstatt. Ehrenamtliche helfen dabei, kleine Haushaltsgeräte und Fahrräder wieder nutzbar zu machen. Ersatzteile müssen die Besucher selbst bezahlen, die Beratung ist kostenlos. Eine Anmeldung ist nur für Fahrräder nötig.", "Für die Fahrradreparatur muss man sich vorher anmelden.", "Welche Leistung ist kostenlos?", new[] { "Die Beratung", "Alle Ersatzteile", "Ein neues Fahrrad" }),
                new GoetheSample("Viele Beschäftigte der Firma nutzen inzwischen ein digitales Weiterbildungskonto. Sie können selbst Kurse auswählen, müssen die Teilnahme aber vorher mit ihrer Teamleitung abstimmen. Die Firma übernimmt die Kosten, wenn der Kurs einen erkennbaren Bezug zur Arbeit hat.", "Jeder beliebige Kurs wird ohne Rücksprache bezahlt.", "Wann übernimmt die Firma die Kosten?", new[] { "Bei beruflichem Bezug", "Nur am Wochenende", "Bei privatem Interesse" }) },
            "B2" => new[] {
                new GoetheSample("Mehrere Städte testen sogenannte Superblocks, in denen der Durchgangsverkehr eingeschränkt wird. Erste Erhebungen zeigen weniger Lärm und mehr Aufenthaltsqualität. Gewerbetreibende befürchten jedoch Nachteile bei Lieferungen. Die Verwaltung will deshalb Ladezonen einrichten und die Auswirkungen über ein Jahr hinweg auswerten.", "Die Verwaltung ignoriert die Bedenken der Gewerbetreibenden.", "Welchen Ausgleich plant die Verwaltung?", new[] { "Besondere Ladezonen", "Kostenlose Parkhäuser", "Neue Umgehungsstraßen" }),
                new GoetheSample("Eine Hochschule lässt Studierende Teile des Lehrplans mitgestalten. Befürworter sehen darin eine Chance, Seminare näher an aktuellen Fragen auszurichten. Kritiker warnen, kurzfristige Interessen könnten grundlegende Inhalte verdrängen. Nach der Pilotphase soll deshalb nicht nur die Zufriedenheit, sondern auch der Lernerfolg untersucht werden.", "Die Hochschule bewertet ausschließlich die Zufriedenheit.", "Was wird nach der Pilotphase zusätzlich geprüft?", new[] { "Der Lernerfolg", "Die Gebäudegröße", "Die Zahl der Parkplätze" }) },
            _ => new[] {
                new GoetheSample("Die Debatte über verkürzte Arbeitswochen wird häufig auf Produktivitätskennzahlen reduziert. Dabei zeigen Feldstudien, dass die Wirkung wesentlich davon abhängt, ob Prozesse tatsächlich neu organisiert oder dieselben Aufgaben lediglich verdichtet werden. Nachhaltige Modelle verbinden Arbeitszeitreduktion daher mit klaren Prioritäten und größerer Entscheidungskompetenz der Teams.", "Eine kürzere Arbeitswoche wirkt unabhängig von der Arbeitsorganisation.", "Welche Bedingung nennt der Text für nachhaltige Modelle?", new[] { "Neu gesetzte Prioritäten", "Mehr Einzelkontrolle", "Unveränderte Abläufe" }),
                new GoetheSample("Kommunale Beteiligungsverfahren erreichen oft jene Gruppen, die ohnehin politisch aktiv sind. Digitale Plattformen erweitern zwar den Zugang, beseitigen aber weder sprachliche Hürden noch ungleiche zeitliche Ressourcen. Fachleute empfehlen deshalb eine Kombination aus Online-Angeboten, aufsuchender Beratung und transparentem Umgang mit den Ergebnissen.", "Digitale Plattformen lösen sämtliche Zugangsprobleme.", "Welche Kombination wird empfohlen?", new[] { "Online-Angebote und direkte Beratung", "Nur schriftliche Umfragen", "Ausschließlich Bürgerversammlungen" }) }
        };
        var sample = samples[(part + index + (variant == "m2" ? 1 : 0)) % samples.Length];
        var trueFalse = taskType is "true-false" or "yes-no";
        var matching = taskType.Contains("matching", StringComparison.Ordinal);
        var instruction = section == "listening"
            ? trueFalse ? "Hören Sie den Text. Ist die Aussage richtig oder falsch?" : "Hören Sie den Text und wählen Sie die richtige Antwort."
            : trueFalse ? "Lesen Sie den Text. Ist die Aussage richtig oder falsch?"
            : matching ? "Lesen Sie den Text und wählen Sie die passende Lösung."
            : "Lesen Sie den Text und wählen Sie die richtige Antwort.";
        var prompt = $"{instruction}\n\n{(trueFalse ? sample.Statement : sample.Question)}";
        var isTrue = !sample.Statement.Contains("nicht", StringComparison.OrdinalIgnoreCase) &&
                     !sample.Statement.Contains("ohne", StringComparison.OrdinalIgnoreCase) &&
                     !sample.Statement.Contains("ausschließlich", StringComparison.OrdinalIgnoreCase) &&
                     !sample.Statement.Contains("unabhängig", StringComparison.OrdinalIgnoreCase) &&
                     !sample.Statement.Contains("sämtliche", StringComparison.OrdinalIgnoreCase) &&
                     !sample.Statement.Contains("ignoriert", StringComparison.OrdinalIgnoreCase) &&
                     !sample.Statement.Contains("beliebige", StringComparison.OrdinalIgnoreCase) &&
                     !sample.Statement.Contains("ändert sich", StringComparison.OrdinalIgnoreCase) &&
                     !sample.Statement.Contains("geöffnet", StringComparison.OrdinalIgnoreCase);
        return new(prompt, sample.Source, sample.Options, isTrue,
            trueFalse ? "این جمله را با اطلاعات صریح متن مقایسه کنید." : "پاسخ درست مستقیماً از اطلاعات متن به دست می‌آید.");
    }

    private sealed record GoetheSample(string Source, string Statement, string Question, string[] Options);
    private sealed record GoetheTaskContent(string Prompt, string Source, string[] Options, bool IsTrue, string Explanation);

    public static ExamBlueprintDocument FromTelc(ExamProgram p)
    {
        var source = JsonSerializer.Deserialize<ExamSectionSource[]>(p.BlueprintJson!)!;
        var sections = source.Select(s => new ExamSectionBlueprint(s.Code, s.Name, s.Order, SectionMinutes(p, s.Code),
            s.Parts.Select((part, index) => Part(part.Key, index + 1, part.TaskType, part.ItemCount,
                s.Code == "listening" ? ListeningPlays(p.Level, index + 1) : null,
                part.TaskType is "writing" or "speaking" ? 1 : 0)).ToArray())).ToArray();
        return new("telc", $"telc-deutsch-{p.Level.ToLowerInvariant()}", p.Name, "de", p.Level, "adults", p.ContentVersion,
            "Validated", DateTimeOffset.Parse("2026-08-12T00:00:00Z"), [p.SourceReference!], ApplyDifficulty(sections,p.Level),
            new(DeserializeScoring(p).MaxPoints, DeserializeScoring(p).PassPoints, true, DeserializeScoring(p).ResultLabel, sections.Select(x => x.Key).ToArray()));
    }

    public static ExamBlueprintDocument Goethe(string level)
    {
        var (version, source, sections, score) = level switch
        {
            "A1" => (1, $"{GoetheRoot}/A1_sd1/sd_1_modellsatz.pdf", new[] {
                Section("listening","Hören",1,20,("h1","multiple-choice",6,2),("h2","true-false",4,1),("h3","multiple-choice",5,2)),
                Section("reading","Lesen",2,25,("r1","true-false",5,0),("r2","matching",5,0),("r3","multiple-choice",5,0)),
                Productive("writing","Schreiben",3,20,2,3), Productive("speaking","Sprechen",4,15,3,1) },
                new ExamScoringBlueprint(100,60,true,"Goethe A1 overall scoring and productive rubrics are blueprint-owned.",["listening","reading","writing","speaking"])),
            "A2" => (5, $"{GoetheRoot}/A2/A2_Modellsatz_Erwachsene.pdf", new[] {
                Section("reading","Lesen",1,30,("r1","multiple-choice",5,0),("r2","multiple-choice",5,0),("r3","multiple-choice",5,0),("r4","matching",5,0)),
                Section("listening","Hören",2,30,("h1","multiple-choice",5,2),("h2","matching",5,1),("h3","multiple-choice",5,1),("h4","true-false",5,2)),
                Productive("writing","Schreiben",3,30,2,3), Productive("speaking","Sprechen",4,15,3,1) },
                new ExamScoringBlueprint(100,60,true,"25 points per skill; written minimum 45/75 and speaking minimum 15/25.",["reading","listening","writing","speaking"])),
            "B1" => (11, $"{GoetheRoot}/B1/b1_modellsatz_erwachsene.pdf", new[] {
                Section("reading","Lesen",1,65,("r1","true-false",6,0),("r2","multiple-choice",6,0),("r3","matching",7,0),("r4","yes-no",7,0),("r5","multiple-choice",4,0)),
                Section("listening","Hören",2,40,("h1","mixed-objective",10,2),("h2","multiple-choice",5,1),("h3","multiple-choice",7,1),("h4","true-false",8,2)),
                Productive("writing","Schreiben",3,60,3,4), Productive("speaking","Sprechen",4,15,3,4) },
                ModularScore(["reading","listening","writing","speaking"])),
            "B2" => (15, $"{GoetheRoot}/B2/b2_modellsatz_erwachsene.pdf", new[] {
                Section("reading","Lesen",1,65,("r1","matching",9,0),("r2","matching",6,0),("r3","multiple-choice",6,0),("r4","matching",6,0),("r5","matching",3,0)),
                Section("listening","Hören",2,40,("h1","multiple-choice",5,1),("h2","multiple-choice",5,2),("h3","matching",6,1),("h4","multiple-choice",8,2)),
                Productive("writing","Schreiben",3,75,2,4), Productive("speaking","Sprechen",4,15,2,1) },
                ModularScore(["reading","listening","writing","speaking"])),
            _ => (2, $"{GoetheRoot}/C1_modular/c1-modular_modellsatz.pdf", new[] {
                Section("reading","Lesen",1,65,("r1","multiple-choice-cloze",8,0),("r2","multiple-choice",7,0),("r3","sentence-matching",8,0),("r4","matching",7,0)),
                Section("listening","Hören",2,40,("h1","matching",6,1),("h2","multiple-choice",9,2),("h3","multiple-choice",8,1),("h4","multiple-choice",7,2)),
                Productive("writing","Schreiben",3,75,2,4), Productive("speaking","Sprechen",4,20,2,4) },
                ModularScore(["reading","listening","writing","speaking"]))
        };
        return new("goethe", $"goethe-zertifikat-{level.ToLowerInvariant()}", $"Goethe-Zertifikat {level}", "de", level,
            "adults", version, "Validated", DateTimeOffset.Parse("2026-08-12T00:00:00Z"), [source], ApplyDifficulty(sections,level), score,
            new() { ["disclaimer"] = "Unidemix practice based on the validated exam structure; not an official Goethe-Institut exam." });
    }

    private static ExamScoringBlueprint ModularScore(string[] order) => new(100,60,true,"Each module is scored out of 100; pass threshold is 60 points.",order);
    private static ExamSectionBlueprint Section(string key,string name,int order,int minutes,params (string Key,string Type,int Count,int Plays)[] parts) =>
        new(key,name,order,minutes,parts.Select((x,i)=>Part(x.Key,i+1,x.Type,x.Count,x.Plays==0?null:x.Plays,0)).ToArray());
    private static ExamSectionBlueprint Productive(string key,string name,int order,int minutes,int count,int promptPoints) =>
        new(key,name,order,minutes,Enumerable.Range(1,count).Select(i=>Part($"{key[0]}{i}",i,key,i==1?1:1,null,promptPoints)).ToArray());
    private static ExamPartBlueprint Part(string key,int order,string type,int itemCount,int? plays,int promptPoints) =>
        new(key,order,type,itemCount,"Follow the exact provider part format.",type is "writing" or "speaking" ? "productive" : "objective",
            plays is null ? null : new(plays.Value,1,null,null),null,"Blueprint-owned scoring",OptionCount(type),
            new(type is "writing" or "speaking" ? 20 : 10, type is "writing" or "speaking" ? 2500 : 4000,promptPoints,
                type is "writing" ? "blueprint-defined" : "neutral",["change provider","change task type","copy official source"]),
            Difficulty("B1",type), Slots(key,type,itemCount,plays is not null));
    private static int OptionCount(string type) => type switch { "multiple-choice" or "mixed-objective" => 3, "multiple-choice-cloze" => 4, "true-false" or "yes-no" => 2, _ => 0 };
    private static ExamDifficultyProfile Difficulty(string cefr,string type) => new(cefr switch { "A1"=>15,"A2"=>20,"B1"=>28,"B2"=>36,_=>50 },$"{cefr}-controlled",cefr switch { "A1"=>"direct","A2"=>"mostly-direct","B1"=>"limited-inference","B2"=>"argument-and-stance",_=>"nuance-and-implicit-meaning" },
        type.Contains("multiple-choice") ? "plausible-near-neighbor" : "blueprint-defined",0,type is "writing" ? 250 : 0,
        type is "writing" ? "blueprint-defined" : "neutral");
    private static ExamSectionBlueprint[] ApplyDifficulty(ExamSectionBlueprint[] sections,string cefr) => sections.Select(section =>
        section with { Parts = section.Parts.Select(part => part with { DifficultyProfile = Difficulty(cefr,part.TaskType) }).ToArray() }).ToArray();
    private static ExamContentSlotBlueprint[] Slots(string partKey,string type,int itemCount,bool listening)
    {
        var slots = new List<ExamContentSlotBlueprint>();
        if (listening) slots.Add(new($"{partKey}:listening-script","listening-script",null,true,10,4000));
        else if (type is not "writing" and not "speaking") slots.Add(new($"{partKey}:reading-text","reading-text",null,true,10,4000));
        if (type is "writing" or "speaking") slots.Add(new($"{partKey}:scenario","scenario",null,true,20,2500));
        else for (var item = 1; item <= itemCount; item++) slots.Add(new($"{partKey}:question:{item}","question",item,true,1,500));
        return slots.ToArray();
    }
    private static int SectionMinutes(ExamProgram p,string section) => section switch { "speaking" => p.SpeakingDurationMinutes, _ => Math.Max(1,p.WrittenDurationMinutes / Math.Max(1,JsonSerializer.Deserialize<ExamSectionSource[]>(p.BlueprintJson!)!.Count(x=>x.Code!="speaking"))) };
    private static int? ListeningPlays(string level,int part) => level switch { "A1" => part == 2 ? 1 : 2, "B1" => part is 2 or 3 ? 1 : 2, "B2" => part is 1 or 3 ? 1 : 2, _ => 2 };
    private static ExamScoringSource DeserializeScoring(ExamProgram p) => JsonSerializer.Deserialize<ExamScoringSource>(p.ScoringJson!)!;
}
