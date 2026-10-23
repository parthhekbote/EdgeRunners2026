"""Collects every spoken line the game can show in a dialogue box and writes Tools/VoiceGen/manifest.json.

Sources:
  * C# calls: DialogueLine / DialogueLineData / CutsceneLine / Line / ShowTransmission / ShowSubtitle /
    ShowDialogue / EnqueueSub, DialogueEntry.dialogueText initialisers, and SpawnNPC(... dialogue: new string[] {...})
  * Scene files: serialized `dialogueText:` entries (NPC dialogue baked into scenes)
  * VoiceLines/missing.tsv: lines the game logged at runtime because no clip existed

Only plain string literals are collected; interpolated strings ($"...{x}") cannot be pre-recorded.
"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
from voice_common import key, spoken_text, strip_speaker_prefix  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ASSETS = os.path.join(ROOT, "Assets")
SKIP_DIRS = {"MCPForUnity", "Synty", "Packages", "Plugins", "TextMesh Pro", "Library"}

# call name -> (speaker arg index or None, text arg index)
CALLS = {
    "DialogueLine": (0, 2),
    "DialogueLineData": (0, 1),
    "CutsceneLine": (0, 2),
    "Line": (0, 2),
    "ShowTransmission": (0, 2),
    "ShowSubtitle": (0, 1),
    "ShowDialogue": (0, 1),
    "EnqueueSub": (0, 2),
    "TriggerCockpitTransmission": (0, 2),
    "PlayExternalDialogue": (0, 1),
}

ESCAPES = {"n": "\n", "t": "\t", "r": "", '"': '"', "\\": "\\", "'": "'", "0": ""}


def decode_literal(body: str) -> str:
    out, i = [], 0
    while i < len(body):
        c = body[i]
        if c == "\\" and i + 1 < len(body):
            n = body[i + 1]
            if n == "u" and i + 5 < len(body) + 0:
                try:
                    out.append(chr(int(body[i + 2:i + 6], 16)))
                    i += 6
                    continue
                except ValueError:
                    pass
            out.append(ESCAPES.get(n, n))
            i += 2
        else:
            out.append(c)
            i += 1
    return "".join(out)


def read_string_literal(src, i):
    """src[i] is the opening quote. Returns (decoded, next_index, interpolated)."""
    interp = False
    verbatim = False
    j = i
    # prefix chars are handled by caller; here src[i] == '"'
    j = i + 1
    start = j
    while j < len(src):
        c = src[j]
        if c == "\\" :
            j += 2
            continue
        if c == '"':
            break
        j += 1
    body = src[start:j]
    return decode_literal(body), j + 1


def split_args(src, open_idx):
    """src[open_idx] == '('. Returns (list of raw arg strings, index after ')')."""
    depth = 0
    args, cur = [], []
    i = open_idx
    n = len(src)
    while i < n:
        c = src[i]
        if c == '"':
            # copy string token verbatim
            j = i + 1
            while j < n:
                if src[j] == "\\":
                    j += 2
                    continue
                if src[j] == '"':
                    break
                j += 1
            cur.append(src[i:j + 1])
            i = j + 1
            continue
        if c == "/" and i + 1 < n and src[i + 1] == "/":
            while i < n and src[i] != "\n":
                i += 1
            continue
        if c == "/" and i + 1 < n and src[i + 1] == "*":
            end = src.find("*/", i + 2)
            i = n if end < 0 else end + 2
            continue
        if c in "([{":
            depth += 1
            if depth == 1 and c == "(":
                i += 1
                continue
            cur.append(c)
        elif c in ")]}":
            depth -= 1
            if depth == 0:
                args.append("".join(cur).strip())
                return args, i + 1
            cur.append(c)
        elif c == "," and depth == 1:
            args.append("".join(cur).strip())
            cur = []
        else:
            cur.append(c)
        i += 1
    return args, n


def eval_literal_expr(arg):
    """'a' + 'b' or a single literal -> string, otherwise None (variables, interpolation)."""
    arg = re.sub(r"^\s*[A-Za-z_]\w*\s*:\s*(?!:)", "", arg.strip())
    parts, i, n = [], 0, len(arg)
    expect_literal = True
    while i < n:
        c = arg[i]
        if c.isspace():
            i += 1
            continue
        if expect_literal:
            if c == "$":
                return None
            if c == "@" and i + 1 < n and arg[i + 1] == '"':
                j = arg.find('"', i + 2)
                parts.append(arg[i + 2:j])
                i = j + 1
            elif c == '"':
                lit, i = read_string_literal(arg, i)
                parts.append(lit)
            else:
                return None
            expect_literal = False
        else:
            if c != "+":
                return None
            expect_literal = True
            i += 1
    if expect_literal or not parts:
        return None
    return "".join(parts)


def literal_strings(src):
    out, i, n = [], 0, len(src)
    while i < n:
        c = src[i]
        if c == '"':
            prev = src[i - 1] if i else ""
            if prev == "$":
                # skip interpolated
                _, i = read_string_literal(src, i)
                continue
            lit, i = read_string_literal(src, i)
            out.append(lit)
        elif c == "/" and i + 1 < n and src[i + 1] == "/":
            while i < n and src[i] != "\n":
                i += 1
        else:
            i += 1
    return out


entries = {}


def add(speaker, text, source):
    if text is None:
        return
    text = text.strip()
    if len(text) < 3 or "{" in text and "}" in text:
        return
    body = spoken_text(text)
    if len(re.sub(r"[^A-Za-z]", "", body)) < 3:
        return
    prefix_speaker, _ = strip_speaker_prefix(re.sub(r"<[^>]+>", "", text))
    spk = (speaker or "").strip() or prefix_speaker or ""
    k = key(text)
    e = entries.get(k)
    if e is None:
        entries[k] = {"key": k, "speaker": spk, "text": body, "sources": [source]}
    else:
        if not e["speaker"] and spk:
            e["speaker"] = spk
        if source not in e["sources"]:
            e["sources"].append(source)


CALL_RE = re.compile(r"\b(" + "|".join(CALLS) + r")\s*\(")
ENTRY_RE = re.compile(r"\bdialogueText\s*=\s*")
SPAWN_RE = re.compile(r"\bSpawnNPC\s*\(")
DIALOGUE_ARR_RE = re.compile(r"dialogue\s*:\s*new\s+string\s*\[\s*\]\s*\{", re.S)


BW_SPEAKER = re.compile(r'\bspeaker\s*=\s*"([^"]*)"\s*;')
BW_TEXT = re.compile(r'\btext\s*=\s*("(?:[^"\\]|\\.)*")\s*;', re.S)


def scan_beat_assignments(src, rel):
    """BlackwallStoryDialogueHUD style: speaker = "..."; ... text = "..."; inside switch cases."""
    pos, last_speaker = 0, ""
    events = [(m.start(), "s", m.group(1)) for m in BW_SPEAKER.finditer(src)]
    events += [(m.start(), "t", m.group(1)) for m in BW_TEXT.finditer(src)]
    for _, kind, val in sorted(events):
        if kind == "s":
            last_speaker = val
        else:
            lit = eval_literal_expr(val)
            if lit:
                add(re.sub(r"[/]", "", last_speaker).strip(), lit, rel)


def scan_cs(path):
    src = open(path, encoding="utf-8", errors="ignore").read()
    rel = os.path.relpath(path, ROOT).replace("\\", "/")
    if "BlackwallStoryDialogueHUD" in os.path.basename(path):
        scan_beat_assignments(src, rel)

    for m in CALL_RE.finditer(src):
        name = m.group(1)
        # skip declarations: "public DialogueLineData(" / "static ... Line("
        before = src[max(0, m.start() - 40):m.start()]
        if re.search(r"(public|private|static|void|class|string)\s+[\w<>\[\]\.]*\s*$", before) and name in ("Line", "CutsceneLine", "DialogueLine", "DialogueLineData"):
            if not re.search(r"new\s+[\w\.]*\s*$", before):
                continue
        args, _ = split_args(src, m.end() - 1)
        si, ti = CALLS[name]
        if name == "ShowSubtitle" and len(args) >= 1:
            # overloads: (raw, ...) or (speaker, text, ...)
            first = eval_literal_expr(args[0]) if args else None
            second = eval_literal_expr(args[1]) if len(args) > 1 else None
            if second is not None and first is not None and ":" not in first[:35]:
                add(first, second, rel)
            elif first is not None:
                add(None, first, rel)
            continue
        if len(args) <= max(si, ti):
            continue
        speaker = eval_literal_expr(args[si]) if si is not None else None
        text = eval_literal_expr(args[ti])
        if text is not None:
            add(speaker, text, rel)

    for m in ENTRY_RE.finditer(src):
        # value runs to the next ',' or '}' at depth 0 within the initializer
        rest = src[m.end():m.end() + 1500]
        depth, end = 0, len(rest)
        j = 0
        while j < len(rest):
            c = rest[j]
            if c == '"':
                k = j + 1
                while k < len(rest):
                    if rest[k] == "\\":
                        k += 2
                        continue
                    if rest[k] == '"':
                        break
                    k += 1
                j = k + 1
                continue
            if c in "([{":
                depth += 1
            elif c in ")]}":
                if depth == 0:
                    end = j
                    break
                depth -= 1
            elif c == "," and depth == 0:
                end = j
                break
            j += 1
        add(None, eval_literal_expr(rest[:end]), rel)

    for m in SPAWN_RE.finditer(src):
        args, end_idx = split_args(src, m.end() - 1)
        call = src[m.start():end_idx]
        name = eval_literal_expr(args[1]) if len(args) > 1 else None
        am = DIALOGUE_ARR_RE.search(call)
        if am and name:
            body_start = am.end()
            depth, j = 1, body_start
            while j < len(call) and depth:
                if call[j] == '"':
                    k = j + 1
                    while k < len(call):
                        if call[k] == "\\":
                            k += 2
                            continue
                        if call[k] == '"':
                            break
                        k += 1
                    j = k
                elif call[j] == "{":
                    depth += 1
                elif call[j] == "}":
                    depth -= 1
                j += 1
            arr = call[body_start:j - 1]
            for lit in literal_strings(arr):
                add(name, lit, rel)


YAML_DLG = re.compile(r"^\s*dialogueText:\s*(.+?)\s*$")
YAML_NAME = re.compile(r"^\s*characterName:\s*(.+?)\s*$")


def yaml_scalar(v):
    if v.startswith("'") and v.endswith("'") and len(v) >= 2:
        return v[1:-1].replace("''", "'")
    if v.startswith('"') and v.endswith('"') and len(v) >= 2:
        return decode_literal(v[1:-1])
    return v


def scan_scene(path):
    rel = os.path.relpath(path, ROOT).replace("\\", "/")
    last_name = ""
    with open(path, encoding="utf-8", errors="ignore") as f:
        for line in f:
            m = YAML_NAME.match(line)
            if m:
                last_name = yaml_scalar(m.group(1))
                continue
            m = YAML_DLG.match(line)
            if m:
                add(last_name, yaml_scalar(m.group(1)), rel)


def scan_missing():
    p = os.path.join(ROOT, "VoiceLines", "missing.tsv")
    if not os.path.exists(p):
        return
    for line in open(p, encoding="utf-8", errors="ignore"):
        parts = line.rstrip("\n").split("\t")
        if len(parts) >= 2 and parts[1].strip():
            add(parts[0], parts[1], "runtime-missing")


def main():
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            p = os.path.join(dirpath, fn)
            if fn.endswith(".cs"):
                scan_cs(p)
            elif fn.endswith(".unity") and os.path.getsize(p) < 80 * 1024 * 1024:
                scan_scene(p)
    scan_missing()

    items = sorted(entries.values(), key=lambda e: (e["speaker"].lower(), e["text"]))
    out = os.path.join(os.path.dirname(__file__), "manifest.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(items, f, indent=1, ensure_ascii=False)

    speakers = {}
    for e in items:
        speakers[e["speaker"] or "(none)"] = speakers.get(e["speaker"] or "(none)", 0) + 1
    print(f"{len(items)} unique lines -> {out}")
    for s, c in sorted(speakers.items(), key=lambda x: -x[1])[:45]:
        print(f"  {c:4d}  {s}")


if __name__ == "__main__":
    main()
