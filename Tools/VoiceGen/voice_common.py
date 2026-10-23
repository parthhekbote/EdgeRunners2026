"""Shared helpers for the dialogue voice pipeline.

The key function MUST stay identical to DialogueVoice.Key in Assets/Scripts/Audio/DialogueVoice.cs.
"""
import hashlib
import re

# The same rule CyberpunkDialogueHUD.ShowDialogue uses to split "Speaker: body"
def strip_speaker_prefix(text: str):
    text = text.strip()
    idx = text.find(":")
    if 0 < idx < 35:
        cand = text[:idx].strip()
        if "<" not in cand and "\n" not in cand and "http" not in cand:
            return cand, text[idx + 1:].strip()
    return None, text


def normalize(text: str) -> str:
    text = re.sub(r"<[^>]+>", "", text)          # rich text tags
    _, body = strip_speaker_prefix(text)
    if len(body) >= 2 and body.startswith('"') and body.endswith('"'):
        body = body[1:-1]
    return re.sub(r"[^a-z0-9]", "", body.lower())


def key(text: str) -> str:
    return hashlib.sha1(normalize(text).encode("utf-8")).hexdigest()[:16]


def spoken_text(text: str) -> str:
    """What the voice should actually say: no tags, no speaker prefix, no surrounding quotes."""
    text = re.sub(r"<[^>]+>", "", text)
    _, body = strip_speaker_prefix(text)
    if len(body) >= 2 and body.startswith('"') and body.endswith('"'):
        body = body[1:-1]
    body = body.replace("\\n", " ").replace("\n", " ")
    # symbols and markers the voice would read out loud or stumble on
    body = body.replace("►", "").replace("▲", "").replace("//", ",").replace("&", " and ")
    body = re.sub(r"\[[^\]]*\]", "", body)                 # [E] key prompts, [0/4] counters
    body = body.replace("—", ", ").replace("–", ", ").replace(" - ", ", ")
    body = re.sub(r"\bURM-?(\d+)\b", lambda m: "U R M " + " ".join(m.group(1)), body)
    body = body.replace("Omni-Corp", "Omni Corp")

    # SHOUTED WORDS are read as spelled-out acronyms by some voices: turn them into normal words
    letters = [c for c in body if c.isalpha()]
    if letters and sum(c.isupper() for c in letters) / len(letters) > 0.6:
        # a fully shouted line: read it as a normal sentence, keeping sentence starts capitalised
        low = body.lower()
        body = re.sub(r"(^|[.!?]\s+)([a-z])", lambda m: m.group(1) + m.group(2).upper(), low)
    else:
        def calm(m):
            w = m.group(0)
            return w.capitalize() if len(w) > 3 else w
        body = re.sub(r"\b[A-Z]{4,}\b", calm, body)

    body = re.sub(r"\s+", " ", body).strip()
    body = re.sub(r"\s+([,.!?])", r"\1", body)
    return body
