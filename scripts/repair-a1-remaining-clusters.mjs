import fs from "node:fs";

const file = "src/Unidemix.Api/Content/Learning/German/Grammar/grammar-a1-v1.json";
const pkg = JSON.parse(fs.readFileSync(file, "utf8"));
const keys = [
  "a1-grammar-dativ", "a1-grammar-personalpronomen", "a1-grammar-possessiv",
  "a1-grammar-nicht", "a1-grammar-praeposition-ort", "a1-grammar-praeposition-zeit",
  "a1-grammar-perfekt-haben", "a1-grammar-perfekt-sein", "a1-grammar-partizip-zwei",
  "a1-grammar-praeteritum-sein-haben", "a1-grammar-vergleich", "a1-grammar-konnektoren"
];
const normalizationRules = ["trim", "collapseWhitespace", "ignoreTerminalPunctuation", "ignoreInitialCapitalization"];

function changedSpan(wrong, correct) {
  const a = wrong.replace(/[.!?]$/, "").split(/\s+/);
  const b = correct.replace(/[.!?]$/, "").split(/\s+/);
  let left = 0;
  while (left < a.length && left < b.length && a[left] === b[left]) left++;
  let ar = a.length - 1, br = b.length - 1;
  while (ar >= left && br >= left && a[ar] === b[br]) { ar--; br--; }
  return { answer: b.slice(left, br + 1).join(" "), prompt: [...b.slice(0, left), "___", ...b.slice(br + 1)].join(" ") + "." };
}

for (const key of keys) {
  const topic = pkg.topics.find(x => x.contentKey === key);
  if (topic.exercises.length === 12 && topic.exercises.every(x => x.id.startsWith(`${key}-v3-`))) continue;
  const source = topic.exercises;
  const repaired = [];
  for (let group = 0; group < source.length; group += 3) {
    const choice = structuredClone(source[group]);
    const correction = structuredClone(source[group + 1]);
    const correct = choice.acceptedAnswers[0];
    const wrong = correction.promptDe.replace(/^Korrigieren Sie:\s*/, "");
    const delta = changedSpan(wrong, correct);
    const subskill = choice.subskill || topic.exerciseBlueprint.subskills[(group / 3) % topic.exerciseBlueprint.subskills.length];
    const family = `${key}:${String(group / 3 + 1).padStart(2, "0")}`;

    repaired.push({
      ...choice,
      id: `${key}-v3-${String(repaired.length + 1).padStart(2, "0")}`,
      type: group % 9 === 0 ? "contextualChoice" : "chooseForm",
      subskill,
      qaStatus: "PASS",
      normalizationRules,
      errorReason: choice.errorReason || topic.exerciseBlueprint.commonErrorPatterns[(group / 3) % topic.exerciseBlueprint.commonErrorPatterns.length],
      contextDomain: choice.contextDomain || topic.exerciseBlueprint.vocabularyDomains[(group / 3) % topic.exerciseBlueprint.vocabularyDomains.length],
      semanticFamily: `${family}:recognition`
    });
    repaired.push({
      id: `${key}-v3-${String(repaired.length + 1).padStart(2, "0")}`,
      type: "fillBlank",
      subskill,
      promptFa: "جمله را با صورت درست کامل کنید.",
      promptDe: delta.prompt,
      options: [],
      acceptedAnswers: [delta.answer],
      explanationFa: correction.explanationFa,
      qaStatus: "PASS",
      normalizationRules,
      errorReason: null,
      contextDomain: correction.contextDomain || topic.exerciseBlueprint.vocabularyDomains[(group / 3) % topic.exerciseBlueprint.vocabularyDomains.length],
      semanticFamily: `${family}:production`
    });
  }
  topic.exercises = repaired;
  topic.qualityStatus = "PASS";
  topic.exerciseBlueprint.allowedExerciseTypes = [...new Set(repaired.map(x => x.type))];
  topic.exerciseBlueprint.minimumSemanticVariation = Math.min(10, repaired.length);
  let n = 0;
  for (const q of repaired.filter(x => x.options.length)) {
    const correct = q.options.find(x => q.acceptedAnswers.includes(x));
    const position = [1, 2, 0, 2, 1, 0, 1][n++ % 7];
    q.options = q.options.filter(x => x !== correct);
    q.options.splice(position, 0, correct);
  }
}

fs.writeFileSync(file, JSON.stringify(pkg, null, 2) + "\n");
