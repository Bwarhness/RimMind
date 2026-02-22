#!/usr/bin/env python3
"""
Generates and posts/edits the "Testable on Dev" status message in Telegram.
Fires on every push to dev. Edits the same message each time (tracked via
GitHub Actions variable TELEGRAM_STATUS_MESSAGE_ID).
"""

import os, json, subprocess, re, urllib.request, urllib.error
from datetime import datetime, timezone

BOT_TOKEN    = os.environ['TELEGRAM_BOT_TOKEN']
GH_TOKEN     = os.environ['GH_TOKEN']
CHAT_ID      = "-1003732082318"
THREAD_ID    = os.environ['TELEGRAM_DEV_THREAD_ID']
STATUS_MSG_ID = os.environ.get('TELEGRAM_STATUS_MESSAGE_ID', '')
REPO         = "Bwarhness/RimMind"

# ── GitHub API ──────────────────────────────────────────────────────────────

def gh_get(path):
    url = f"https://api.github.com/repos/{REPO}/{path}"
    req = urllib.request.Request(url, headers={
        'Authorization': f'Bearer {GH_TOKEN}',
        'Accept': 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2022-11-28'
    })
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            return json.loads(r.read())
    except Exception as e:
        print(f"  gh_get {path} failed: {e}")
        return None

def gh_set_variable(name, value):
    """Create or update a GitHub Actions repository variable."""
    url = f"https://api.github.com/repos/{REPO}/actions/variables/{name}"
    payload = json.dumps({'name': name, 'value': value}).encode()
    for method in ('PATCH', 'POST'):
        endpoint = url if method == 'PATCH' else f"https://api.github.com/repos/{REPO}/actions/variables"
        req = urllib.request.Request(endpoint, data=payload, headers={
            'Authorization': f'Bearer {GH_TOKEN}',
            'Accept': 'application/vnd.github+json',
            'X-GitHub-Api-Version': '2022-11-28',
            'Content-Type': 'application/json'
        }, method=method)
        try:
            urllib.request.urlopen(req, timeout=10)
            return
        except urllib.error.HTTPError as e:
            if e.code == 404 and method == 'PATCH':
                continue  # variable doesn't exist yet, try POST
            print(f"  gh_set_variable {name} ({method}) failed: {e}")
            return

# ── Telegram API ────────────────────────────────────────────────────────────

def tg(method, data):
    url = f"https://api.telegram.org/bot{BOT_TOKEN}/{method}"
    req = urllib.request.Request(url,
        data=json.dumps(data).encode(),
        headers={'Content-Type': 'application/json'},
        method='POST')
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            return json.loads(r.read())
    except Exception as e:
        print(f"  tg {method} failed: {e}")
        return {'ok': False}

# ── Content generation ──────────────────────────────────────────────────────

def get_unreleased_commits():
    result = subprocess.run(
        ['git', 'log', 'origin/main..HEAD', '--format=%H|%s'],
        capture_output=True, text=True
    )
    commits = []
    for line in result.stdout.strip().split('\n'):
        if '|' in line:
            sha, subject = line.split('|', 1)
            commits.append({'sha': sha.strip(), 'subject': subject.strip()})
    return commits

def get_pr_for_commit(sha):
    data = gh_get(f"commits/{sha}/pulls")
    if data:
        return data[0]
    return None

def extract_testing_section(body):
    """Pull the ## Testing / ## Test Prompts section out of a PR body."""
    if not body:
        return None
    match = re.search(
        r'##\s*Test(?:ing|s?(?:\s+Prompts?)?)\s*\n(.*?)(?=\n##\s|\Z)',
        body, re.DOTALL | re.IGNORECASE
    )
    if not match:
        return None
    raw = match.group(1).strip()
    # Keep lines that look like actual prompts/instructions, drop blank/header lines
    lines = []
    for l in raw.split('\n'):
        l = l.strip().lstrip('- *').strip()
        if l and not l.startswith('#'):
            lines.append(l)
        if len(lines) >= 4:
            break
    return '\n'.join(f'  • {l}' for l in lines) if lines else None

def extract_issue_number(text):
    m = re.search(r'#(\d+)', text or '')
    return m.group(1) if m else None

SKIP_PATTERNS = ['bump version', '[skip ci]', 'chore: bump', 'merge pull request']

def is_noise(subject):
    return any(p in subject.lower() for p in SKIP_PATTERNS)

def current_version():
    result = subprocess.run(
        ['git', 'log', 'HEAD', '-1', '--format=%s'],
        capture_output=True, text=True
    )
    m = re.search(r'(\d+\.\d+\.\d+)', result.stdout)
    return m.group(1) if m else ''

def build_message(commits):
    version = current_version()
    ver_str = f" (v{version})" if version else ""

    lines = [
        f"🧪 *Testable on Dev{ver_str}*",
        "_On the dev Steam branch — not yet released to main_",
        "",
    ]

    features = []
    for c in commits:
        subj = c['subject']
        if is_noise(subj):
            continue

        pr = get_pr_for_commit(c['sha'])
        issue_num = extract_issue_number(subj) or (extract_issue_number(pr.get('body', '') if pr else '') if pr else None)

        prefix = f"#{issue_num} — " if issue_num else ""
        # Strip conventional commit prefix for cleaner display
        display = re.sub(r'^(feat|fix|chore|docs|refactor|ci):\s*', '', subj, flags=re.IGNORECASE)

        entry = [f"✅ *{prefix}{display}*"]

        if pr:
            prompts = extract_testing_section(pr.get('body', ''))
            if prompts:
                entry.append(prompts)

        features.append('\n'.join(entry))

    if features:
        lines.extend(features)
        lines.append("")
    else:
        lines.append("_Nothing new since last release_\n")

    now = datetime.now(timezone.utc).strftime('%Y\\-%m\\-%d %H:%M UTC')
    lines.append(f"_Last updated: {now}_")

    return '\n'.join(lines)

# ── Main ────────────────────────────────────────────────────────────────────

def main():
    print(f"Thread ID: {THREAD_ID}")
    print(f"Existing message ID: {STATUS_MSG_ID or 'none'}")

    commits = get_unreleased_commits()
    print(f"Unreleased commits: {len(commits)}")

    message = build_message(commits)
    print("── Generated message ──")
    print(message)
    print("───────────────────────")

    new_msg_id = None

    # Try editing existing message first
    if STATUS_MSG_ID:
        result = tg('editMessageText', {
            'chat_id': CHAT_ID,
            'message_id': int(STATUS_MSG_ID),
            'text': message,
            'parse_mode': 'MarkdownV2'
        })
        if result.get('ok'):
            print(f"Edited existing message {STATUS_MSG_ID}")
            new_msg_id = STATUS_MSG_ID

    # Post new if no existing or edit failed
    if not new_msg_id:
        result = tg('sendMessage', {
            'chat_id': CHAT_ID,
            'message_thread_id': int(THREAD_ID),
            'text': message,
            'parse_mode': 'MarkdownV2'
        })
        if result.get('ok'):
            new_msg_id = str(result['result']['message_id'])
            print(f"Posted new message {new_msg_id}")
        else:
            print(f"Failed to post: {result}")
            return

    # Update stored message ID if it changed
    if new_msg_id and new_msg_id != STATUS_MSG_ID:
        gh_set_variable('TELEGRAM_STATUS_MESSAGE_ID', new_msg_id)
        print(f"Updated TELEGRAM_STATUS_MESSAGE_ID → {new_msg_id}")

    print("Done.")

if __name__ == '__main__':
    main()
