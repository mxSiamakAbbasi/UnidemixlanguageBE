import fs from "node:fs";
import { publishAuthoredTopic as save } from "./grammar-authoring-utils.mjs";

export const file = "src/Unidemix.Api/Content/Learning/German/Grammar/grammar-c1-v1.json";

export function initialize() {
  if (fs.existsSync(file)) return;
  fs.writeFileSync(file, `${JSON.stringify({
    schemaVersion: "2.0",
    packageId: "german-grammar-c1-v1",
    version: "2.0.0-draft",
    languageCode: "de",
    cefrLevel: "C1",
    titleFa: "گرامر کاربردی آلمانی C1",
    qualityStatus: "DRAFT",
    topics: [],
  }, null, 2)}\n`);
}

function completed(exercise, answer = exercise.acceptedAnswers[0]) {
  if (exercise.promptDe.includes("___")) return exercise.promptDe.replace("___", answer);
  return answer?.trim().split(/\s+/).length >= 4 ? answer : null;
}

export function publish({ contentKey, order, titleDe, titleFa, objective, patterns, exercises, category = "Komplexe Satz-, Text- und Registergrammatik" }) {
  initialize();
  const examples = exercises.map((x) => ({ german: completed(x), persian: x.explanationFa })).filter((x) => x.german).slice(0, 4);
  const firstObjective = exercises.find((x) => x.options.length && completed(x));
  const wrong = firstObjective?.options.find((x) => !firstObjective.acceptedAnswers.includes(x));
  save(file, {
    contentKey, languageCode: "de", cefrLevel: "C1", order, titleDe, titleFa,
    category, progression: "Advanced/Revisited", qualityStatus: "PASS",
    relatedCoreLessons: [`c1-lesson-${String(Math.ceil(order / 2)).padStart(2, "0")}`],
    exerciseBlueprint: {
      learningObjectives: [objective],
      subskills: [...new Set(exercises.map((x) => x.subskill))],
      allowedExerciseTypes: [...new Set(exercises.map((x) => x.type))],
      validPatterns: patterns,
      commonErrorPatterns: ["Bedeutungsnuance oder Register verfehlen", "komplexe Satzklammer oder Rektion verletzen", "formal mögliche, kontextuell aber unpassende Variante wählen"],
      distractorRules: ["plausible fortgeschrittene Lernerfehler", "Bedeutung und Textfunktion gezielt kontrastieren", "keine zufälligen Fehlformen"],
      vocabularyDomains: [...new Set(exercises.map((x) => x.contextDomain))],
      forbiddenPatterns: ["mechanischer Nomentausch", "unnatürliche Überladung", "mehrdeutige Musterlösung"],
      minimumSemanticVariation: Math.max(6, exercises.length - 2),
      difficultyProgression: ["Form und Funktion differenzieren", "Strukturen im Diskurs transformieren", "registergerecht und präzise produzieren"],
    },
    reference: {
      usageFa: objective,
      structure: patterns.join("; "),
      tables: [{ title: "ساختارهای C1", headers: ["الگو", "کاربرد"], rows: patterns.map((p, i) => [p, ["بیان دقیق رابطهٔ معنایی در متن پیشرفته", "تمایز کاربرد و register در مقایسه با ساختارهای نزدیک", "کنترل جایگاه اجزا در جمله و متن پیچیده"][Math.min(i, 2)]]) }],
      examples,
      contrasts: firstObjective && wrong ? [{ left: completed(firstObjective), right: completed(firstObjective, wrong), explanationFa: "نمونهٔ درست هم از نظر ساختار و هم از نظر معنا و register با بافت C1 هماهنگ است؛ گزینهٔ مقابل یک خطای محتمل و هدفمند را نشان می‌دهد." }] : [],
      commonMistakes: ["انتخاب ساختار فقط بر اساس شباهت ظاهری و بدون توجه به نقش آن در متن", "از دست دادن مرجع، زمان یا درجهٔ قطعیت هنگام بازنویسی"],
      notes: ["در C1 چند صورت ممکن است از نظر دستوری درست باشند؛ پاسخ پذیرفته‌شده باید با معنا، بافت و register خواسته‌شده نیز هماهنگ باشد."],
      summaryFa: objective,
    },
    exercises,
  });
}
