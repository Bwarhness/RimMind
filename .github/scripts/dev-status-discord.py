#!/usr/bin/env python3
"""
Generates and posts/edits the "Testable on Dev" status message in Discord.
Fires on every push to dev. Edits the same message each time (tracked via
GitHub Actions variable DISCORD_STATUS_MESSAGE_ID).
"""

import os, json, subprocess, re, urllib.request, urllib.error
from datetime import datetime, timezone

BOT_TOKEN  = os.environ['DISCORD_BOT_TOKEN']
GH_TOKEN   = os.environ['GH_TOKEN']
CHANNEL_ID = os.environ['DISCORD_STATUS_CHANNEL_ID']
MSG_ID     = os.environ.get('DISCORD_STATUS_MESSAGE_ID', '')
REPO       = "Bwarhness/RimMind"
DEFAULT_BRANCH = "main"

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
            print(f"  Set variable {name} = {value}")
            return
        except urllib.error.HTTPError as e:
            if e.code == 404 and method == 'PATCH':
                continue
            print(f"  gh_set_variable {name} ({method}) failed: {e}")
            return

# ── Discord API ─────────────────────────────────────────────────────────────

def discord_request(method, path, data=None):
    url = f"https://discord.com/api/v10{path}"
    body = json.dumps(data).encode() if data else None
    req = urllib.request.Request(url, data=body, headers={
        'Authorization': f'Bot {BOT_TOKEN}',
        'Content-Type': 'application/json',
    }, method=method)
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        body = e.read().decode()
        print(f"  Discord {method} {path} failed: {e.code} {body}")
        return None
    except Exception as e:
        print(f"  Discord {method} {path} failed: {e}")
        return None

# ── Content extraction ──────────────────────────────────────────────────────

def extract_example_prompts(body, max_prompts=3):
    if not body:
        return []
    for pattern in [
        r'##\s*Example\s+Test\s+Prompts?\s*\n(.*?)(?=\n##\s|\Z)',
        r'##\s*Test(?:ing|s?(?:\s+Prompts?)?)\s*\n(.*?)(?=\n##\s|\Z)',
    ]:
        match = re.search(pattern, body, re.DOTALL | re.IGNORECASE)
        if match:
            raw = match.group(1).strip()
            prompts = []
            for line in raw.split('\n'):
                line = re.sub(r'^[-*>\s]+', '', line).strip()
                line = re.sub(r'[*_`]', '', line)
                line = re.sub(r'\s*—.*$', '', line)
                line = line.strip('"\'').strip()
                if line and not line.startswith('#') and len(line) > 5:
                    prompts.append(line)
                if len(prompts) >= max_prompts:
                    break
            if prompts:
                return prompts
    return []

def get_pr_for_commit(sha):
    data = gh_get(f"commits/{sha}/pulls")
    if data:
        return data[0]
    return None

def get_issue(number):
    return gh_get(f"issues/{number}")

def extract_issue_number(text):
    m = re.search(r'(?:closes?|fixes?|resolves?)\s+#(\d+)', text or '', re.IGNORECASE)
    if m:
        return m.group(1)
    m = re.search(r'#(\d+)', text or '')
    return m.group(1) if m else None

# ── Git helpers ─────────────────────────────────────────────────────────────

def get_unreleased_commits():
    result = subprocess.run(
        ['git', 'log', f'origin/{DEFAULT_BRANCH}..HEAD', '--format=%H|%s'],
        capture_output=True, text=True
    )
    commits = []
    for line in result.stdout.strip().split('\n'):
        if '|' in line:
            sha, subject = line.split('|', 1)
            commits.append({'sha': sha.strip(), 'subject': subject.strip()})
    return commits

def get_version():
    result = subprocess.run(
        ['git', 'show', 'HEAD:About/About.xml'],
        capture_output=True, text=True
    )
    m = re.search(r'<version>([^<]+)</version>', result.stdout, re.IGNORECASE)
    if m:
        return m.group(1).strip()
    result2 = subprocess.run(
        ['git', 'log', 'HEAD', '-5', '--format=%s'],
        capture_output=True, text=True
    )
    m2 = re.search(r'(\d+\.\d+\.\d+)', result2.stdout)
    return m2.group(1) if m2 else ''

SKIP_PATTERNS = [
    'bump version', '[skip ci]', 'chore: bump', 'merge pull request',
    'merge branch', 'merge remote', 'add using system', 'ensure all fixes',
    'add missing using', 'add using', 'merge community', 'sanitize ci',
    'clarify version', 'add version management', 'add dev workshop',
    'trigger version', 'add steam workshop', 'test version',
    'translations docs', 'claude.md', 'add auto-update script',
    'custom provider routing', 'version bump workflow',
]

REQUIRE_PREFIX = re.compile(r'^(feat|fix)(?:\([^)]+\))?:', re.IGNORECASE)

def is_noise(subject):
    s = subject.lower()
    if any(p in s for p in SKIP_PATTERNS):
        return True
    if not REQUIRE_PREFIX.match(subject) and not re.search(r'#\d+', subject):
        return True
    return False

MAX_FEATURES = 8
MAX_CHARS = 1900  # Discord limit is 2000, leave headroom

# ── Message builder ─────────────────────────────────────────────────────────

def build_message(commits):
    version = get_version()

    lines = ["🧪 **Testable on Dev**", ""]
    if version:
        lines += [f"📦 **Version: {version}**",
                  "*Switch to the **dev** Steam beta branch, then verify your version matches above.*",
                  ""]

    features = []
    seen_issues = set()

    for c in commits:
        subj = c['subject']
        if is_noise(subj):
            continue
        if len(features) >= MAX_FEATURES:
            break

        pr = get_pr_for_commit(c['sha'])
        pr_body = pr.get('body', '') if pr else ''

        issue_num = extract_issue_number(subj) or extract_issue_number(pr_body)
        if issue_num and issue_num in seen_issues:
            continue
        if issue_num:
            seen_issues.add(issue_num)

        display = re.sub(r'^(feat|fix|chore|docs|refactor|ci)(?:\([^)]+\))?:\s*', '', subj, flags=re.IGNORECASE)
        display = re.sub(r'(\s*\(#\d+\))+\s*$', '', display).strip()
        prefix = f"#{issue_num} — " if issue_num else ""

        entry = [f"✅ **{prefix}{display}**"]

        prompts = []
        if issue_num:
            issue = get_issue(issue_num)
            if issue:
                prompts = extract_example_prompts(issue.get('body', ''))
        if not prompts and pr_body:
            prompts = extract_example_prompts(pr_body)

        if prompts:
            entry.append("*Try these:*")
            for p in prompts:
                entry.append(f'  💬 *"{p}"*')

        features.append('\n'.join(entry))

    if features:
        lines.extend(features)
        lines.append("")
    else:
        lines.append("*Nothing new since last release*\n")

    now = datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC')
    lines.append(f"*Updated: {now}*")

    msg = '\n'.join(lines)
    if len(msg) > MAX_CHARS:
        msg = msg[:MAX_CHARS] + "\n*…(truncated)*"
    return msg

# ── Main ────────────────────────────────────────────────────────────────────

def main():
    print(f"Channel ID: {CHANNEL_ID}")
    print(f"Existing message ID: {MSG_ID or 'none'}")

    commits = get_unreleased_commits()
    print(f"Unreleased commits: {len(commits)}")

    message = build_message(commits)
    print("── Generated message ──")
    print(message)
    print("───────────────────────")

    new_msg_id = None

    if MSG_ID:
        result = discord_request('PATCH', f'/channels/{CHANNEL_ID}/messages/{MSG_ID}',
                                 {'content': message})
        if result and result.get('id'):
            print(f"Edited existing message {MSG_ID}")
            new_msg_id = MSG_ID
        else:
            print("Edit failed — posting new message")

    if not new_msg_id:
        result = discord_request('POST', f'/channels/{CHANNEL_ID}/messages',
                                 {'content': message})
        if result and result.get('id'):
            new_msg_id = str(result['id'])
            print(f"Posted new message {new_msg_id}")
        else:
            print("Failed to post message")
            return

    if new_msg_id and new_msg_id != MSG_ID:
        gh_set_variable('DISCORD_STATUS_MESSAGE_ID', new_msg_id)
        print(f"Updated DISCORD_STATUS_MESSAGE_ID → {new_msg_id}")

    print("Done.")

if __name__ == '__main__':
    main()
