using System.Text.Json;
using Unidemix.Api.Models;
using Unidemix.Api.Services;

namespace Unidemix.Api.Tests;

public sealed class ExamGenerationValidatorTests
{
    private readonly ExamGenerationValidator validator = new();
    private readonly ExamTemplateAssembler assembler = new();

    [Theory]
    [InlineData("telc","A1")]
    [InlineData("telc","A2")]
    [InlineData("telc","B1")]
    [InlineData("telc","B2")]
    [InlineData("goethe","A1")]
    [InlineData("goethe","A2")]
    [InlineData("goethe","B1")]
    [InlineData("goethe","B2")]
    [InlineData("goethe","C1")]
    public void Replacement_variant_matches_exact_fixed_template_and_is_original(string provider, string cefr)
    {
        var blueprint = LoadBlueprint(provider,cefr);
        var contract = assembler.Contract(blueprint,"FullMockExam",null,null,[]);
        var generated = assembler.Assemble(blueprint,contract,ReplacementContent(contract,$"original-{provider}-{cefr}"));
        var result = validator.Validate(blueprint,generated,"FullMockExam",null,null,[]);

        Assert.True(result.IsValid,string.Join(';',result.Issues.Select(x=>x.Code)));
        Assert.Equal(ExamGenerationValidator.StructuralSignature(blueprint,"FullMockExam",null,null),ExamGenerationValidator.StructuralSignature(generated));
        Assert.DoesNotContain(blueprint.SourceReferences[0],JsonSerializer.Serialize(generated));
    }

    [Fact]
    public void Provider_receives_only_fixed_contract_and_returns_content_slots()
    {
        var blueprint=ExamBlueprintSeeder.Goethe("B1");
        var contract=assembler.Contract(blueprint,"PartPractice","reading","r2",[]);
        var replacement=ReplacementContent(contract,"fresh");
        Assert.Single(contract.Parts);
        Assert.All(replacement.Replacements,x=>Assert.Contains(contract.Parts[0].ContentSlots,s=>s.Key==x.SlotKey));
        Assert.DoesNotContain(typeof(GeneratedContentReplacement).GetProperties(),x=>x.Name is "TaskType" or "ItemCount" or "PlaybackCount" or "ScoringRule");
    }

    public static IEnumerable<object[]> HardFailures()
    {
        yield return ["provider",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Provider="goethe"})];
        yield return ["cefr",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Cefr="B2"})];
        yield return ["part",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Tasks=[x.Tasks[0] with{Part="wrong"}]})];
        yield return ["task-type",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Tasks=[x.Tasks[0] with{Type="true-false"}]})];
        yield return ["item-count",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Tasks=[x.Tasks[0] with{ItemCount=2}]})];
        yield return ["question-count",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Tasks=[x.Tasks[0] with{Questions=[]}]})];
        yield return ["option-count",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Tasks=[x.Tasks[0] with{OptionCount=4}]})];
        yield return ["response-type",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Tasks=[x.Tasks[0] with{ResponseType="productive"}]})];
        yield return ["playback",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Tasks=[x.Tasks[0] with{PlaybackCount=2}]})];
        yield return ["order-timing",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Tasks=[x.Tasks[0] with{PartOrder=9}]})];
        yield return ["scoring",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Tasks=[x.Tasks[0] with{ScoringRule="changed"}]})];
        yield return ["duplicate-options",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Tasks=[x.Tasks[0] with{Questions=[x.Tasks[0].Questions[0] with{Options=["a","a","c"]}]}]})];
        yield return ["answer",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Tasks=[x.Tasks[0] with{Questions=[x.Tasks[0].Questions[0] with{CorrectAnswer=null}]}]})];
        yield return ["language-leakage",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Tasks=[x.Tasks[0] with{Prompt="Click the correct answer."}]})];
        yield return ["completeness",(Func<GeneratedExamEnvelope,GeneratedExamEnvelope>)(x=>x with{Tasks=[]})];
    }

    [Theory,MemberData(nameof(HardFailures))]
    public void Format_drift_is_always_rejected(string expectedCode,Func<GeneratedExamEnvelope,GeneratedExamEnvelope> mutate)
    {
        var blueprint=SimpleBlueprint();
        var contract=assembler.Contract(blueprint,"PartPractice","listening","h1",[]);
        var generated=mutate(assembler.Assemble(blueprint,contract,ReplacementContent(contract,"fresh")));
        var result=validator.Validate(blueprint,generated,"PartPractice","listening","h1",[]);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues,x=>x.Code==expectedCode);
    }

    [Fact]
    public void Unknown_missing_and_duplicate_slots_never_assemble()
    {
        var blueprint=SimpleBlueprint(); var contract=assembler.Contract(blueprint,"PartPractice","listening","h1",[]);
        var valid=ReplacementContent(contract,"fresh");
        Assert.Throws<InvalidOperationException>(()=>assembler.Assemble(blueprint,contract,valid with{Replacements=valid.Replacements.Skip(1).ToArray()}));
        Assert.Throws<InvalidOperationException>(()=>assembler.Assemble(blueprint,contract,valid with{Replacements=[..valid.Replacements,new("unknown", "x",null,null)]}));
        Assert.Throws<InvalidOperationException>(()=>assembler.Assemble(blueprint,contract,valid with{Replacements=[..valid.Replacements,valid.Replacements[0]]}));
    }

    [Theory]
    [InlineData("متن را بخوانید و پاسخ دهید.")]
    [InlineData("Choose the correct answer.")]
    [InlineData("Quelle G: Information 5")]
    [InlineData("Originaler Unidemix-Übungstext")]
    public void German_mock_quality_validator_rejects_language_leakage_and_placeholders(string prompt)
    {
        var task = new ExamTaskSource("quality-1", "listening", "h1", "multiple-choice", prompt,
            null, "Der Zug fährt heute um zehn Uhr ab.", "Deutsch", 20, 1,
            "script-ready-audio-deferred", "Um zehn Uhr", ["Um zehn Uhr", "Um elf Uhr", "Um zwölf Uhr"], 1, false);

        Assert.Throws<InvalidOperationException>(() => ExamContentQualityValidator.ValidateGermanMock([task]));
    }

    private static GeneratedContentReplacementEnvelope ReplacementContent(ExamAiContext contract,string seed) => new("exam-content-replacements-v1",
        contract.Parts.SelectMany(part=>part.ContentSlots.Select(slot=>slot.Type=="question"
            ? new GeneratedContentReplacement(slot.Key,$"Welche Antwort passt zu {seed}?",part.OptionCount>0?Enumerable.Range(1,part.OptionCount).Select(i=>$"{seed}-{i}").ToArray():null,part.OptionCount>0?$"{seed}-1":null)
            : new GeneratedContentReplacement(slot.Key,$"Dies ist ein neuer deutscher Inhalt für {seed}. Er bleibt innerhalb der festgelegten Aufgabe.",null,null))).ToArray());

    private static ExamBlueprintDocument LoadBlueprint(string provider,string cefr)
    {
        if(provider=="goethe")return ExamBlueprintSeeder.Goethe(cefr);
        var path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","src","Unidemix.Api","Content","Learning","German","Exams","telc",cefr,"exam-package.json"));
        var package=JsonSerializer.Deserialize<TelcExamPackage>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        var program=new ExamProgram{Level=cefr,Name=package.Name,ContentVersion=package.Version,BlueprintJson=JsonSerializer.Serialize(package.Sections),ScoringJson=JsonSerializer.Serialize(package.Scoring),SourceReference=package.SourceReference,WrittenDurationMinutes=package.Timing.WrittenMinutes,SpeakingDurationMinutes=package.Timing.SpeakingMinutes,PreparationDurationMinutes=package.Timing.PreparationMinutes};
        return ExamBlueprintSeeder.FromTelc(program);
    }

    private static ExamBlueprintDocument SimpleBlueprint()
    {
        var part=new ExamPartBlueprint("h1",1,"multiple-choice",1,"fixed","objective",new(1,1,30,null),5,"one-point",3,
            new(10,500,0,"neutral",[]),new(15,"A1","direct","near",0,0,"neutral"),
            [new("h1:listening-script","listening-script",null,true,10,500),new("h1:question:1","question",1,true,1,200)]);
        return new("telc","telc-a1","telc A1","de","A1","adults",1,"Validated",DateTimeOffset.UtcNow,["official"],
            [new("listening","Hören",1,20,[part])],new(1,1,false,"fixed",["listening"]));
    }
}
