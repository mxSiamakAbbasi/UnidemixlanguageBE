import { publishAuthoredTopic as save } from "./grammar-authoring-utils.mjs";

export const file = "src/Unidemix.Api/Content/Learning/German/Grammar/grammar-b2-v1.json";

function completeExample(exercise, answer = exercise.acceptedAnswers[0]) {
  if (exercise.promptDe.includes("___")) return exercise.promptDe.replace("___", answer);
  return answer.trim().split(/\s+/).length >= 4 ? answer : null;
}

function referenceExamples(exercises) {
  return exercises.map((exercise) => ({ german: completeExample(exercise), persian: exercise.explanationFa }))
    .filter((example) => example.german).slice(0, 4);
}

function contrastFor(exercise) {
  const wrong = exercise.options.find((option) => !exercise.acceptedAnswers.includes(option));
  return { correct: completeExample(exercise), incorrect: wrong ? completeExample(exercise, wrong) : null };
}

export function publish({ contentKey, order, titleDe, titleFa, objective, patterns, exercises, category = "Fortgeschrittene Satz- und Textgrammatik", progression = "Advanced/Revisited" }) {
  const subskills = [...new Set(exercises.map((exercise) => exercise.subskill))];
  const contrast = contrastFor(exercises[0]);
  save(file, {
    contentKey, languageCode: "de", cefrLevel: "B2", order, titleDe, titleFa, category, progression,
    qualityStatus: "PASS",
    relatedCoreLessons: [`b2-lesson-${String(Math.ceil(order / 2)).padStart(2, "0")}`],
    exerciseBlueprint: {
      learningObjectives: [objective], subskills,
      allowedExerciseTypes: [...new Set(exercises.map((exercise) => exercise.type))],
      validPatterns: patterns,
      commonErrorPatterns: ["Register und Aussageabsicht nicht unterscheiden", "Satzklammer oder Verbendstellung verletzen", "formal ähnliche Strukturen funktional verwechseln"],
      distractorRules: ["plausible B2-Lernerfehler mit klarer Fehlerursache", "keine zufälligen Fehlformen", "Register und Bedeutungsnuance mitprüfen"],
      vocabularyDomains: [...new Set(exercises.map((exercise) => exercise.contextDomain))],
      forbiddenPatterns: ["mechanischer Nomentausch", "unnatürliche Wiederholung", "mehrdeutige Musterlösung"],
      minimumSemanticVariation: 10,
      difficultyProgression: ["Form und Funktion im Kontext erkennen", "Strukturen kontrastieren und transformieren", "registergerecht und zusammenhängend produzieren"],
    },
    reference: {
      usageFa: objective, structure: patterns.join("; "),
      tables: [{ title: "ساختارهای B2", headers: ["الگو", "کاربرد"], rows: patterns.map((pattern, index) => [pattern, ["ساخت و کاربرد پایه در متن B2", "تمایز معنایی و سبکی در مقایسه با ساختارهای نزدیک", "جایگاه اجزا و پیوند ساختار در جملهٔ پیچیده"][Math.min(index, 2)]]) }],
      examples: referenceExamples(exercises),
      contrasts: [{ left: contrast.correct, right: contrast.incorrect, explanationFa: "نمونهٔ درست هم از نظر ساخت و هم از نظر نقش معنایی و سطح رسمی متن مناسب است؛ نمونهٔ مقابل یک خطای محتمل B2 را نشان می‌دهد." }],
      commonMistakes: ["انتخاب ساختار بدون توجه به نوع متن و قصد گوینده", "انتقال ترتیب جملهٔ اصلی به بند وابسته یا ساخت چندبخشی"],
      notes: ["در B2 درستی دستوری به‌تنهایی کافی نیست؛ ساختار باید با معنا، بافت و register نیز هماهنگ باشد."],
      summaryFa: objective,
    }, exercises,
  });
}
