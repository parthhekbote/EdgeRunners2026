import os
import json
import re
import shutil
from datetime import datetime, timezone

CONVERSATION_ID = "e435c426-d83c-40b2-8ade-57eec1271d17"
LOGS_DIR = rf"C:\Users\LEGION\.gemini\antigravity-ide\brain\{CONVERSATION_ID}\.system_generated\logs"
OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Documentation", "ChatHistory")
OUTPUT_DIR = os.path.abspath(OUTPUT_DIR)

os.makedirs(OUTPUT_DIR, exist_ok=True)

src_full = os.path.join(LOGS_DIR, "transcript_full.jsonl")
src_compact = os.path.join(LOGS_DIR, "transcript.jsonl")
dst_full = os.path.join(OUTPUT_DIR, "transcript_full.jsonl")
dst_compact = os.path.join(OUTPUT_DIR, "transcript.jsonl")

print(f"Copying JSONL transcripts to {OUTPUT_DIR}...")
if os.path.exists(src_full):
    shutil.copy2(src_full, dst_full)
    print(f"Copied transcript_full.jsonl ({os.path.getsize(dst_full):,} bytes)")

if os.path.exists(src_compact):
    shutil.copy2(src_compact, dst_compact)
    print(f"Copied transcript.jsonl ({os.path.getsize(dst_compact):,} bytes)")

# Parse and create readable markdown chat log
print("Parsing transcript into Markdown...")
markdown_path = os.path.join(OUTPUT_DIR, "GEMINI_FULL_CHAT_LOG.md")

turns = []
current_user = None
current_tools = []
current_assistant = None

with open(src_full, "r", encoding="utf-8") as f:
    for line in f:
        try:
            item = json.loads(line)
        except Exception:
            continue

        item_type = item.get("type")
        source = item.get("source")
        time_str = item.get("created_at", "")
        content = item.get("content", "")

        if item_type == "USER_INPUT":
            # If we had a prior turn in flight, flush it
            if current_user or current_assistant:
                turns.append({
                    "user": current_user,
                    "tools": current_tools,
                    "assistant": current_assistant
                })
                current_tools = []
                current_assistant = None

            # Extract user request cleanly
            req_match = re.search(r"<USER_REQUEST>(.*?)</USER_REQUEST>", content, re.DOTALL)
            user_text = req_match.group(1).strip() if req_match else content.strip()
            current_user = {
                "time": time_str,
                "text": user_text
            }

        elif item_type == "PLANNER_RESPONSE":
            tool_calls = item.get("tool_calls", [])
            if tool_calls:
                for tc in tool_calls:
                    name = tc.get("name", "tool")
                    args = tc.get("args", {})
                    # create compact summary
                    summary = args.get("toolSummary") or args.get("toolAction") or args.get("Title") or ""
                    if not summary:
                        if "CommandLine" in args:
                            summary = f"Run: `{args['CommandLine'][:60]}`"
                        elif "TargetFile" in args:
                            summary = f"Edit: `{os.path.basename(args['TargetFile'])}`"
                        elif "AbsolutePath" in args:
                            summary = f"View: `{os.path.basename(args['AbsolutePath'])}`"
                        elif "Query" in args:
                            summary = f"Search: `{args['Query']}`"
                        else:
                            summary = f"Call `{name}`"
                    current_tools.append({
                        "name": name,
                        "summary": summary,
                        "time": time_str
                    })

            if content:
                # Assistant text response
                if current_assistant is None:
                    current_assistant = {
                        "time": time_str,
                        "text": content
                    }
                else:
                    current_assistant["text"] += "\n\n" + content

# Flush final turn
if current_user or current_assistant:
    turns.append({
        "user": current_user,
        "tools": current_tools,
        "assistant": current_assistant
    })

print(f"Processed {len(turns)} conversation turns.")

# Write GEMINI_FULL_CHAT_LOG.md
with open(markdown_path, "w", encoding="utf-8") as out:
    out.write("# Gemini / Antigravity Full Session Chat Log\n\n")
    out.write(f"**Conversation ID**: `{CONVERSATION_ID}`  \n")
    out.write(f"**Exported At**: `{datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M:%S UTC')}`  \n")
    out.write(f"**Total Conversation Turns**: `{len(turns)}`  \n")
    out.write(f"**Raw Transcripts**: [transcript_full.jsonl](./transcript_full.jsonl) | [transcript.jsonl](./transcript.jsonl)  \n\n")
    out.write("---\n\n")

    # Table of Contents
    out.write("## Table of Contents\n\n")
    for i, turn in enumerate(turns):
        u = turn.get("user")
        if u and u.get("text"):
            # first 60 chars of prompt
            snippet = u["text"].split("\n")[0][:70].replace("[", "").replace("]", "")
            out.write(f"- [Turn {i+1}: {snippet}](#turn-{i+1})\n")
        else:
            out.write(f"- [Turn {i+1}: Initial System Setup](#turn-{i+1})\n")
    out.write("\n---\n\n")

    # Turns
    for i, turn in enumerate(turns):
        out.write(f"## Turn {i+1}\n\n")
        u = turn.get("user")
        if u:
            out.write(f"### 👤 User Prompt ({u.get('time', '')})\n\n")
            out.write(f"```text\n{u.get('text', '')}\n```\n\n")

        tools = turn.get("tools", [])
        if tools:
            out.write("<details>\n<summary>🛠️ <b>Actions & Tools Executed (" + str(len(tools)) + ")</b></summary>\n\n")
            for t in tools:
                out.write(f"- `{t['name']}`: {t['summary']}\n")
            out.write("\n</details>\n\n")

        a = turn.get("assistant")
        if a and a.get("text"):
            out.write(f"### 🤖 Assistant Response ({a.get('time', '')})\n\n")
            out.write(f"{a.get('text', '')}\n\n")

        out.write("---\n\n")

print(f"Generated {markdown_path} ({os.path.getsize(markdown_path):,} bytes)")
