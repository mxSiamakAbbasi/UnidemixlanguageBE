import fs from "node:fs";
const file =
  process.argv[2] ??
  "src/Unidemix.Api/Content/Learning/German/Grammar/grammar-a1-v1.json";
const pkg = JSON.parse(fs.readFileSync(file, "utf8"));
const expectedCoverage = {
  A1: [
    "a1-g1",
    "a1-grammar-vokalwechsel",
    "a1-g3",
    "a1-g2",
    "a1-g4",
    "a1-grammar-v2",
    "a1-grammar-satzklammer",
    "a1-grammar-modalverben",
    "a1-grammar-moechten",
    "a1-g5",
    "a1-grammar-untrennbar",
    "a1-grammar-imperativ",
    "a1-grammar-artikel-bestimmt",
    "a1-grammar-artikel-unbestimmt",
    "a1-grammar-kein",
    "a1-grammar-plural",
    "a1-grammar-nominativ",
    "a1-g6",
    "a1-grammar-dativ",
    "a1-grammar-personalpronomen",
    "a1-grammar-possessiv",
    "a1-grammar-nicht",
    "a1-grammar-praeposition-ort",
    "a1-grammar-praeposition-zeit",
    "a1-grammar-perfekt-haben",
    "a1-grammar-perfekt-sein",
    "a1-grammar-partizip-zwei",
    "a1-grammar-praeteritum-sein-haben",
    "a1-grammar-vergleich",
    "a1-grammar-konnektoren",
  ],
  A2: [
    "a2-possessiv-unser-euer",
    "a2-perfekt-narration",
    "a2-wechselpraepositionen",
    "a2-positionsverben",
    "a2-adjektiv-bestimmt",
    "a2-temporal-erweitert",
    "a2-adjektiv-unbestimmt",
    "a2-wenn",
    "a2-konjunktiv-zwei-modal",
    "a2-weil-deshalb",
    "a2-dass",
    "a2-reflexive-verben",
    "a2-als-vergangenheit",
    "a2-passiv-praesens",
    "a2-objektreihenfolge",
    "a2-indirekte-fragen",
    "a2-praepositionalverben",
    "a2-modalverben-praeteritum",
    "a2-herkunft-veranstaltungsort",
    "a2-welch-demonstrativ",
    "a2-lassen",
    "a2-relativ-nom-akk",
    "a2-bis-seitdem",
    "a2-narratives-praeteritum",
  ],
  B1: [
    "b1-past-review-narration",
    "b1-plusquamperfekt",
    "b1-temporalsaetze",
    "b1-futur-eins",
    "b1-konjunktiv-zwei-gegenwart",
    "b1-konjunktiv-zwei-vergangenheit",
    "b1-passiv-praesens-review",
    "b1-passiv-praeteritum",
    "b1-passiv-modal",
    "b1-zustandspassiv",
    "b1-relative-alle-kasus",
    "b1-relativ-dativ-praep",
    "b1-relativ-wo-was",
    "b1-infinitiv-mit-zu",
    "b1-um-zu-damit",
    "b1-dass-vs-infinitiv",
    "b1-kausalsaetze",
    "b1-obwohl-trotzdem",
    "b1-konditionalsaetze",
    "b1-konsekutivsaetze",
    "b1-zweiteilige-konnektoren",
    "b1-je-desto",
    "b1-adjektivdeklination-voll",
    "b1-nominalisierte-adjektive",
    "b1-n-deklination",
    "b1-verben-praepositionen",
    "b1-praepositionaladverbien",
    "b1-genitiv-basis",
    "b1-genitiv-praepositionen",
    "b1-partizip-adjektiv",
    "b1-mittelfeld",
    "b1-indirekte-fragen",
    "b1-redeinhalt-wiedergeben",
    "b1-lassen-brauchen",
    "b1-kohasion-text",
  ],
  B2: [
    "b2-passiv-zeiten", "b2-zustandspassiv", "b2-passiv-modal", "b2-passiversatzformen",
    "b2-konjunktiv-zwei-gegenwart", "b2-konjunktiv-zwei-vergangenheit", "b2-irreale-bedingungen", "b2-als-ob-als-wenn",
    "b2-konjunktiv-eins", "b2-indirekte-rede", "b2-konjunktiv-zwei-ersatz", "b2-subjektive-modalverben",
    "b2-futur-zwei-vermutung", "b2-nominalisierung", "b2-nominal-verbalstil", "b2-funktionsverbgefuege",
    "b2-partizipialadjektive", "b2-erweiterte-attribute", "b2-relative-strukturen", "b2-eingebettete-nebensaetze",
    "b2-subjekt-objektsaetze", "b2-infinitivsaetze-erweitert", "b2-modalsaetze", "b2-konsekutivsaetze",
    "b2-finalsaetze", "b2-bedingung-ohne-wenn", "b2-konzessive-strukturen", "b2-zweiteilige-konnektoren",
    "b2-je-desto-umso", "b2-rektion-verben-nomen-adjektive", "b2-praepositionaladverbien", "b2-genitivpraepositionen",
    "b2-modalpartikeln", "b2-indefinitpronomen", "b2-wortbildung", "b2-textgrammatik",
    "b2-informationsstruktur", "b2-register",
  ],
  C1: [
    "c1-konnektoren-folgerung-ausnahme", "c1-trennbarkeit-wortbildung-register", "c1-rede-wiedergabe", "c1-nominal-verbalstil",
    "c1-subjekt-objektsaetze", "c1-weiterfuehrende-nebensaetze", "c1-temporale-nominalisierung", "c1-kausale-transformation",
    "c1-modale-transformation", "c1-negative-konsekutivsaetze", "c1-konzessiv-final-transformation", "c1-infinitivsaetze-gegenwart-vergangenheit",
    "c1-konditionale-nominalisierung", "c1-passiv-besonderheiten", "c1-modales-partizip", "c1-subjektive-modalverben-behauptung",
    "c1-subjektive-modalverben-wahrscheinlichkeit", "c1-nominalisierung-praepositionalergaenzung", "c1-konnektoren-diskurs", "c1-konditionalsaetze-erweitert",
    "c1-modalitaetsverben", "c1-verben-mit-praepositionen", "c1-komplexer-satzbau", "c1-informationsstruktur-fokus",
    "c1-partizipialkonstruktionen", "c1-unpersoenliches-passiv", "c1-passivalternativen", "c1-berichtete-fragen-aufforderungen",
    "c1-funktionsverbgefuege", "c1-genitiv-praepositionalattribute", "c1-textkohaesion", "c1-wortbildung",
    "c1-register-hedging", "c1-akademischer-beruflicher-stil",
  ],
};
const results = pkg.topics.map((topic) => {
  const issues = [];
  const exercises = topic.exercises;
  const types = new Set(exercises.map((x) => x.type));
  const subskills = new Set(
    exercises.filter((x) => x.qaStatus === "PASS").map((x) => x.subskill),
  );
  const missing = topic.exerciseBlueprint.subskills.filter(
    (x) => !subskills.has(x),
  );
  if (missing.length) issues.push(`missing subskills: ${missing.join(", ")}`);
  const accepted = new Map();
  for (const x of exercises)
    for (const a of new Set(
      x.acceptedAnswers.map((a) =>
        a
          .toLowerCase()
          .replace(/[.!?]$/, "")
          .replace(/\s+/g, " ")
          .trim(),
      ),
    )) {
      const ids = accepted.get(a) ?? new Set();
      ids.add(x.id);
      accepted.set(a, ids);
    }
  const repeated = [...accepted].filter(
    ([answer, ids]) => answer.split(/\s+/).length >= 4 && ids.size > 1,
  );
  if (repeated.length)
    issues.push(
      `${repeated.length} full accepted answers reused across exercises`,
    );
  if (types.size < 3) issues.push("insufficient interaction variety");
  const typeCounts = new Map();
  for (const x of exercises)
    typeCounts.set(x.type, (typeCounts.get(x.type) ?? 0) + 1);
  const dominantType = Math.max(...typeCounts.values()) / exercises.length;
  if (dominantType > 0.55)
    issues.push("one interaction type dominates the bank");
  const pairedPrompts = exercises.filter((x) =>
    /Welcher Satz ist grammatisch richtig\?/.test(x.promptDe),
  ).length;
  if (pairedPrompts > Math.max(2, Math.floor(exercises.length * 0.2)))
    issues.push("generic correct-sentence prompt is overused");
  const optionPositions = exercises
    .filter((x) => x.options.length)
    .map((x) => x.options.findIndex((o) => x.acceptedAnswers.includes(o)));
  if (optionPositions.length >= 4) {
    const counts = new Map();
    for (const position of optionPositions)
      counts.set(position, (counts.get(position) ?? 0) + 1);
    const dominant = Math.max(...counts.values()) / optionPositions.length;
    if (counts.size === 1 || dominant > 0.72)
      issues.push("correct option position is predictably biased");
    const cycleLength = [2, 3].find(
      (size) =>
        optionPositions.length >= size * 2 &&
        optionPositions.every(
          (value, index) =>
            index < size || value === optionPositions[index % size],
        ),
    );
    if (cycleLength)
      issues.push(
        `correct option position repeats an obvious ${cycleLength}-step cycle`,
      );
  }
  const genericFeedback = exercises.filter(
    (x) => !x.explanationFa || x.explanationFa.length < 30,
  ).length;
  if (genericFeedback) issues.push(`${genericFeedback} weak feedback items`);
  const invalidOptions = exercises.filter(
    (x) =>
      x.options.length &&
      (x.options.some((o) => o == null) ||
        x.acceptedAnswers.some((a) => !x.options.includes(a)) ||
        new Set(x.options).size !== x.options.length),
  ).length;
  if (invalidOptions)
    issues.push(
      `${invalidOptions} objective items have invalid option/answer mapping`,
    );
  const missingReasons = exercises.filter(
    (x) => x.options.length && !x.errorReason,
  ).length;
  if (missingReasons)
    issues.push(`${missingReasons} objective items lack an error reason`);
  const families = new Map();
  for (const x of exercises)
    families.set(x.semanticFamily, (families.get(x.semanticFamily) ?? 0) + 1);
  const dominant = [...families].filter(
    ([, n]) => n > Math.max(4, Math.ceil(exercises.length * 0.3)),
  );
  if (dominant.length)
    issues.push(
      `dominant semantic family: ${dominant.map((x) => x[0]).join(", ")}`,
    );
  const reference = topic.reference;
  const fragmentExamples = reference.examples.filter(
    (example) => example.german.trim().split(/\s+/).length < 3,
  ).length;
  if (reference.examples.length < 4)
    issues.push("reference chapter has fewer than four examples");
  if (fragmentExamples)
    issues.push(`${fragmentExamples} reference examples are sentence fragments`);
  const placeholderTableCells = reference.tables
    .flatMap((table) => table.rows.flat())
    .filter((cell) => /^کارکرد\s+\d+$/u.test(cell)).length;
  if (placeholderTableCells)
    issues.push(`${placeholderTableCells} placeholder reference-table cells`);
  if (!reference.commonMistakes?.length || !reference.notes?.length)
    issues.push("reference chapter lacks mistakes or usage notes");
  const status = issues.length ? "REVISION REQUIRED" : "PASS";
  return {
    topic: topic.contentKey,
    status,
    types: [...types],
    subskills: [...subskills],
    issues,
  };
});
const level = pkg.topics[0]?.cefrLevel;
const expected = expectedCoverage[level] ?? [];
const actual = new Set(pkg.topics.map((x) => x.contentKey));
const missing = expected.filter((x) => !actual.has(x));
const unexpected = expected.length
  ? pkg.topics.map((x) => x.contentKey).filter((x) => !expected.includes(x))
  : [];
const coverage = {
  level,
  expected: expected.length,
  actual: actual.size,
  missing,
  unexpected,
  status: missing.length || unexpected.length ? "REVISION REQUIRED" : "PASS",
};
console.log(
  JSON.stringify(
    {
      topics: results.length,
      coverage,
      summary: Object.groupBy(results, (x) => x.status),
      results,
    },
    null,
    2,
  ),
);
if (
  coverage.status !== "PASS" ||
  results.some((x) => x.status === "REVISION REQUIRED" || x.status === "REJECT")
)
  process.exitCode = 2;
