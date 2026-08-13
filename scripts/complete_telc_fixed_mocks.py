import json
from pathlib import Path

ROOT = Path(__file__).parents[1] / "src/Unidemix.Api/Content/Learning/German/Exams/telc"

NAMES = ["Lina", "Mert", "Sofia", "Jonas", "Nora", "David", "Mina", "Felix", "Elif", "Paul", "Sara", "Emil", "Lea", "Omar", "Hanna", "Luis", "Maya", "Ben", "Aylin", "Tom"]
PLACES = ["Bibliothek", "Bahnhof", "Rathaus", "Sprachschule", "Sporthalle", "Marktplatz", "Bürgerzentrum", "Museum", "Apotheke", "Park"]
TIMES = ["8:15 Uhr", "9:30 Uhr", "10:45 Uhr", "12 Uhr", "14:20 Uhr", "16 Uhr", "17:30 Uhr", "18:45 Uhr", "Samstag", "Montag"]
TOPICS = ["einen Kochkurs", "das Nachbarschaftsfest", "eine Zugfahrt", "den Deutschkurs", "einen Arzttermin", "das Teamtreffen", "einen Flohmarkt", "die Wohnungsbesichtigung", "einen Workshop", "den Ausflug"]
HEADINGS = ["Terminänderung für ein Gruppentreffen", "Kostenloser Kurs in der Stadtbibliothek", "Anmeldung für einen Wochenendkurs", "Neue Abfahrtszeit am Bahnhof", "Ehrenamtliche Hilfe im Nachbarschaftszentrum", "Sportangebot für Berufstätige", "Beratung rund um Weiterbildung", "Flohmarkt mit Tischreservierung", "Stadtführung mit geändertem Treffpunkt", "Workshop zur digitalen Sicherheit", "Gemeinsamer Ausflug bei gutem Wetter", "Kulturveranstaltung mit freiem Eintritt", "Reparaturtreff für Fahrräder", "Sprachcafé sucht Teilnehmende", "Wohnungsbesichtigung nach Anmeldung", "Gesundheitskurs am Abend", "Informationsabend zum Nahverkehr", "Kochkurs mit regionalen Zutaten", "Teamtreffen wird verschoben", "Museumsführung auf Deutsch"]
SOURCES = ["Kursprogramm des Bürgerzentrums", "Fahrplan des Verkehrsverbunds", "Veranstaltungskalender der Stadtbibliothek", "Öffnungszeiten des Stadtmuseums", "Angebote des Sportvereins", "Beratungsseite der Volkshochschule"]


def metadata(level, section, part, task_type):
    if section == "listening":
        plays = 1 if (level == "A1" and part == "h2") else 2
        if level in ("B1", "B2"):
            plays = 2 if part == "h1" else 1
        return {"optionCount": 2 if "true-false" in task_type else (0 if task_type == "short-answer" else 3), "answerType": "short-text" if task_type == "short-answer" else "single-choice", "playbackCount": plays, "timingMinutes": None}
    if task_type == "true-false": return {"optionCount": 2, "answerType": "single-choice", "playbackCount": None, "timingMinutes": None}
    if task_type == "short-answer": return {"optionCount": 0, "answerType": "short-text", "playbackCount": None, "timingMinutes": None}
    if task_type == "source-selection": return {"optionCount": 2, "answerType": "single-choice", "playbackCount": None, "timingMinutes": None}
    if task_type == "matching":
        pools = {("A2", "r1"): 10, ("B1", "r1"): 10, ("B2", "r1"): 10, ("B1", "r3"): 12, ("B2", "r3"): 12, ("B1", "l2"): 15, ("B2", "l2"): 15}
        return {"optionCount": pools.get((level, part), 3), "answerType": "single-choice", "playbackCount": None, "timingMinutes": None}
    return {"optionCount": 0 if task_type in ("writing", "speaking") else 3, "answerType": "productive" if task_type in ("writing", "speaking") else "single-choice", "playbackCount": None, "timingMinutes": None}


def german_text(level, variant, section, part, item):
    name = NAMES[(item + variant * 3) % len(NAMES)]
    place = PLACES[(item * 2 + variant) % len(PLACES)]
    time = TIMES[(item + variant * 2) % len(TIMES)]
    topic = TOPICS[(item * 3 + variant) % len(TOPICS)]
    if level == "A1":
        return f"Hallo, hier ist {name}. Wir treffen uns bei der {place} um {time}. Bitte bring deine Fahrkarte mit. Bis bald!"
    if level == "A2":
        return f"{name} informiert die Gruppe über {topic}. Der Treffpunkt ist bei der {place}. Beginn ist um {time}; wer später kommt, soll kurz anrufen."
    if level == "B1":
        return f"{name} berichtet über {topic}. Der ursprüngliche Termin bei der {place} wurde auf {time} verschoben, weil mehrere Teilnehmende länger arbeiten müssen. Die Gruppe hält die Änderung für praktisch, obwohl zwei Personen eine frühere Uhrzeit bevorzugen."
    return f"In einem Beitrag bewertet {name} {topic} differenziert. Die Veranstaltung am {place} beginnt um {time}. Befürworter verweisen auf den offenen Austausch, während Kritiker fehlende Planungssicherheit bemängeln. Entscheidend sei deshalb eine transparente Anmeldung mit verbindlicher Rückmeldung."


def options(level, task_type, count, variant, item, answer):
    if count == 0: return None
    if "true-false" in task_type or task_type == "true-false": return ["Richtig", "Falsch"]
    values = [answer]
    for offset in range(1, count):
        if task_type == "matching": values.append(HEADINGS[(item + offset + variant) % len(HEADINGS)])
        elif task_type == "source-selection": values.append(SOURCES[(item + offset + variant) % len(SOURCES)])
        else: values.append(["am Vormittag", "am Nachmittag", "am Abend", "am Wochenende", "in der nächsten Woche"][(item + offset + variant) % 5])
    # Keep the correct answer at a deterministic but varying position.
    shift = (item + variant) % count
    return values[shift:] + values[:shift]


def objective_task(level, variant, section, part, task_type, item, meta):
    text = german_text(level, variant, section, part, item)
    answer = f"{PLACES[(item * 2 + variant) % len(PLACES)]}, {TIMES[(item + variant * 2) % len(TIMES)]}"
    if "true-false" in task_type or task_type == "true-false": answer = "Richtig" if (item + variant) % 2 else "Falsch"
    elif task_type == "matching": answer = HEADINGS[(item + variant) % len(HEADINGS)]
    elif task_type == "source-selection": answer = SOURCES[(item + variant) % len(SOURCES)]
    correct_place = PLACES[(item * 2 + variant) % len(PLACES)]
    correct_time = TIMES[(item + variant * 2) % len(TIMES)]
    wrong_time = TIMES[(item + variant * 2 + 3) % len(TIMES)]
    prompt = {
        "short-answer": f"Wo und wann ist der Treffpunkt von {NAMES[(item + variant * 3) % len(NAMES)]}?",
        "true-false": f"Das Treffen beginnt um {correct_time if answer == 'Richtig' else wrong_time}.",
        "true-false-once": f"Die Ansage nennt {correct_place if answer == 'Richtig' else PLACES[(item * 2 + variant + 3) % len(PLACES)]} als Treffpunkt.",
        "multiple-choice-twice": "Wo und wann findet das Treffen statt?",
        "source-selection": f"Sie suchen aktuelle Informationen über {TOPICS[(item * 3 + variant) % len(TOPICS)]}. Welche Quelle passt?",
        "matching": "Welche Überschrift oder Informationsquelle passt am besten zu diesem Text?",
        "multiple-choice": "Welche Aussage gibt die wichtigste Information richtig wieder?",
    }.get(task_type, "Wählen Sie die richtige Antwort.")
    is_listening = section == "listening"
    return {
        "key": f"v{variant}-{part}-{item:02d}", "section": section, "part": part, "type": task_type,
        "prompt": prompt, "sourceText": None if is_listening else text, "script": text if is_listening else None,
        "speakerMetadata": (f"Deutsch, Niveau {level}; klare natürliche Sprechweise" if is_listening else None),
        "durationTargetSeconds": (18 + min(item, 12) if is_listening else None),
        "playbackCount": meta["playbackCount"], "audioStatus": ("script-ready-audio-deferred" if is_listening else None),
        "correctAnswer": answer, "options": options(level, task_type, meta["optionCount"], variant, item, answer),
        "points": 1, "requiresEvaluation": False
    }


for level in ("A1", "A2", "B1", "B2"):
    path = ROOT / level / "exam-package.json"
    package = json.loads(path.read_text(encoding="utf-8"))
    package["version"] = max(package.get("version", 1), 3)
    part_map = {}
    for section in package["sections"]:
        for part in section["parts"]:
            meta = metadata(level, section["code"], part["key"], part["taskType"])
            part.update(meta)
            part_map[(section["code"], part["key"])] = part
    for variant_index, variant in enumerate(package["mockVariants"], 1):
        old = {(task["section"], task["part"]): task for task in variant["tasks"]}
        tasks = []
        for section in package["sections"]:
            for part in section["parts"]:
                meta = part_map[(section["code"], part["key"])]
                productive = part["taskType"] in ("writing", "speaking")
                if productive:
                    task = dict(old[(section["code"], part["key"])])
                    task["key"] = f"v{variant_index}-{part['key']}-01"
                    task["type"] = part["taskType"]
                    task["playbackCount"] = None
                    tasks.append(task)
                else:
                    tasks.extend(objective_task(level, variant_index, section["code"], part["key"], part["taskType"], i, meta) for i in range(1, part["itemCount"] + 1))
        variant["tasks"] = tasks
    for task in package["practiceBank"]:
        part = part_map[(task["section"], task["part"])]
        task["playbackCount"] = part["playbackCount"]
        task["type"] = part["taskType"]
    path.write_text(json.dumps(package, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

print("TELC A1-B2 fixed mocks expanded deterministically.")
