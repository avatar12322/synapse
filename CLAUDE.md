# Synapse — LifeQuest AI

> Aplikacja mobilna (iOS + Android), która zamiast walczyć o czas ekranowy
> *wymusza jego oddanie* — pary użytkowników dostają miejskie misje (Side Quests)
> w lokalnych kawiarniach i muszą fizycznie zablokować telefon, by ją zaliczyć,
> dostać zniżkę u partnera i punkty reputacji. Monetyzacja: prowizja od lokali
> *za zweryfikowaną sprzedaż*, a faktury B2B wystawia automatycznie sama platforma
> przez **KSeF 2.0 (samofakturowanie, FA(3))**.

Dokument źródłowy koncepcji: `c:\Users\avata\Downloads\synapse.md`. Ten plik
adaptuje wizję do realnego stacku scaffoldu (różni się od raportu — patrz §2).

---

## 1. Co już jest (stan repozytorium)

Repo to scaffold z projektu „Questify" przekształcony w `Synapse`. **Fazy 0, 1 i 2 ukończone.**

| Warstwa          | Co jest                                                                                                                                              |
| ---------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| Backend          | [backend/](backend/) — **.NET 9** minimal API, EF Core 9, JWT (httpOnly cookie), Postgres + Redis (lub in-memory fallback). Clean Arch: Api/Core/Infrastructure |
| Modele DB        | [User](backend/Core/Models/User/User.cs), [Business](backend/Core/Models/Business/Business.cs), [Mission](backend/Core/Models/Mission/Mission.cs), [UserProfile](backend/Core/Models/User/UserProfile.cs), [KsefInvoice](backend/Core/Models/Invoice/KsefInvoice.cs), Friendship, Notification |
| Endpointy        | `/api/missions` (CRUD + accept/lock/cancel/verify/**verify-nfc**), `/api/businesses` (nearby + CRUD + webhook-secret + nfc-secret), `/api/match`, `/ws/presence`, `/api/stripe/*`, `/api/webhooks/pos`, `/api/invoices/*`, `/api/notifications` |
| Baza danych      | Migracje: `Phase1_InitialSchema` + `Phase2_PosNfcSecretsAndKsefInvoices` — PostGIS, pgvector, KsefInvoices, PosWebhookSecret, NfcSecret, VerifiedByPos |
| Frontend         | [frontend/](frontend/) — **Next.js 15** (Turbopack) + React 19 + TanStack Query + Tailwind v4 + Radix + framer-motion + i18next (PL/EN). |
| Python agents    | [agents/](agents/) — **FastAPI + LangGraph**, 4 agenty (Profiler / Scout / Matchmaker / Orchestrator), Gemini 2.5 Flash, sentence-transformers (768-dim embeddings) |
| Capacitor plugins | [frontend/native-plugins/](frontend/native-plugins/) — `screen-lock-guard`, `presence-verifier`, `anti-spoof`, **`nfc-presence`** (Swift + Kotlin + TS bridge) |
| KSeF service     | [ksef/](ksef/) — **osobny ASP.NET Core 9** na porcie 8002 (docker profile: `ksef`). Auth challenge-response, XML FA(3), AES+RSA szyfrowanie, UPO polling, PDF/A-3 z QR |
| Mobile wrap      | [capacitor.config.ts](frontend/capacitor.config.ts) — **Capacitor 8** (iOS + Android), appId `com.synapse.app`                                       |
| Infra dev        | [docker-compose.yml](docker-compose.yml) — Postgres 16 + Redis 7 + Adminer + agents (profil: agents) + **ksef** (profil: ksef)                       |

**Co NIE jest zrobione (pozostałe z Fazy 1):**
- iOS Live Activity z timerem (ActivityKit + Dynamic Island) — plugin `mission-hud`
- Android persistent foreground notification z licznikiem

**Co NIE jest zrobione (Fazy 3–4):**
geosharding Redis, Kafka, Pinecone, Kubernetes, Anti-Sybil ML, Apple Watch companion, CI/CD, multi-jurisdiction VAT.

---

## 2. Świadome odstępstwa od raportu źródłowego

| Raport (synapse.md) | Ten projekt          | Powód                                                                                                              |
| ------------------- | -------------------- | ------------------------------------------------------------------------------------------------------------------ |
| Flutter             | **Next.js + Capacitor 8** | Scaffold już istnieje. Capacitor + natywne pluginy w 2026 wystarczają, a frontend współdzieli kod z web onboardingiem |
| Pinecone od MVP     | **pgvector na Postgres 16** w MVP, Pinecone/Milvus później | Już mamy Postgres; pgvector w 2026 jest dojrzały, oszczędza infra-koszt MVP                                        |
| Kafka od początku   | **Redis Streams** w MVP, Kafka/Redpanda przy >10k DAU      | YAGNI. Redis już mamy                                                                                              |
| LangGraph w jednym monolicie | **Osobny mikroserwis Python** (FastAPI + LangGraph) wołany przez .NET | .NET nie ma natywnego LangGraph. Język agentów = Python, biznes = C#                                               |
| Tylko cloud matching | **Hybrydowo**: profilowanie on-device (Apple Foundation Models / Gemini Nano), matching w chmurze | 2026: modele on-device są darmowe, prywatne i wystarczająco dobre do embeddingów profilu (privacy-first)            |

---

## 3. Architektura docelowa (2026)

```
┌──────────────── MOBILE (iOS / Android) ─────────────────┐
│  Capacitor 8 shell  ─── webview ───  Next.js 15 (SSG)    │
│  ▲  natywne pluginy (Swift / Kotlin) — jeden per feature │
└──┬──────────────────────────────────────────────────────┘
   │ HTTPS + WebSocket (presence stream)
┌──┴──────────────────────────────────────────────────────┐
│  EDGE (Cloudflare / Caddy reverse-proxy)                │
└──┬──────────────────────────────────────────────────────┘
   │
   ├─► .NET 9 API (Synapse.Api)        ── auth, business, mission CRUD, payments
   │     │
   │     ├─► Postgres 16 + PostGIS + pgvector    ── source of truth
   │     ├─► Redis 7 (GEO, Streams, cache)        ── presence + event bus (MVP)
   │     └─► gRPC ─► Python "Synapse.Agents"     ── LangGraph orkiestracja
   │                  │
   │                  ├─► Anthropic Claude (Sonnet 4.6 / Opus 4.7) ── reasoning
   │                  ├─► Pinecone (faza 2) lub pgvector (MVP)      ── semantic search
   │                  └─► Tool calling: Maps, OpenWeather, POS APIs
   │
   ├─► Stripe Connect (Standard + Express)         ── KYC, split payments, BLIK
   ├─► KSeF Integrator (osobny .NET service)       ── FA(3), self-billing, OAEP/AES
   └─► Object storage (S3-compat)                   ── PDF UPO, faktury, eksporty RODO
```

---

## 4. Plan mobilny (kluczowy obszar)

### 4.1 Strategia: Capacitor + natywne pluginy

Webview Capacitora obsługuje **UI/UX** (mapy, listy misji, czat, profil).
Wszystko, co dotyka uprawnień systemowych, sensorów albo działania w tle,
trafia do dedykowanego pluginu Capacitora — **jeden plugin = jedna domena**.

Pluginy żyją w `frontend/native-plugins/<name>/` z podkatalogami `ios/` (Swift)
i `android/` (Kotlin) + cienki TS-bridge. Lista do zbudowania:

| Plugin                | iOS                                                                                                              | Android                                                                                            | Cel                                                                                       |
| --------------------- | ---------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| **screen-lock-guard** | `FamilyControls` + `ManagedSettings` (Shield) + `DeviceActivityMonitorExtension` (≤6 MB RAM!) + `UIApplicationProtectedData*` | Foreground Service (`FOREGROUND_SERVICE_HEALTH`) + `BroadcastReceiver(SCREEN_OFF/ON)` + `KeyguardManager.isKeyguardLocked()` | Wymusza i mierzy czas blokady ekranu na czas misji                                        |
| **presence-verifier** | `CMMotionActivityManager` + `CLLocationManager` (geofence)                                                       | `ActivityRecognitionClient.requestActivityTransitionUpdates` + `GeofencingClient`                  | Potwierdza fizyczną obecność (STILL + wewnątrz geofence kawiarni)                          |
| **anti-spoof**        | `DCAppAttestService` (App Attest)                                                                                | Play Integrity API (`StandardIntegrityManager`)                                                    | Atestacja sprzętowa — odrzuca emulatory, root, mock-location                              |
| **mission-hud**       | `ActivityKit` (Live Activities + Dynamic Island) + WidgetKit                                                     | Persistent foreground notification + `MediaSession`-style live tile + Wear OS companion (faza 2)   | Pokazuje timer misji bez konieczności odblokowania                                        |
| **payment-bridge**    | Stripe iOS SDK (Apple Pay)                                                                                       | Stripe Android SDK (Google Pay) + BLIK via Stripe Payment Sheet                                    | Mikropłatności BLIK/Apple Pay/Google Pay przez Stripe Connect                              |
| **nfc-presence** *(autorski)* | Core NFC `NFCNDEFReaderSession`                                                                          | `NfcAdapter.ENABLE_READER_MODE`                                                                    | Tap-to-verify: kawiarnie dostają tag NDEF z signed payload — twardy dowód obecności       |
| **profile-llm** *(autorski)* | Apple Foundation Models (iOS 26+) — `LanguageModelSession`                                                | Gemini Nano via AICore + ML Kit GenAI APIs                                                         | On-device profilowanie/embedding — surowy tekst nigdy nie opuszcza urządzenia              |

### 4.2 Stałe gotchas mobilne

- **iOS `DeviceActivityMonitorExtension` ma 6 MB RAM** — przekroczenie = silent kill. Trzymać struktury w `Codable` z manual `init(from:)` zamiast `JSONDecoder` na całym blobie.
- **Apple zabrania prywatnych API** typu `com.apple.springboard.lockstate`. Każda taka próba = automatyczne odrzucenie z App Store. Tylko publiczne frameworki.
- **Capacitor + Next.js wymaga static export** (`output: 'export'` w `next.config.ts`) bo webview nie ma serwera. Routy dynamiczne hostuje natywnie webview, dane fetchuje runtime.
- **Android 14+ Foreground Service** wymaga deklaracji `foregroundServiceType` (`location`, `health`) i runtime permission. Bez tego serwis jest zabijany w <30s.
- **Background battery**: oba systemy. Geofence bije po stronie OS-a, nie naszego procesu — używać tego, nie pollingu GPS w tle.

### 4.3 UX zamknięcia ekranu — pomysły autorskie

1. **Apple Watch / Wear OS companion** (faza 2): timer misji na zegarku. Rozwiązuje problem „skąd mam wiedzieć, że już 30 min minęło?".
2. **Mood check-in** (autorskie): 30-sek dzienny puls (3 emoji + slider energii) zamiast statycznych zainteresowań — agent profilujący dostaje świeży sygnał, parowanie jest „today-aware".
3. **Anti-spoof fallback**: jeśli GPS jest niepewny (kawiarnia w piwnicy, słaby sygnał), plugin `nfc-presence` lub statyczny QR „Quest Code" obok lady kelnerskiej działa jako twardszy dowód.

---

## 5. Roadmap (4 fazy, każda zamknięta produktowo)

### Faza 0 — Fundament (✅ ukończona)
Auth + JWT, Postgres + Redis, podstawowe modele, Capacitor wired up, Next.js dev server działa.

### Faza 1 — MVP w jednym mieście (Kraków, 6–10 lokali ręcznie onboardowanych) (✅ prawie ukończona)
- [x] Endpointy: `/missions` (CRUD + accept/lock/cancel/verify), `/businesses/nearby`, `/api/match`, `/ws/presence` (WebSocket)
- [x] PostGIS `geography(Point,4326)` + pgvector `vector(768)` + extension `h3` w migracji `Phase1_InitialSchema`
- [x] Mikroserwis `Synapse.Agents` (Python 3.13 + FastAPI + LangGraph) — 4 agenty (Profiler / Scout / Matchmaker / Orchestrator) + Claude Sonnet 4.6
- [x] Plugin `screen-lock-guard` scaffold (FamilyControls iOS + Foreground Service Android)
- [x] Plugin `presence-verifier` scaffold (CoreLocation + geofence iOS, ActivityRecognitionClient Android)
- [x] Plugin `anti-spoof` scaffold (DCAppAttestService iOS, Play Integrity Android)
- [x] Weryfikacja sprzedaży: **ręczna** — lokal wpisuje 6-cyfrowy kod w panel `/business/verify`
- [x] Stripe Connect Standard — onboarding link + ChargeCommission via Transfer
- [x] Frontend: lista misji, detail z state machine (Pending→Accepted→InProgress→Completed), match flow, panel biznesowy
- [ ] iOS Live Activity z timerem (ActivityKit + Dynamic Island) — plugin `mission-hud` — **pozostało**
- [ ] Android persistent foreground notification z licznikiem — **pozostało**

**Definition of done:** dwoje testerów dostaje misję, dochodzą do kawiarni, dostają zniżkę 15 % via kod, lokal akceptuje, Stripe wypłaca prowizję.

### Faza 2 — Integracje partnerskie (✅ ukończona)
- [x] **Stripe Webhooks** — `POST /api/stripe/webhook`, obsługa `account.updated` / `account.application.deauthorized` / `transfer.paid`, env: `STRIPE_WEBHOOK_SECRET`
- [x] **POS Webhooks** — `POST /api/webhooks/pos` (HMAC-SHA256, format Synapse), generowanie sekretów per-biznes (`/api/businesses/webhook-secret`, `/nfc-secret`), pola `Business.PosWebhookSecret` + `Business.NfcSecret`, `Mission.VerifiedByPos` + `Mission.PosTransactionId`
- [x] **KSeF 2.0 integrator** (`ksef/` — osobny ASP.NET Core 9 service, port 8002, docker profile: `ksef`):
    - `KsefAuthService` — OAuth challenge-response, cert lub token (test env)
    - `InvoiceGeneratorService` — XML FA(3) z `P_17=1` (samofakturowanie)
    - `EncryptionService` — AES-256-CBC + RSA-4096 OAEP-SHA256
    - `UpoPollerService` — IHostedService, exponential backoff 5s→10s→30s→60s (max 20 prób)
    - `PdfArchiveService` — PDF z QR kodem (QuestPDF + QRCoder)
    - Tabela `KsefInvoices` w głównej DB, migracja `Phase2_PosNfcSecretsAndKsefInvoices`
- [x] **Plugin `nfc-presence`** (`frontend/native-plugins/nfc-presence/`) — iOS `NFCNDEFReaderSession` + Android `NfcAdapter.enableReaderMode`, NDEF payload `{ v, bid, mid, ts, sig }` z HMAC-SHA256 per-biznes, backend endpoint `POST /api/missions/{id}/verify-nfc` z replay protection (5-min okno)
- [ ] Card-Linked Offers przez Mastercard Offers API (alternatywa, odłożone)
- [ ] Apple Watch / Wear OS companion (Faza 3)

### Faza 3 — Skalowanie
- [ ] Geosharding Redis (klucz: H3 res 6, czyli ~36 km²) — zastąpić single-node
- [ ] Migracja event-bus: Redis Streams → Kafka/Redpanda (gdy >10k DAU)
- [ ] Migracja embedding store: pgvector → Pinecone Serverless lub Milvus on K8s
- [ ] Kubernetes (k3s lub managed) dla agentów Pythona — autoscaling per HPA
- [ ] Anti-Sybil ML: graf Neo4j (relacje misji), klasyfikator (RandomForest baseline → GNN docelowo), reguły heurystyczne na BSSID/IP/device fingerprint
- [ ] Pseudonimizacja archiwum: H3 res 9 → res 7 po 30 dniach (RODO art. 5 minimalizacja)

### Faza 4 — Ekspansja regionalna
- [ ] Multi-tenant / multi-jurisdiction (prawo VAT per kraj)
- [ ] Confidential Matching w TEE (Intel SGX / AMD SEV-SNP) dla embeddings — zero-trust
- [ ] White-label dla sieci coworkingów

---

## 6. Konwencje kodowania

### Backend (.NET 9)
- Minimal API endpointy w `backend/Api/Endpoints/<Domain>/<Domain>Endpoints.cs` — extension method `MapXEndpoints(this WebApplication)`.
- Logika biznesowa w `backend/Core/Services/<Domain>/<Domain>Service.cs` (interfejs + impl), DI w `Program.cs`.
- Modele EF: `backend/Core/Models/<Domain>/<Entity>.cs`. Konfiguracja relacji w `SynapseDbContext.OnModelCreating`.
- DTO w `backend/Core/DTOs/<Domain>/<Domain>Dtos.cs` jako rekordy.
- Migracje: `dotnet ef migrations add <Name> --project backend` (auto-aplikowane przy starcie — patrz `Program.cs:113`).
- Async wszędzie. `CancellationToken` w każdym handlerze.

### Frontend (Next.js 16 — niestandardowy)
- ⚠️ **Najpierw przeczytaj `frontend/AGENTS.md`** — to NIE jest stock Next.js, dokumentacja jest w `frontend/node_modules/next/dist/docs/`. Trening LLM-a jest nieaktualny dla tej dystrybucji.
- App Router (`src/app/`). Server components domyślnie, client tylko gdy potrzebne.
- Stan serwera: TanStack Query (queryClient w `src/lib/queryClient.ts`).
- API base URL: `src/lib/config.ts`. Auth token: httpOnly cookie + `src/lib/auth.ts`.
- UI: Radix primitives + warianty `class-variance-authority`. Animacje: framer-motion.
- i18n: PL/EN via `react-i18next` (`src/i18n/config.ts`).

### Capacitor pluginy (do założenia)
- Folder: `frontend/native-plugins/<plugin-name>/`
- Każdy ma: `package.json`, `src/index.ts` (TS bridge), `ios/Plugin/`, `android/src/main/java/`.
- Importować przez workspace, nie publikować na npm — to monorepo.
- Po napisaniu: `cd frontend && npx cap sync` zawsze.

### Język
- Kod, nazwy plików, commit messages: **angielski**.
- Komentarze: rzadko, tylko gdy „dlaczego" jest nieoczywiste (np. „6 MB RAM limit — patrz Apple docs"). Nigdy „co".
- CLAUDE.md i ten dokument: polski (zgodnie z preferencją autora).

---

## 7. Krytyczne ograniczenia / red flags

- **KSeF jest obowiązkowy od Q1 2026** dla obrotu >200 mln zł, **Q2 2026** dla reszty. Przed Q2 można działać bez integracji, ale wszystko, co fakturujesz B2B jest nielegalne na tradycyjnym PDF.
- **Self-billing wymaga ręcznej akceptacji w panelu KSeF kontrahenta** — nie da się zrobić w 100 % programowo. UX onboarding lokalu MUSI zawierać krok „kliknij tutaj w Aplikacji Podatnika i nadaj nam uprawnienie".
- **App Store review** odrzuci aplikację, która prosi o `Family Controls` bez jasnego case'u dla samokontroli użytkownika dorosłego. Description w Info.plist + onboarding screen muszą to tłumaczyć.
- **Geofence + activity recognition w tle** = aplikacja będzie monitowana przez OS o nadużycie baterii. Trzeba mieć przygotowaną odpowiedź (raport energetyczny) jeśli Apple/Google poprosi.
- **RODO**: lokalizacja w wysokiej rozdzielczości to dane osobowe. Retention max 30 dni surowych H3 res 9, potem agregacja do res 7. Eksport/usuwanie konta MUSI być zaimplementowane od początku.

---

## 8. Polecenia (cheatsheet)

```powershell
# Pełny stack lokalnie
docker compose up -d                           # postgres + redis + adminer (8080)
cd backend ; dotnet run                        # .NET API na :5000
cd frontend ; npm run dev                      # Next.js dev na :3000

# KSeF service (opcjonalnie)
docker compose --profile ksef up -d            # ksef na :8002
cd ksef ; dotnet run                           # lub lokalnie bez Dockera

# Migracje EF (backend)
cd backend ; dotnet ef migrations add <Name>
cd backend ; dotnet ef database update

# Mobile build (po `npm run build` ze static export)
cd frontend ; npm run build
cd frontend ; npx cap sync
cd frontend ; npx cap open ios                 # Xcode (wymaga macOS)
cd frontend ; npx cap open android             # Android Studio

# Testowanie Stripe Webhooks lokalnie
stripe listen --forward-to localhost:5000/api/stripe/webhook
stripe trigger transfer.paid

# Testowanie POS Webhooka (PowerShell)
$secret = "<base64 z POST /api/businesses/webhook-secret>"
$body = '{"event":"sale.completed","businessId":"1","verificationCode":"123456","amount":4500,"currency":"PLN","transactionId":"tx-1","timestamp":"2026-05-09T12:00:00Z"}'
$hmac = [System.Security.Cryptography.HMACSHA256]::new([Convert]::FromBase64String($secret))
$sig = [Convert]::ToHexString($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($body))).ToLower()
Invoke-RestMethod -Uri http://localhost:5000/api/webhooks/pos -Method POST -Body $body `
  -Headers @{"X-Synapse-Signature"="sha256=$sig";"Content-Type"="application/json"}
```

Dev-server IP do iOS/Android symulatora:
ustaw `DEV_SERVER_IP` w `frontend/.env.local` (musi być IP w LAN, nie `localhost`).

Zmienne środowiskowe Fazy 2 — patrz [`.env.example`](.env.example).

---

## 9. Kontakt i kierunek

Wątpliwości przy wyborze technologii rozstrzygać w kolejności:
1. **Czy to da się zrobić bez tej technologii w MVP?** Jeśli tak — odłóż.
2. **Czy to jest standardem w 2026?** (Sonnet 4.6, Opus 4.7, pgvector, Capacitor 8, Next.js 15, .NET 9 — tak. LangChain klasyczny, Pinecone od MVP, Kafka od MVP — niekoniecznie).
3. **Czy istnieje plan migracji z prostszej wersji?** Jeśli nie — to ślepa uliczka.

Każde odstępstwo od tego dokumentu jest w porządku, jeśli zostanie tu udokumentowane wraz z powodem.
