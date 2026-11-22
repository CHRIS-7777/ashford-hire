# Ashford Hire — Customer Portal (Black-Box Web Application Penetration Test Lab)

> ⚠️ **Intentionally vulnerable software.** Run it only on an isolated lab VM or network. Never expose it to the internet or a shared network.
>
> 🙈 **This README is spoiler-free.** All answers are in [`Solutions.md`](Solutions.md). Don't open it, the `src/`, `db/`, `tools/` or `tests/` folders, `Dockerfile` or `docker-compose.yml` until you've finished. Treat the application as a black box, the same as in the exam.

---

## 1. Engagement brief

**Client:** Ashford Tool & Plant Hire Ltd, an independent plant, tool and access equipment hire company with eight depots across Yorkshire and Greater Manchester.

**Application:** The Ashford Hire customer portal (v3.2.0), due for go-live. It is used by:

| User group | What they do |
|---|---|
| Anonymous visitors | Browse the fleet, check delivery coverage, find depots, build an estimate |
| Customers (retail and trade) | Book hires online, manage hires, save/export/import quotes, configure webhooks, manage their profile and security settings, use the customer API |
| Depot staff ("yard") | Dispatch board, service-record exports, portal activity |
| Head office (admin) | Trading dashboard, user and role management |

The client has heard there are **around 25 security issues** and wants them all found before launch.

## 2. Scope

| In scope | Out of scope |
|---|---|
| The web application at `http://<lab-ip>:8100/`: every page, form, cookie, script and API under that origin | The host VM itself (SSH, other ports, other Docker containers) |
| Anything you can reach *through* the application | Denial of service, resource exhaustion, brute-forcing with huge wordlists against the web login |
| | Other labs on the same VM (ports 8090, 8095, …) |

## 3. Starting point

You get one standard **retail customer** account:

| Username | Password |
|---|---|
| `jordan` | `Summer2024` |

You may register as many extra accounts as you like. No staff or admin credentials are provided.

Test card for bookings: any 13–19 digit number (e.g. `4242 4242 4242 4242`), any future expiry `MM/YY`, any 3-digit CVC.

## 4. Objectives

1. **Map the application.** Find every page, parameter, cookie, API endpoint and client-side script. What can an anonymous visitor learn?
2. **Find and prove each vulnerability** with a minimal, non-destructive proof of concept.
3. **Classify** each finding with its OWASP Top 10 (2025) category and CWE.
4. **Escalate.** Get from customer → depot staff → head-office admin. There's more than one route.
5. **Capture the flags.** Several issues reward you with a `FLAG{...}` value (five in total).
6. **Recommend a fix** for every finding.

### Questions to guide you
- What does the application tell you about itself in places users don't normally look?
- Which features make the *server* fetch, read, run or deserialize something for you?
- Which values does the server trust that you control (hidden fields, cookies, tokens, JSON properties)?
- How does the portal decide who you are, and does every way of signing in check that equally well?
- What happens when you send input the developers didn't expect, such as the wrong type, odd characters or encodings?

## 5. Rules of engagement

- Keep proofs of concept harmless. Prefer `alert()`/`print()`, `id`/`whoami`, or reading a record over changing or deleting data.
- Don't close or lock the provided `jordan` account. Register your own accounts for destructive tests.
- Record every request and response you use as evidence (Burp project file recommended).
- If the lab gets into a weird state, reset it (see below). It takes about a minute.

## 6. Reporting

For each finding include: **title, OWASP 2025 category, CWE, affected URL and parameter, severity, steps to reproduce, evidence (request/response or screenshot), impact, and remediation.** Finish with an attack-chain diagram showing how you escalated from customer to admin.

Suggested tools: Burp Suite / OWASP ZAP, browser DevTools, ffuf / gobuster / feroxbuster, sqlmap, hashcat / John, jwt_tool, Retire.js, curl, `python3 -m http.server` for PoC pages.

---

## 7. Deployment (Docker)

Requirements: Docker Engine 24+ with the Compose plugin, about 3 GB of free RAM and 4 GB of free disk.

```bash
cd /home/kali/PTLAB/CHRIS-LAB/ashford-hire
docker compose up -d --build        # first build takes a few minutes
# the database initialises on first boot (~30-60 s)
curl -s http://127.0.0.1:8100/healthz   # {"status":"Healthy",...} when ready
```

Open `http://<vm-ip>:8100/` (find the VM IP with `hostname -I | awk '{print $1}'`).

| Setting (`.env`) | Default | Meaning |
|---|---|---|
| `LAB_BIND` | `0.0.0.0` | Interface to publish on. Set to `127.0.0.1` for local-only |
| `LAB_PORT` | `8100` | Published port |

### Reset to a clean state
```bash
./reset.sh          # destroys the containers and their data, rebuilds, waits until ready
```

### Stop / start without losing data
```bash
docker compose stop
docker compose start
```

### Free up memory
The lab uses about 2 GB of RAM. If the VM is tight, stop the other labs while you test:
```bash
docker stop meridian-web meridian-db willowbrook-web willowbrook-db
```

### Troubleshooting
| Symptom | Fix |
|---|---|
| Portal not responding right after `up` | Wait 60 s; check `docker compose ps` (the `ashford-db-init` container should show *Exited (0)*) |
| `ashford-web` keeps restarting | `docker compose logs web`, usually the database ran out of memory. Free RAM and `./reset.sh` |
| Lab behaving strangely after heavy testing | `./reset.sh` |

---

## 8. Folder layout

```
ashford-hire/
├── README.md            ← this file (spoiler-free)
├── Solutions.md         ← full answers (don't open until you're done)
├── reset.sh             ← one-command reset
├── docker-compose.yml   ← deployment
├── .env                 ← ports and lab secrets
└── src/ db/ data/ staging/ tools/ tests/   ← application source, seed data, self-test (spoilers)
```

When you're finished, compare your report with `Solutions.md`, then run the self-test against a fresh instance to see every intended finding exploited automatically:

```bash
./reset.sh && python3 tests/selftest.py && ./reset.sh
```
