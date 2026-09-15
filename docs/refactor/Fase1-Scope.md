# Fase 1 — scope og udvidelsesgaranti

> Skrevet 2026-09-02 efter en scope-afklaring: **valgfagenes features skal ikke bygges nu.**
> Refaktoreringens opgave er at efterlade projektet i en tilstand hvor de *kan* bygges, når hver guild kommer dertil.

---

## Princippet

Fase 1 til 3 leverer **ét kørende system: personale-backoffice på web, porteret fra WPF.** Intet andet.

Alt hvad valgfagene skal bygge — API, Flutter-app, kundeside, betaling, roller — bygges senere af de respektive guilds. Fase 1's ansvar over for dem er **ikke at spærre vejen**, ikke at bane den.

Det giver to slags beslutninger, og kun den ene skal træffes nu:

| | Beslutning | Hvornår |
|---|---|---|
| **Dyr at ændre senere** | Rammer alle filer eller kræver datamigrering | **Fase 1** |
| **Billig at tilføje senere** | Nyt projekt, ny mappe, ny migration, middleware | Når guilden bygger den |

---

## Dyre beslutninger — skal træffes i Fase 1

Disse koster stort set intet at beslutte nu og er dyre at ændre bagefter.

**1. Al forretningslogik i `Application`-services — aldrig i ViewModels.**
Det er den eneste beslutning der virkelig betyder noget. Ligger reglen i en Blazor-ViewModel, skal API'et duplikere den, og Flutter-appen arver duplikatet. Ligger den i en service, får alle senere klienter den gratis. Det er præcis den fejl det gamle projekt lavede: 823 linjer forretningslogik i `BookingOverviewViewModel.cs`.

*Test på om det holder:* kan en konsol-app kalde `BookingService.CheckIn(id)` og få alle reglerne håndhævet? Hvis ja, kan et API det også.

**2. Async hele vejen ned.**
Det gamle projekt er 100 % synkront — nul `async` i hele dataadgangslaget. At retrofitte async gennem repos, services og ViewModels senere er en mekanisk ændring der rører hver eneste fil. Conventions.md kræver det allerede.

**3. Databasenavngivning: snake_case via `EFCore.NamingConventions`.**
Ændres senere = alle migrations og alle rå queries. Koster ét linjes opsætning nu. (Q-14, Q-29)

**4. Enum-lagring: int, string eller PostgreSQL-enum.**
Ændres senere = datamigrering. Anbefaling: **int**, fordi ordinalværdierne 0-4 allerede findes i eksisterende data. (Q-13)

**5. Datotyper: `date` vs. `timestamptz`, og tidszonevalg.**
Ændres senere = datamigrering plus gennemgang af alle datosammenligninger. `StartDate`/`EndDate` bruges som rene datoer, `CheckInTime`/`CheckOutTime` som tidsstempler. (Q-05b)

**6. Bookingreglerne selv — Q-01, Q-02, Q-03.**
De skrives ind i `Domain/Rules/BookingRules` og er det ledger-auditen i Fase 3 måler op imod. Bygges systemet på et gæt, auditerer I mod et gæt.

**7. `RoomStatus`-forløbet.** *(tilføjet 2026-09-07)*
Mobilappen skal styre rengøringsforløbet — daglig rengøring, service, slutrengøring (D-10). Den nuværende enum (`Available`, `OutOfService`, `Maintenance`) kan ikke bære det, og den gamle XAML-dropdown var i forvejen uenig med den (BR-126). Enum-værdier persisteres som int, så en omlægning bagefter er en datamigrering. Forløbet designes færdigt nu, selv om appen bygges senere. Se `Decisions.md` F-01.

**8. Kryptering af persondata.** *(tilføjet 2026-09-07)*
Pasnummer og tilsvarende skal krypteres (D-04). En krypteret kolonne kan ikke søges eller sorteres normalt, så det ændrer EF-konfiguration og enhver query der rører feltet. Kan ikke retrofittes billigt.

---

## Billig insurance — koster intet nu, holder døren åben

Ikke features. Bare beslutninger om *ikke* at male sig op i et hjørne.

| Hvad | Hvorfor det er gratis nu | Hvad det ville koste senere |
|---|---|---|
| **Lad `Guest` forblive en ren domæneentitet** — ikke login-entitet | Vi bygger ikke login nu; vi undgår bare at `Guest` bliver kontoen ved et uheld | Hvis `Guest` får password-felter og login-ansvar, skal det pilles fra hinanden igen når Identity kommer |
| **Lad `RoomSize` blive en enum eller lookup, ikke en fri streng** | Samme arbejde som at portere det som string | `RoomType` med pris kan vokse ud af en enum; en magisk streng skal migreres |
| **Håndhæv rolle-autorisation i `Application`, ikke kun i UI** | Følger af beslutning 1 | Håndhæves rollen kun i Blazor, arver API'et den ikke, og Flutter-appen kan kalde forbi (D-03, F-03) |
| **Hold `Web` fri for al logik — også præsentationsnær** | Følger af beslutning 1 | Et API tilføjet senere ville skulle genskabe det |
| **Definér DTO'er i `Application`, ikke i `Web`** | Samme arbejde | DTO'er i Web kan ikke genbruges af et API uden at flytte dem |
| **Registrér services via interfaces i DI fra start** | Conventions.md kræver det | Et API's composition root skal kunne genbruge registreringerne |

Det er hele listen. Ingen af punkterne er en feature.

---

## Bevidst udskudt — bygges af guilden, ikke nu

Alle disse er **billige at tilføje**, når beslutning 1 holder. De skal ikke designes i Fase 1.

| Hvad | Hvad det koster når det kommer | Track |
|---|---|---|
| `NFHotel.Api` | Nyt projekt der refererer `Application`. Endpoints er tynde kald til services der allerede findes | Mobil |
| Flutter-app | Selvstændig kodebase. Rører ikke .NET-solutionen | Mobil |
| Kundeside på hjemmesiden | Ny mappe i `Web`. Nye regler, ja — men de skal alligevel defineres når featuren bygges | Frontend |
| Auth-implementering + JWT | Middleware, en migration, `[Authorize]`-attributter. **Ikke** ASP.NET Identity — se D-01 | Cybersecurity |
| Roller og autorisation | Følger med Identity | Cybersecurity |
| `RoomType` med pris | Migration + backfill. Enum'en fra "billig insurance" gør det lettere | Domæne |
| `Payment`/`Invoice` | Nye entiteter + migration | Domæne |
| Blazor render modes | Sættes pr. komponent i .NET 8+. Retrofittes uden videre | Frontend |
| CORS, rate limiting | Middleware-konfiguration | DevOps / Cybersecurity |
| CI-pipeline til Flutter | Separat pipeline. Rører ikke .NET-siden | DevOps |

**Konsekvens for `OpenQuestions-Tracks.md`:** Q-33 til Q-69 er stadig rigtige spørgsmål, men de er **guildernes spørgsmål, når de bygger** — ikke blokkere for Fase 1. De fire reelle blokkere er Q-01, Q-02, Q-03 og de fem tekniske konventioner ovenfor.

---

## Hvad Fase 1 skal levere

1. `docs/Architecture.md` — mappestruktur (inkl. `Api` som målbillede, jf. D-09), interfaces, DTO-signaturer, de otte beslutninger ovenfor besluttet og begrundet.
2. Tomme projekter der builder: `Domain`, `Application`, `Infrastructure`, `Web` + de tre testprojekter.
3. Svar på Q-01, Q-02, Q-03 skrevet ind i ledgeren, så Fase 2 bygger mod en fastlagt tilstandsmaskine.
4. En kort "udvidelsesnote" i `Architecture.md`: hvor et API, en kundeside og Identity vil hægte sig på — **som beskrivelse, ikke som kode.**

Ikke i Fase 1: `Api`-projektet som kode (det tegnes, men bygges ikke), kundesidens regler, auth-implementeringen, priser, Flutter.

---

## Én ting værd at bevare fra den forrige runde

Selvom features udskydes, er ét fund ægte og bør stå i `Architecture.md` som en note: **Blazor Server har ingen HTTP-flade.** Når mobil-guilden kommer, skal de bruge et API — det kan ikke undgås ved at "kalde Blazor". Det er ikke et problem at løse nu, men det er værd at have skrevet ned, så det ikke opdages som en overraskelse midt i mobil-forløbet.
