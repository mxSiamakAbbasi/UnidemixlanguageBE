import fs from "node:fs";

function replace(file, from, to) {
  const text = fs.readFileSync(file, "utf8");
  if (!text.includes(from)) return;
  fs.writeFileSync(file, text.replaceAll(from, to));
}

replace("scripts/author-a2-topics-19-21.mjs", "a2-wd-v1-", "a2-wdem-v1-");
replace("scripts/author-a2-topics-07-09.mjs", "___ Sie mir bitte helfen?", "___ Sie mir bitte kurz mit diesem Formular helfen?");
