import json
from pathlib import Path

ROOT = Path(__file__).parents[1] / "src/Unidemix.Api/Content/Learning/German"

for level in ("A1", "A2"):
    for path in sorted((ROOT / level).glob("lesson-*.json")):
        lesson = json.loads(path.read_text(encoding="utf-8"))
        activities = lesson.get("activities", [])
        introduction = next((item for item in activities if item.get("type") == "introduction"), None)
        if not introduction:
            continue
        outcomes = lesson.get("canDoObjectives", [])[:4]
        outcome_lines = "\n".join(f"• {outcome}" for outcome in outcomes)
        introduction["prompt"] = (
            "در این درس چه یاد می‌گیرم؟\n\n"
            f"{lesson.get('description', '')}\n\n"
            f"{outcome_lines}"
        ).strip()
        lesson["activities"] = [
            item for item in activities
            if item is introduction or item.get("type") != "dialogue-context"
        ]
        path.write_text(json.dumps(lesson, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

print("Reduced A1/A2 guided lessons to one concise opening stage.")
