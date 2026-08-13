import fs from "node:fs";
const file = "src/Unidemix.Api/Content/Learning/German/Grammar/grammar-a1-v1.json";
const pkg = JSON.parse(fs.readFileSync(file, "utf8"));
const question = pkg.topics.find(x => x.contentKey === "a1-grammar-vergleich").exercises.find(x => x.id === "a1-vgl-v4-08");
question.promptDe = "Meine Schwester steht morgens ___ auf als ich. (gern)";
question.acceptedAnswers = ["lieber"];
question.explanationFa = "برای مقایسهٔ ترجیح از صورت نامنظم lieber استفاده می‌شود.";
const possessive = pkg.topics.find(x => x.contentKey === "a1-grammar-possessiv").exercises.find(x => x.id === "a1-pos-v4-07");
possessive.options = ["Ihre", "Ihr", "Ihren"];
const participle = pkg.topics.find(x => x.contentKey === "a1-grammar-partizip-zwei").exercises.find(x => x.id === "a1-p2-v4-01");
participle.options = ["gemachen", "gemacht", "machtet"];
for (const id of ["a1-ph-v4-05", "a1-ps-v4-05"]) {
  const exercise = pkg.topics.flatMap(x => x.exercises).find(x => x.id === id);
  exercise.type = "transformation";
  const topic = pkg.topics.find(x => x.exercises.some(y => y.id === id));
  topic.exerciseBlueprint.allowedExerciseTypes = [...new Set(topic.exercises.map(x => x.type))];
}
fs.writeFileSync(file, JSON.stringify(pkg, null, 2) + "\n");
