# Beslutningslog

> Truffet beslutninger, ikke spørgsmål. Erstatter de tilsvarende poster i `OpenQuestions.md` og `OpenQuestions-Tracks.md`.
> Format: hvad er besluttet · hvad det betyder for koden · hvornår det rammer.

---

## 2026-09-07 — runde 1

### Sikkerhed og adgang

**D-01 (Q-08) — Ikke ASP.NET Identity.**
Der bruges en lettere auth-løsning (fx `simpleauth.net` — eksempel, ikke valgt leverandør).

**"Auth først, middleware" betyder rækkefølge *inden for* Cybersecurity-sporet:** når auth-featuren bygges, bygges den før roller og autorisation, og den eksponeres som middleware. Det betyder **ikke** at auth skal bygges før resten af refaktoreringen. *(Bekræftet 2026-09-07.)*
→ *Kode:* auth er et selvstændigt lag, ikke noget der væves ind i Web-projektet. Application-services skal kunne kalde uden at kende auth-implementeringen.
→ *Rammer:* auth-implementeringen er udskudt, jf. `Fase1-Scope.md`. Kun grænsefladen skal tænkes med i Fase 1b, så laget kan hægtes på uden ombygning.

**D-02 (Q-36) — JWT implementeres.**
→ *Kode:* token-udstedelse i auth-middleware, validering på både Web og senere Api.

**D-03 (Q-39) — Rollebaseret adgang, fire roller.**

| Rolle | Mobilapp | Hjemmeside |
|---|---|---|
| Rengøringspersonale | ✅ | ✅ |
| Servicetekniker | ✅ | ✅ |
| Manager / ejer | ✅ | ✅ |
| Gæst | ❌ | ✅ |

Rollerne styrer adgang til **skema og logning**.
→ *Kode:* rolle er en claim i JWT'en. Autorisation håndhæves i Application-laget, ikke kun i UI — ellers arver API'et ikke reglen.

**D-04 (Q-38) — Privacy by design. Pasnummer og tilsvarende persondata krypteres. Delete policy påkrævet.**
→ *Kode:* **dette er en Fase 1-beslutning**, ikke en senere tilføjelse. En krypteret kolonne kan ikke søges eller sorteres normalt, så det ændrer både EF-konfiguration og enhver query der rører feltet. Skal med i `Architecture.md`.

**D-05 (Q-41) — Sletning håndteres i to separate spor.**
- Bookingdata slettes efter X måneder.
- Persondata opbevares i X år, eller indtil gæsten selv beder om sletning.
→ *Kode:* to uafhængige retention-mekanismer. Soft-delete af booking (BR-13) er **ikke** det samme som sletning af persondata. X'erne fastsættes senere.

**D-06 (Q-09) — Krediteringslækken i det gamle README er en konstatering, ikke en opgave.**
Læring: læs output før det pastes.

**D-07 (Q-40) — Secrets gemmes på GitHub indtil videre.**
Beslutningen er bevidst midlertidig — holdet vurderer ikke at have nok teknologikendskab til et endeligt valg endnu.
→ ⚠️ **Skal præciseres:** *GitHub Actions Secrets* (krypteret, korrekt) eller *filer i repoet* (= præcis fejlen fra D-06). Se note nederst.

### Mobilapp

**D-08 (Q-33) — Flutter.**

**D-09 (Q-35) — `NFHotel.Api` indgår i målarkitekturen.**
Tegnes i `Architecture.md` som en del af målbilledet. Bygges ikke i denne omgang — jf. `Fase1-Scope.md`.

**D-10 (Q-34) — Appens formål er rumstatus, ikke check-in.**
Brugere: rengøringspersonale, servicetekniker, ejer. Samme app, forskellig adgang efter rolle (D-03).
Funktion: opdatering af rumstatus gennem rengøringsforløbet — **daglig rengøring, service, slutrengøring**.
→ ⚠️ *Konsekvens:* se "Fund" nedenfor. Den nuværende `RoomStatus`-enum kan ikke bære det.

**D-11 (Q-37) — Ingen offline-understøttelse.**
Begrundelse: status skal kunne opdateres fra flere enheder uden at der opstår to versioner af virkeligheden — ét sted viser rummet "klar", et andet "mangler service".
→ *Kode:* alle statusændringer går direkte mod serveren. Ingen lokal kø, ingen konfliktløsning. Til gengæld skal appen håndtere netværksfejl synligt (ikke som det gamle projekt, hvor check-in-fejl kun gik til `Debug.WriteLine` — BR-07/BR-10, Q-25).

---

## Fund der følger af beslutningerne

**F-01 — `RoomStatus` kan ikke bære mobilappens formål.**

| Kilde | Værdier |
|---|---|
| `Models/Enums/RoomStatus.cs` | `Available` (0), `OutOfService` (1), `Maintenance` (2) |
| Gammel XAML-dropdown | Available, Maintanance *(stavefejl)*, Cleaning, Disabled |
| **D-10 kræver** | daglig rengøring, service, slutrengøring, klar |

De to gamle kilder har aldrig været enige (BR-126, Q-15), og ingen af dem dækker det rengøringsforløb appen skal styre.

→ **Dette flytter `RoomStatus` fra "billig insurance" til en Fase 1-beslutning.** Enum-værdier persisteres som int i databasen, så en omlægning bagefter er en datamigrering. Rumstatus-forløbet bør designes færdigt i Fase 1 — selv om appen først bygges senere.

**F-02 — Mobilappen hviler på rumstatus, ikke bookingstatus.**
Ledgeren indeholder 18 statusovergangsregler for `BookingStatus` og reelt **ingen** for `RoomStatus` (kun BR-81: nye rum er `Available`, og BR-126: XAML-mismatchet). Rumstatus-forløbet er altså et nyt lille regelsæt der skal defineres — men det er mobil-guildens arbejde, når de bygger.
*Sidegevinst:* appen rører ikke bookinger, så den er uafhængig af Q-01, Q-02 og Q-03.

**F-03 — Autorisation skal ligge i Application-laget.**
D-03 giver fire roller på tværs af to klienter. Håndhæves rollen kun i Blazor-UI'et, arver API'et den ikke, og Flutter-appen kan kalde forbi. Samme princip som forretningslogikken.

---

## Note til D-07

Ironien er værd at fange: D-06 handler om credentials der lå i klartekst i et README, og D-07 siger "secrets på GitHub".

- **GitHub Actions Secrets** → krypteret, korrekt, standardpraksis. Ingen indvending.
- **Secrets i filer i repoet** → samme fejl som D-06, bare i et nyere repo.

Hvis det er det første, er beslutningen fin som den er og kan stå indtil I ved mere. Er det det andet, er `dotnet user-secrets` lokalt + Actions Secrets i CI en gratis opgradering der ikke kræver teknologivalg.

---

## Status

**Besluttet:** Q-08, Q-09, Q-33, Q-34, Q-35, Q-36, Q-37, Q-38, Q-39, Q-40, Q-41.

**Tilbage før Fase 1:** Q-01, Q-02, Q-03 (bookingreglerne) · rumstatus-forløbet (F-01) · de fem tekniske konventioner i `Fase1-Scope.md`.
