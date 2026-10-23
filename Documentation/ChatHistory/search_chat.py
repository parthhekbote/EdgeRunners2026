import os
import sys
import json
import re

LOG_FILE = os.path.join(os.path.dirname(__file__), "GEMINI_FULL_CHAT_LOG.md")
JSON_FILE = os.path.join(os.path.dirname(__file__), "transcript.jsonl")

def search(query):
    query_lower = query.lower()
    print(f"Searching conversation history for: '{query}'...\n" + "=" * 60)
    
    if not os.path.exists(JSON_FILE):
        print(f"Transcript file not found: {JSON_FILE}")
        return

    matches = 0
    with open(JSON_FILE, "r", encoding="utf-8") as f:
        for line in f:
            try:
                data = json.loads(line)
            except Exception:
                continue

            t = data.get("type")
            content = data.get("content", "")
            if query_lower in content.lower():
                matches += 1
                source = data.get("source", "")
                created = data.get("created_at", "")
                
                # Clean up request tags
                req_match = re.search(r"<USER_REQUEST>(.*?)</USER_REQUEST>", content, re.DOTALL)
                clean_text = req_match.group(1).strip() if req_match else content.strip()
                
                print(f"[{matches}] Type: {t} | Source: {source} | Time: {created}")
                print(clean_text[:300] + ("..." if len(clean_text) > 300 else ""))
                print("-" * 60)

    print(f"\nTotal matches found: {matches}")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python search_chat.py <search_query>")
    else:
        search(" ".join(sys.argv[1:]))
