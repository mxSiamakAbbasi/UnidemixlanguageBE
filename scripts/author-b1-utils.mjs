import { publishAuthoredTopic as save } from "./grammar-authoring-utils.mjs";
export const file =
  "src/Unidemix.Api/Content/Learning/German/Grammar/grammar-b1-v1.json";

function completeExample(exercise, answer = exercise.acceptedAnswers[0]) {
  if (exercise.promptDe.includes("___")) {
    return exercise.promptDe.replace("___", answer);
  }

  if (answer.trim().split(/\s+/).length >= 4) return answer;
  return null;
}

function referenceExamples(exercises) {
  return exercises
    .map((exercise) => ({
      german: completeExample(exercise),
      persian: exercise.explanationFa,
    }))
    .filter((example) => example.german)
    .slice(0, 4);
}

function contrastFor(exercise) {
  const correct = completeExample(exercise);
  const distractor = exercise.options.find(
    (option) => !exercise.acceptedAnswers.includes(option),
  );
  const incorrect = distractor ? completeExample(exercise, distractor) : null;
  return { correct, incorrect };
}
export function publish({
  contentKey,
  order,
  titleDe,
  titleFa,
  objective,
  patterns,
  exercises,
  category = "Komplexe Satz- und Textgrammatik",
}) {
  const subskills = [...new Set(exercises.map((x) => x.subskill))];
  save(file, {
    contentKey,
    languageCode: "de",
    cefrLevel: "B1",
    order,
    titleDe,
    titleFa,
    category,
    progression:
      "A2 sichere Alltagssätze → B1 zusammenhängende, begründete und differenzierte Aussagen",
    qualityStatus: "PASS",
    relatedCoreLessons: [
      `b1-lesson-${String(Math.ceil(order / 2)).padStart(2, "0")}`,
    ],
    exerciseBlueprint: {
      learningObjectives: [objective],
      subskills,
      allowedExerciseTypes: [...new Set(exercises.map((x) => x.type))],
      validPatterns: patterns,
      commonErrorPatterns: [
        "Kasus oder Verbposition aus dem Hauptsatz übernehmen",
        "ähnliche Konnektoren funktional verwechseln",
        "Zeit- oder Perspektivbezug nicht konsequent markieren",
      ],
      distractorRules: [
        "nur plausible B1-Lernerfehler",
        "jede Option prüft die jeweilige Teilkompetenz",
        "keine zufälligen oder beschädigten Sätze",
      ],
      vocabularyDomains: [...new Set(exercises.map((x) => x.contextDomain))],
      forbiddenPatterns: [
        "mechanischer Austausch einzelner Nomen",
        "unnatürliche Wiederholung",
        "mehrdeutige Musterlösung",
      ],
      minimumSemanticVariation: 10,
      difficultyProgression: [
        "Form im Kontext erkennen",
        "Beziehungen zwischen Sätzen kontrollieren",
        "zusammenhängend produzieren",
      ],
    },
    reference: {
      usageFa: objective,
      structure: patterns.join("; "),
      tables: [
        {
          title: "ساختارهای B1",
          headers: ["الگو", "کارکرد"],
          rows: patterns.map((p, i) => [
            p,
            [
              "صورت پایه برای تشخیص و ساخت این موضوع",
              "صورت مقایسه‌ای برای انتخاب دقیق نقش و معنا",
              "الگوی ترکیب اجزا و کنترل جایگاه فعل در جمله",
            ][Math.min(i, 2)],
          ]),
        },
      ],
      examples: referenceExamples(exercises),
      contrasts: [
        {
          left: contrastFor(exercises[0]).correct,
          right: contrastFor(exercises[0]).incorrect,
          explanationFa:
            "تفاوت نقش دستوری یا رابطهٔ معنایی دو ساختار باید آگاهانه تشخیص داده شود.",
        },
      ],
      commonMistakes: [
        "توجه‌نکردن به نقش اسم یا مرجع",
        "انتقال ترتیب جملهٔ اصلی به بند وابسته",
      ],
      notes: [
        "در B1 انتخاب ساختار باید هم درست و هم از نظر معنایی مناسب متن باشد.",
      ],
      summaryFa: objective,
    },
    exercises,
  });
}
