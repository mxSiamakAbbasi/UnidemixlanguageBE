import fs from "node:fs";

const files = process.argv.slice(2);
if (!files.length) throw new Error("Pass one or more Grammar package files.");

const normalize = (value) => value.toLowerCase().normalize("NFKC")
  .replace(/[„“”"'’.,!?;:()]/g, " ").replace(/\b\d+(?:[.,]\d+)?\b/g, "#")
  .replace(/\s+/g, " ").trim();
const skeleton = (value) => normalize(value).split(" ").map((token) => {
  if (/^[A-ZÄÖÜ]/.test(value.split(/\s+/).find((x) => normalize(x) === token) ?? "")) return "<N>";
  return token;
}).join(" ");

const rows = [];
for (const file of files) {
  const pkg = JSON.parse(fs.readFileSync(file, "utf8"));
  for (const topic of pkg.topics) for (const exercise of topic.exercises) rows.push({
    level: pkg.cefrLevel, topic: topic.contentKey, id: exercise.id,
    prompt: exercise.promptDe, answers: exercise.acceptedAnswers,
    family: exercise.semanticFamily,
  });
}

function duplicates(keyOf, eligible = () => true) {
  const map = new Map();
  for (const row of rows) for (const key of keyOf(row)) {
    if (!key || !eligible(key, row)) continue;
    const values = map.get(key) ?? [];
    values.push({ level: row.level, topic: row.topic, id: row.id });
    map.set(key, values);
  }
  return [...map].filter(([, values]) => new Set(values.map((x) => x.topic)).size > 1)
    .map(([value, occurrences]) => ({ value, occurrences }));
}

const duplicateIds = duplicates((x) => [x.id]);
const genericInstructions = new Set(["welcher satz ist richtig", "welche form ist richtig", "korrigieren sie den satz"]);
const duplicatePrompts = duplicates((x) => [normalize(x.prompt)], (key) => key.split(" ").length >= 4 && !genericInstructions.has(key));
const duplicateAnswers = duplicates((x) => x.answers.map(normalize), (key) => key.split(" ").length >= 5);
const duplicateFamilies = duplicates((x) => [normalize(x.family)], (key) => key.length > 0);
const suspiciousSkeletons = duplicates((x) => [skeleton(x.prompt)], (key) => key.split(" ").length >= 8)
  .filter((x) => x.occurrences.length >= 3);
const result = {
  levels: [...new Set(rows.map((x) => x.level))], exercises: rows.length,
  duplicateIds, duplicatePrompts, duplicateAnswers,
  informational: { duplicateFamilies, suspiciousSkeletons },
  status: duplicateIds.length || duplicatePrompts.length || duplicateAnswers.length ? "REVISION REQUIRED" : "PASS",
};
console.log(JSON.stringify(result, null, 2));
if (result.status !== "PASS") process.exitCode = 1;
