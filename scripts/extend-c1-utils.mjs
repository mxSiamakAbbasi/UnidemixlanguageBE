import fs from "node:fs";
import { file } from "./author-c1-utils.mjs";

export function extend(contentKey, exercises) {
  const pkg = JSON.parse(fs.readFileSync(file, "utf8"));
  const topic = pkg.topics.find((x) => x.contentKey === contentKey);
  if (!topic) throw new Error(`Missing topic ${contentKey}`);
  const ids = new Set(exercises.map((x) => x.id));
  topic.exercises = topic.exercises.filter((x) => !ids.has(x.id));
  let objectiveIndex = topic.exercises.filter((x) => x.options.length).length;
  for (const exercise of exercises) {
    if (exercise.options.length) {
      const correct = exercise.options.find((x) => exercise.acceptedAnswers.includes(x));
      if (correct == null) throw new Error(`Correct option missing: ${exercise.id}`);
      exercise.options = exercise.options.filter((x) => x !== correct);
      const target = [2, 0, 1, 2, 1, 0][objectiveIndex++ % 6];
      exercise.options.splice(Math.min(target, exercise.options.length), 0, correct);
    }
    topic.exercises.push(exercise);
  }
  topic.exerciseBlueprint.subskills = [...new Set(topic.exercises.map((x) => x.subskill))];
  topic.exerciseBlueprint.allowedExerciseTypes = [...new Set(topic.exercises.map((x) => x.type))];
  topic.exerciseBlueprint.minimumSemanticVariation = Math.max(10, topic.exercises.length - 2);
  fs.writeFileSync(file, `${JSON.stringify(pkg, null, 2)}\n`);
}
