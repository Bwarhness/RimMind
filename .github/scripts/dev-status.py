#!/usr/bin/env python3
"""
Generates and posts/edits the "Testable on Dev" status message in Telegram.
Fires on every push to dev. Edits the same message each time (tracked via
GitHub Actions variable TELEGRAM_STATUS_MESSAGE_ID).
"""

import os, json, subprocess, re, urllib.request, urllib.error
from datetime import datetime, timezone

BOT_TOKEN     = os.environ['TELEGRAM_BOT_TOKEN']
GH_TOKEN      = os.environ['GH_TOKEN']
CHAT_ID       = "-1003732082318"
THREAD_ID     = os.environ['TELEGRAM_DEV_THREAD_ID']
STATUS_MSG_ID = os.environ.get('TELEGRAM_STATUS_MESSAGE_ID', '')
REPO          = "Bwarhness/RimMind"
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
            return
        except urllib.error.HTTPError as e:
            if e.code == 404 and method == 'PATCH':
                continue
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

def html_escape(text):
    return text.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')

# ── Content extraction ──────────────────────────────────────────────────────

def extract_example_prompts(body, max_prompts=3):
    """Extract prompts from '## Example Test Prompts' or '## Testing' section."""
    if not body:
        return []
    # Try Example Test Prompts first, then Testing
    for pattern in [
        r'##\s*Example\s+Test\s+Prompts?\s*\n(.*?)(?=\n##\s|\Z)',
        r'##\s*Test(?:ing|s?(?:\s+Prompts?)?)\s*\n(.*?)(?=\n##\s|\Z)',
    ]:
        match = re.search(pattern, body, re.DOTALL | re.IGNORECASE)
        if match:
            raw = match.group(1).strip()
            prompts = []
            for line in raw.split('\n'):
                # Strip markdown bullets, italic/bold markers, and quotes
                line = re.sub(r'^[-*>\s]+', '', line).strip()   # leading bullets
                line = re.sub(r'[*_`]', '', line)               # markdown formatting
                line = re.sub(r'\s*—.*$', '', line)             # strip "— annotation" suffixes
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
    # Prefer "closes/fixes #NNN" style, fall back to bare #NNN
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
    """Read version from About/About.xml on HEAD."""
    result = subprocess.run(
        ['git', 'show', 'HEAD:About/About.xml'],
        capture_output=True, text=True
    )
    m = re.search(r'<version>([^<]+)</version>', result.stdout, re.IGNORECASE)
    if m:
        return m.group(1).strip()
    # Fallback: scan recent commit messages
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

# Only show commits that introduce user-facing features or bug fixes
REQUIRE_PREFIX = re.compile(r'^(feat|fix)(?:\([^)]+\))?:', re.IGNORECASE)

def is_noise(subject):
    s = subject.lower()
    if any(p in s for p in SKIP_PATTERNS):
        return True
    # Skip anything that isn't a feat: or fix: (docs, chore, ci, refactor, etc.)
    # unless it has an issue number reference (manually written commit)
    if not REQUIRE_PREFIX.match(subject) and not re.search(r'#\d+', subject):
        return True
    return False

MAX_FEATURES = 8

# ── Message builder ─────────────────────────────────────────────────────────

def build_message(commits):
    version = get_version()

    lines = [
        f"🧪 <b>Testable on Dev</b>",
        "",
        f"📦 <b>Version: {html_escape(version)}</b>" if version else "",
        "<i>Switch to the <b>dev</b> Steam beta branch, then verify your version matches above.</i>",
        "",
    ]
    # Remove blank lines from version block if no version
    lines = [l for l in lines if l != ""]
    lines.append("")

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

        # Clean up display title
        display = re.sub(r'^(feat|fix|chore|docs|refactor|ci)(?:\([^)]+\))?:\s*', '', subj, flags=re.IGNORECASE)
        display = re.sub(r'(\s*\(#\d+\))+\s*$', '', display).strip()
        prefix = f"#{issue_num} — " if issue_num else ""

        entry = [f"✅ <b>{html_escape(prefix + display)}</b>"]

        # Get example prompts — issue body first, PR body as fallback
        prompts = []
        if issue_num:
            issue = get_issue(issue_num)
            if issue:
                prompts = extract_example_prompts(issue.get('body', ''))
        if not prompts and pr_body:
            prompts = extract_example_prompts(pr_body)

        if prompts:
            entry.append("<i>Try these:</i>")
            for p in prompts:
                entry.append(f'  💬 <i>"{html_escape(p)}"</i>')

        features.append('\n'.join(entry))

    if features:
        lines.extend(features)
        lines.append("")
    else:
        lines.append("<i>Nothing new since last release</i>\n")

    now = datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC')
    lines.append(f"<i>Updated: {now}</i>")

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

    if STATUS_MSG_ID:
        result = tg('editMessageText', {
            'chat_id': CHAT_ID,
            'message_id': int(STATUS_MSG_ID),
            'text': message,
            'parse_mode': 'HTML'
        })
        if result.get('ok'):
            print(f"Edited existing message {STATUS_MSG_ID}")
            new_msg_id = STATUS_MSG_ID
        else:
            print(f"Edit failed: {result}")

    if not new_msg_id:
        result = tg('sendMessage', {
            'chat_id': CHAT_ID,
            'message_thread_id': int(THREAD_ID),
            'text': message,
            'parse_mode': 'HTML'
        })
        if result.get('ok'):
            new_msg_id = str(result['result']['message_id'])
            print(f"Posted new message {new_msg_id}")
        else:
            print(f"Failed to post: {result}")
            return

    if new_msg_id and new_msg_id != STATUS_MSG_ID:
        gh_set_variable('TELEGRAM_STATUS_MESSAGE_ID', new_msg_id)
        print(f"Updated TELEGRAM_STATUS_MESSAGE_ID → {new_msg_id}")

    print("Done.")

if __name__ == '__main__':
    main()
