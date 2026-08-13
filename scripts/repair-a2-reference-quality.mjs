import fs from "node:fs";
const file = "src/Unidemix.Api/Content/Learning/German/Grammar/grammar-a2-v1.json";
const pkg = JSON.parse(fs.readFileSync(file, "utf8"));

for (const topic of pkg.topics) for (const table of topic.reference.tables) {
  table.rows = table.rows.map((row) => row.map((cell, index) => {
    if (!/^(?:کارکرد|کاربرد)\s+\d+$/.test(cell)) return cell;
    const pattern = row[1 - index];
    return `کاربرد هدفمند الگوی «${pattern}» در جمله و بافت سطح A2`;
  }));
}

const examples = {
  "a2-welch-demonstrativ": ["Welcher Bus fährt ins Zentrum?", "Diesen Bus nehme ich heute.", "Welches Handy gehört dir?", "Das dort gehört mir."],
  "a2-relativ-nom-akk": ["Der Mann, der dort wartet, ist mein Nachbar.", "Ich kenne den Mann, den du gestern getroffen hast.", "Die Kollegin, die uns hilft, kommt aus Köln.", "Das ist das Café, das sonntags früh öffnet."],
  "a2-bis-seitdem": ["Ich warte hier, bis der Bus kommt.", "Seitdem ich umgezogen bin, fahre ich mit der Bahn.", "Bis du anrufst, bleibe ich im Büro.", "Seitdem ich hier arbeite, kenne ich viele Leute."],
  "a2-narratives-praeteritum": ["Früher war der Weg viel kürzer.", "Wir hatten damals kein Auto.", "Am Ende konnte ich das Problem lösen.", "Danach bin ich nach Hause gegangen."],
};
for (const [key, sentences] of Object.entries(examples)) {
  const topic = pkg.topics.find((x) => x.contentKey === key);
  topic.reference.examples = sentences.map((german, index) => ({ german, persian: topic.reference.examples[index]?.persian ?? "نمونهٔ کاربردی ساختار در بافت A2" }));
}
fs.writeFileSync(file, `${JSON.stringify(pkg, null, 2)}\n`);
