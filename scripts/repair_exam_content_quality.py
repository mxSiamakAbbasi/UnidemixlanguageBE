import json
import re
from pathlib import Path

ROOT = Path(__file__).parents[1] / "src/Unidemix.Api/Content/Learning/German/Exams/telc"

HEADINGS = [
    "Terminänderung für ein Gruppentreffen", "Kostenloser Kurs in der Stadtbibliothek",
    "Anmeldung für einen Wochenendkurs", "Neue Abfahrtszeit am Bahnhof",
    "Ehrenamtliche Hilfe im Nachbarschaftszentrum", "Sportangebot für Berufstätige",
    "Beratung rund um Weiterbildung", "Flohmarkt mit vorheriger Tischreservierung",
    "Stadtführung mit geändertem Treffpunkt", "Workshop zur digitalen Sicherheit",
    "Gemeinsamer Ausflug bei gutem Wetter", "Kulturveranstaltung mit freiem Eintritt",
    "Reparaturtreff für Fahrräder und Geräte", "Sprachcafé sucht neue Teilnehmende",
    "Wohnungsbesichtigung nur nach Anmeldung", "Gesundheitskurs am frühen Abend",
    "Informationsabend zu öffentlichen Verkehrsmitteln", "Kochkurs mit regionalen Zutaten",
    "Teamtreffen wird auf Montag verschoben", "Museum bietet eine Führung auf Deutsch an",
]

WRITING = {
    "A1": {
        "w1": "Füllen Sie das Formular mit den fünf fehlenden Angaben aus.",
        "w2": "Sie möchten an einem Kurs im Nachbarschaftszentrum teilnehmen. Schreiben Sie eine kurze Nachricht von etwa 30 Wörtern. Schreiben Sie, warum Sie teilnehmen möchten, was Sie mitbringen müssen und wann Sie kommen können. Verwenden Sie eine passende Anrede und einen passenden Schluss.",
    },
    "A2": {
        "w1": "Füllen Sie das Formular mit den Angaben aus der Situation aus.",
        "w2": "Sie können an einer geplanten Veranstaltung nicht teilnehmen. Schreiben Sie der Organisatorin eine kurze Nachricht. Entschuldigen Sie sich, nennen Sie den Grund und schlagen Sie einen neuen Termin vor.",
    },
    "B1": {
        "w1": "Ein Kulturzentrum hat den Termin einer Veranstaltung geändert. Schreiben Sie eine E-Mail. Reagieren Sie auf die Änderung, erklären Sie Ihr zeitliches Problem, schlagen Sie eine Lösung vor und bitten Sie um eine Antwort.",
    },
    "B2": {
        "w1": "Schreiben Sie eine halbformelle E-Mail an die Leitung eines beruflichen Netzwerks. Bewerten Sie das Angebot, erläutern Sie eine begründete Sorge, machen Sie zwei konkrete Verbesserungsvorschläge und bitten Sie um eine Antwort.",
    },
}

SPEAKING = {
    "A1": {
        "s1": "Stellen Sie sich vor. Nennen Sie Name, Herkunft, Wohnort, Sprachen, Beruf und Hobby. Buchstabieren Sie anschließend Ihren Familiennamen und nennen Sie eine Telefonnummer.",
        "s2": "Thema: Einkaufen. Bitten Sie um eine Information und beantworten Sie die Frage Ihrer Partnerin oder Ihres Partners.",
        "s3": "Formulieren Sie zu Ihrer Bildkarte eine höfliche Bitte und reagieren Sie passend auf die Bitte Ihrer Partnerin oder Ihres Partners.",
    },
    "A2": {
        "s1": "Stellen Sie sich kurz vor und beantworten Sie Rückfragen zu Ihrem Alltag.",
        "s2": "Sprechen Sie mit Ihrer Partnerin oder Ihrem Partner über Einkäufe für ein gemeinsames Picknick. Fragen Sie nach Wünschen und machen Sie Vorschläge.",
        "s3": "Planen Sie gemeinsam das Picknick. Einigen Sie sich auf Zeit, Ort, Einkauf und Kosten.",
    },
    "B1": {
        "s1": "Lernen Sie Ihre Partnerin oder Ihren Partner kennen. Stellen und beantworten Sie Fragen zu Alltag, Arbeit und Sprachenlernen.",
        "s2": "Sprechen Sie über Weiterbildung im Alltag. Beschreiben Sie eine eigene Erfahrung, nennen Sie Vor- und Nachteile und begründen Sie Ihre Meinung.",
        "s3": "Planen Sie gemeinsam einen Informationstag. Klären Sie Ziel, Ort, Aufgaben, Material und Zeitplan.",
    },
    "B2": {
        "s1": "Berichten Sie strukturiert über eine berufliche Veränderung und erklären Sie, welche Folgen sie für Sie hatte.",
        "s2": "Diskutieren Sie, ob Betriebe feste Lernzeiten anbieten sollten. Gehen Sie auf Gegenargumente ein und begründen Sie Ihren Standpunkt.",
        "s3": "Planen Sie gemeinsam eine öffentliche Informationsveranstaltung. Legen Sie Zielgruppe, Programm, Verantwortlichkeiten und Zeitplan fest.",
    },
}

PERSIAN = re.compile(r"[\u0600-\u06ff]")
FAKE_SOURCE = re.compile(r"^(Quelle\s+[A-Z]:\s*Information\s+\d+|Hinweis\s+[A-Z])$", re.I)


def rotate(values, offset, count):
    return [values[(offset + i) % len(values)] for i in range(count)]


def repair_task(task, level, ordinal):
    section, part, kind = task["section"], task["part"], task["type"]
    if section == "writing":
        task["prompt"] = WRITING[level].get(part, next(iter(WRITING[level].values())))
    elif section == "speaking":
        task["prompt"] = SPEAKING[level].get(part, next(iter(SPEAKING[level].values())))
    elif section == "listening":
        task["prompt"] = (
            "Hören Sie den Text. Ist die Aussage richtig oder falsch?"
            if "true-false" in kind else
            "Hören Sie den Text und notieren Sie die verlangte Information."
            if kind == "short-answer" else
            "Hören Sie den Text und wählen Sie die richtige Antwort."
        )
        task["speakerMetadata"] = f"Deutsch, Niveau {level}; klare natürliche Sprechweise"
    elif section == "language-elements":
        task["prompt"] = "Welche Formulierung passt sprachlich und inhaltlich am besten in den Zusammenhang?"
    elif section == "reading":
        task["prompt"] = (
            "Lesen Sie den Text. Ist die Aussage richtig oder falsch?"
            if kind == "true-false" else
            "Lesen Sie die Situation und wählen Sie die passende Informationsquelle."
            if kind == "source-selection" else
            "Lesen Sie den Text und wählen Sie die passende Überschrift."
            if kind == "matching" else
            "Lesen Sie den Text und wählen Sie die richtige Antwort."
        )

    if task.get("sourceText") and PERSIAN.search(task["sourceText"]):
        task["sourceText"] = "Anmeldung: Name, Veranstaltung, Termin, Treffpunkt und benötigtes Material"

    options = task.get("options")
    if not options:
        return
    correct_index = options.index(task["correctAnswer"]) if task.get("correctAnswer") in options else 0
    if kind == "matching":
        if section == "language-elements":
            pool = ["allerdings", "deshalb", "während", "sofern", "dennoch", "außerdem", "stattdessen", "obwohl", "damit", "dadurch", "zunächst", "schließlich", "einerseits", "andererseits", "folglich"]
        else:
            pool = HEADINGS
        repaired = rotate(pool, ordinal, len(options))
        task["options"] = repaired
        task["correctAnswer"] = repaired[correct_index]
    elif kind == "source-selection" or any(FAKE_SOURCE.match(str(option)) for option in options):
        pool = [
            "Kursprogramm des Bürgerzentrums", "Fahrplan des regionalen Verkehrsverbunds",
            "Veranstaltungskalender der Stadtbibliothek", "Öffnungszeiten des Stadtmuseums",
            "Angebote des örtlichen Sportvereins", "Beratungsseite der Volkshochschule",
        ]
        repaired = rotate(pool, ordinal, len(options))
        task["options"] = repaired
        task["correctAnswer"] = repaired[correct_index]
    elif kind not in ("true-false", "true-false-once") and any(PERSIAN.search(str(option)) for option in options):
        repaired = rotate(["Am vereinbarten Treffpunkt", "Im Bürgerzentrum", "Am Bahnhof", "In der Stadtbibliothek"], ordinal, len(options))
        task["options"] = repaired
        task["correctAnswer"] = repaired[correct_index]


for path in sorted(ROOT.glob("*/exam-package.json")):
    package = json.loads(path.read_text(encoding="utf-8"))
    level = package["cefr"]
    ordinal = 0
    for collection in [package["practiceBank"], *[variant["tasks"] for variant in package["mockVariants"]]]:
        for task in collection:
            ordinal += 1
            repair_task(task, level, ordinal)
    package["version"] = max(package.get("version", 1), 4)
    path.write_text(json.dumps(package, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

print("Repaired TELC learner-facing German content and source options.")
