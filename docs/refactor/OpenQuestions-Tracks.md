# Åbne spørgsmål fordelt på valgfag

> Opdateret 2026-09-02. Supplement til `OpenQuestions.md` — samme Q-id'er, grupperet efter hvem der ejer beslutningen.
> Q-01 til Q-32 stammer fra Fase 0-analysen. Q-33 og opefter er nye, rejst af selve fagopdelingen.
>
> ✅ **11 spørgsmål er besvaret 2026-09-07** — se `Decisions.md` (D-01 til D-11): Q-08, Q-09, Q-33 til Q-41. Beslutningsloggen har forrang over denne fil.
>
> ⚠️ **Læs `Fase1-Scope.md` først.** Valgfagenes features bygges ikke i denne omgang. De fleste spørgsmål her er **guildernes spørgsmål, når de bygger deres feature** — ikke blokkere for Fase 1. Kun Q-01, Q-02, Q-03 plus fem tekniske konventioner skal besvares før Fase 1. Resten er noteret så de ikke går tabt.

---

## ⚠️ Læs først: to ting fagopdelingen afslører

**1. De fire tungeste blokkere tilhører ingen af valgfagene.**
Q-01 til Q-04 er domænespørgsmål — hvad *er* en booking, hvornår må man checke ind, koster et værelse penge. De kan ikke uddelegeres til en guild, fordi alle fem tracks bygger oven på svaret. De skal afgøres samlet, af hele holdet, før Fase 1. Se afsnittet "Fælles domæne" nederst.

**2. Mobilapp i Flutter er afklaret — og ændrer målarkitekturen. [opdateret 2026-09-02]**
Valgfagene er krav, ikke valg. Mobilappen bygges i **Flutter**. Det afgør Q-33 og Q-35, og planens afsnit 3 er opdateret til fem projekter + en `mobile/`-mappe.

- **Blazor Server har ingen HTTP-API** — den kører over SignalR mellem browser og server. En mobilapp kan ikke bruge den. `NFHotel.Api` er derfor obligatorisk, ikke valgfri.
- **Flutter er Dart.** Der kan ikke deles én linje C# med backend'en — modsat .NET MAUI, hvor `Domain` og `Application` kunne refereres direkte. **API-kontrakten er dermed det eneste integrationspunkt**, og skal behandles som et selvstændigt artefakt: OpenAPI-spec fra `Api`, som Dart-klienten genereres fra.
- **JWT fra start.** Cookies rækker ikke. **Q-08 (Identity) er ikke længere til at udskyde** — den flytter fra "senere" til Fase 1b.
- **Serialisering skal besluttes bevidst.** Dart og C# er ikke enige om defaults for enums og datoer. Se Q-53 og Q-54.
- **To CI-pipelines.** .NET og Flutter deler intet ud over spec'en. Se Q-42.
- **Fase 2 får en agent mere:** `Api` bygges parallelt med Web-featurene, da begge kun afhænger af `Application`.

---

## DevOps

| Id | Spørgsmål | Hvorfor det haster |
|---|---|---|
| Q-30 | Testdatabase til `Infrastructure.Tests`: Testcontainers, en fast instans, eller EF InMemory? | **DevOps ejer svaret.** Testcontainers kræver Docker på udvikler- og CI-maskiner — det er en infrastrukturbeslutning, ikke en projektbeslutning |
| Q-31 | Hosting-mål for database og app | **DevOps ejer svaret.** Projektet kender kun en connection string; topologien låser deployment-diagrammet i Fase 2 |
| Q-07 | Hvad gør vi med de 40 DB-afhængige repo-tests? | De kan ikke køre i CI som de er |
| Q-06 | MSTest eller xUnit? | Testprojekternes struktur i Fase 1b |
| **Q-42** | Hvilken CI-platform, og hvornår sættes den op? | Se note nedenfor om Windows-låsen |
| **Q-43** | Køres EF-migrations automatisk ved deploy, eller manuelt? | Påvirker `Program.cs` og pipeline |
| **Q-44** | `git init` i den nye solution — hvornår, og hvilken branch-strategi? | Planen siger: **før første agent skriver noget**. Det er ikke sket endnu |
| **Q-45** | Hvor mange miljøer? (lokal / staging / prod, eller kun lokal) | Konfigurationsmodel og secrets |

**Note — CI får to pipelines.** .NET-solutionen og Flutter-appen bygges med hver sin toolchain og deler intet ud over OpenAPI-spec'en. Overvej at lade .NET-pipelinen publicere spec'en som artefakt, så Flutter-pipelinen kan fejle hvis kontrakten er brudt — det er den billigste måde at fange kontraktbrud på.

**Note — Windows-låsen forsvinder.** Det gamle testprojekt er `net8.0-windows`, fordi det refererer WPF-projektet direkte. Det binder CI til Windows-runners. Den nye solution har ingen WPF-afhængighed, så alt kan køre på Linux-runners — hurtigere og billigere. Værd at nævne som konkret gevinst.

**Note — der er ingen `.git` i det gamle projekt** (det er en udpakket zip). Der findes altså ingen historik at arve, og intet diff-spor på det gamle hold. Planens punkt om `git init` før første byggeagent er vigtigere end det lyder: uden det kan I ikke se hvad agenterne har ændret.

---

## Cybersecurity

| Id | Spørgsmål | Hvorfor det haster |
|---|---|---|
| Q-09 | Azure SQL-credentials i klartekst i `Database/README.md:18` | **Skal roteres uanset hvad.** Passwordet er eksponeret i et projekt der er delt mellem grupper |
| Q-08 | ASP.NET Identity nu eller senere? | ✅ **Afgjort af Flutter: nu.** Tilbage står kun *hvilken* model — Identity + JWT, eller noget lettere. JWT betyder også: token-levetid, refresh, og hvor de gemmes på enheden |
| **Q-38** | Pasnummer, e-mail, telefon og land er persondata. Hvad er GDPR-holdningen? | Påvirker skema, logning og hvem der kan se hvad |
| **Q-39** | Hvilken autorisationsmodel? | ✅ **Ikke længere valgfri** — kundesiden kræver minimum rollerne personale og kunde, og at en kunde kun ser sine egne bookinger. Åbent: skal personale underopdeles (receptionist / manager)? |
| **Q-40** | `dotnet user-secrets` lokalt — og hvad i produktion? (env vars, Key Vault, andet) | Konfigurationsmodel i Fase 1b |
| **Q-41** | Bookinger soft-deletes (BR-13). Hvad med gæster — ret til sletning? | Konflikt mellem soft-delete og GDPR art. 17 |

**Note — der findes ingen autorisationsmodel overhovedet.** Alle 23 regler i ledgeren af typen "adgang" handler om UI-tilstand (er en booking valgt, er vi i redigeringstilstand), ikke om hvem der er logget ind. Der er intet `User`- eller `Employee`-begreb i domænet. Systemet antager én bruger med fuld adgang til alt — inklusive alle gæsters pasnumre.

**Note — det gamle projekt er faktisk sikkert mod SQL-injection.** Analysen fandt ingen strenginterpolation ind i SQL; alt bruger parametre eller stored procedures. Det ene sted med `$"%{name}%"` (`GuestRepo.cs:130`) bygger en *parameterværdi*, ikke SQL. Det er et rent resultat og værd at nævne i rapporten som en baseline man ikke må forringe. (Den reelle svaghed er `%`/`_` der ikke escapes og virker som wildcards — en funktionel detalje, ikke en sårbarhed.)

---

## Mobilapp

| Id | Spørgsmål | Status |
|---|---|---|
| ~~Q-33~~ | Platform | ✅ **Afklaret: Flutter** |
| ~~Q-35~~ | Skal `NFHotel.Api` med? | ✅ **Afklaret: ja, obligatorisk.** Planens afsnit 3 er opdateret |
| ~~Q-36~~ | Token-baseret auth fra start? | ✅ **Afklaret: ja, JWT.** Følger af Flutter. Se Q-08 |
| ~~Q-34~~ | Hvem er brugeren? | ✅ **Afklaret: kun personale.** Hjemmesiden dækker både personale og kunder |
| **Q-37** | Offline-understøttelse? | Åben. Hvis ja, ændrer det datamodellen (sync, konflikter, lokale id'er) — stor mundfuld |
| **Q-52** | Genereres Dart-klienten fra OpenAPI-spec'en, eller håndskrives den? | Anbefaling: genereres (`openapi-generator`, `swagger_dart_code_generator`). Ellers driver kontrakten fra hinanden |
| **Q-53** | Enums over API'et: int eller string? | Dart og C# har forskellige defaults. Hænger sammen med Q-13 (enum-mapping i DB) |
| **Q-54** | Datoer over API'et: ISO 8601 med eller uden tidszone? | Hænger sammen med Q-05b. `date` og `timestamptz` skal serialiseres forskelligt |
| **Q-55** | API-versionering fra start (`/api/v1/`) eller senere? | Billigt nu, dyrt senere |
| **Q-56** | CORS — hvilke origins, og hvordan i dev vs. prod? | Rammer også DevOps (Q-45) |
| **Q-57** | Hvem ejer API-kontrakten: backend eller mobil-guild? | Procesbeslutning, men den afgør hvem der blokerer hvem i Fase 2 |
| **Q-58** | Flutter state management: Riverpod, Bloc, Provider? | Mobil-guildens eget valg, men bør besluttes før Fase 2 |
| **Q-59** | Deles designsystem/farver mellem Blazor og Flutter? | Hænger sammen med Q-47 (komponentbibliotek) |

**Konsekvens af Q-34 (personale-app):** appen genbruger BR-06 til BR-10 direkte fra ledgeren og kræver **ingen nye forretningsregler**. Den demonstrerer stadig hele lagdelingen: Flutter → API → Service → Repository → DB. Mobil-guilden er dermed ikke blokeret af Q-04 (priser) — det er hjemmesidens kundeside derimod. Se afsnittet "Kundesiden" nedenfor.

**API-scope følger med:** `NFHotel.Api` behøver kun personale-endpoints til at starte med. Skal kundesiden på et tidspunkt også bruge API'et (fx hvis I senere vil lave en kunde-app), er det en udvidelse — ikke et redesign, så længe endpoints er autoriseret pr. rolle fra start.

**Bonus for AI Integration-rapporten:** en Flutter-app oven på det samme `Application`-lag er det stærkest mulige bevis for at Clean Architecture-opdelingen holder. To klienter i to sprog, nul duplikeret forretningslogik, og en ledger der kan auditeres mod begge.

---

## ⚠️ Kundesiden — nyt område uden dækning i ledgeren

Hjemmesiden har **to publikummer**: personale og kunder. Det er den største scope-udvidelse indtil nu, og den er ikke synlig i noget Fase 0-materiale.

**Alle 126 regler i ledgeren er personale-regler.** Det gamle WPF-projekt var en ren backoffice-app — alle 7 skærme (kalender, gæsteoversigt, rumoversigt, salg, gæstepolitik) er personaleskærme. Der findes **nul** kunderegler i det gamle system, fordi der aldrig har været en kundeflade.

Konsekvenser:

- **Kundesidens forretningsregler kan ikke udledes af det gamle projekt.** De skal defineres fra bunden i Fase 1 og tilføjes ledgeren som en ny serie (fx BR-C-01 og frem), tydeligt adskilt fra de porterede regler. `verify:ledger-audit` i Fase 3 skal kunne skelne "regel bevaret fra gammelt system" fra "regel opfundet nu".
- **Q-04 (RoomType med pris) er reelt afgjort.** En kunde kan ikke booke uden at se en pris. Prismodellen går fra åbent spørgsmål til forudsætning — og dermed også `SalesService` og omsætningsberegningen (BR-31, der i dag altid returnerer 0).
- **Q-39 (autorisationsmodel) er ikke længere valgfri.** Minimum to roller: personale og kunde. En kunde må kun se sine egne bookinger — det er en autorisationsregel, ikke en UI-tilstand, og den findes ikke i systemet i dag.
- **Angrebsfladen bliver offentlig.** Se Cybersecurity-sporet: rate limiting, kontoopregning, booking-spam.

| Id | Spørgsmål | Ejer |
|---|---|---|
| **Q-60** | Hvad kan en kunde på hjemmesiden? Søg + book, se egne bookinger, annullere, redigere profil? | Frontend + domæne |
| **Q-61** | Er `Guest` det samme som en kundekonto, eller er `User` en separat entitet knyttet til `Guest`? | Domæne — **blokerende for Fase 1** |
| **Q-62** | Må en kunde annullere eller ændre sin egen booking, og hvor tæt på ankomst? | Domæne. Ny regel — findes ikke i ledgeren |
| **Q-63** | Betaling på kundesiden, eller "betal ved ankomst"? | Domæne. Betaling ville trække `Payment`/`Invoice` ind (planens afsnit 7) |
| **Q-64** | Blazor render modes: static SSR for offentlige sider + interactive server for personale? | Frontend — se note nedenfor |
| **Q-65** | Skal kundesiden være offentligt tilgængelig og findbar (SEO)? | Frontend + DevOps |
| **Q-66** | Kan man booke uden konto (guest checkout), eller kræves login? | Domæne + Cybersecurity |
| **Q-67** | Rate limiting og bot-beskyttelse på offentlig booking? | Cybersecurity |
| **Q-68** | Hvem må se pasnumre? Kunden sit eget, personale alles? | Cybersecurity (skærper Q-38) |
| **Q-69** | E-mailbekræftelser ved booking? | Domæne + DevOps (SMTP i pipeline) |

**Note — Blazor Server og en offentlig side.** Blazor Server holder en SignalR-forbindelse pr. besøgende. Til 10 samtidige receptionister er det uproblematisk. Til en offentlig kundeside betyder det serverstate pr. anonym besøgende, følsomhed over for dårlige forbindelser, og ingen indeksérbar HTML uden videre. .NET 8+ løser det med render modes pr. komponent: static SSR på de offentlige sider, interactive server på personaleskærmene. Det bør besluttes i Fase 1b, ikke opdages i Fase 2.

---

## Frontend

*(Valdemars guild)*

| Id | Spørgsmål | Hvorfor det haster |
|---|---|---|
| Q-26 | `GuestPolicy` og `Sales` er tomme skærme med hver sit menupunkt. Bygges eller udgår? | Sales afhænger af Q-04 (priser) |
| Q-27 | "Payment Status" er hardkodet "Coming soon…" | Afhænger af om `Payment`/`Invoice` kommer med |
| Q-28 | Tomme tilstande — skal alle 7 skærme have dem i Blazor? | Komponentdesign |
| Q-17 | Uge- og månedsvisning filtrerer ikke på periode. Fejl eller feature? | Bevares reglen, eller rettes den? |
| Q-21 | Søgning låser visningen permanent til år | Samme |
| Q-23 | Enter i gæste-søgefeltet sorterer i stedet for at søge | Samme |
| Q-24 | "Cancel Booking **Permanently** From System?" modsiger soft-delete | Ordlyd eller adfærd? |
| Q-25 | CheckIn/CheckOut-fejl er usynlige for brugeren (kun `Debug.WriteLine`) | Skal rettes — status ændres in-memory før fejlen sluges |
| **Q-46** | Kalender-tidslinjen: hvor havner converter-logikken (BR-117 til BR-120)? | Se note nedenfor |
| **Q-47** | Komponentbibliotek: MudBlazor, Radzen, eller egne komponenter? | Låser hele Fase 2's Web-arbejde |
| **Q-48** | Blazor Server er stateful pr. bruger. Hvad sker der når to receptionister booker samme værelse samtidigt? | Realtidsopdatering eller optimistisk låsning |

**Note — kalenderen er den dyreste komponent.** `BookingOverviewView.xaml` er 1.093 linjer, og fire ægte forretningsregler ligger gemt i dens WPF-convertere: bredden er antal *overnatninger* (BR-117), bookinger tegnes fra midt på check-in-dagen (BR-118), ophold der starter før perioden klippes (BR-119), og bookinger uden rum udelades helt (BR-120). WPF-convertere findes ikke i Blazor — logikken skal et sted hen, og den bør ende i en ViewModel eller en ren beregningsklasse, ikke i Razor-markup. Det er den ene komponent hvor "genskrivning, ikke portering" gør mest ondt.

---

## AI Integration

| Id | Spørgsmål | Hvorfor det haster |
|---|---|---|
| **Q-49** | Hvad måler I på? Kontekstforbrug, wall-clock, antal fund pr. agent, fejl fanget af verifikationsagenten? | Skal besluttes **før** Fase 1, ellers er der ingen baseline |
| **Q-50** | Deterministisk `Workflow`-script eller prompt-drevet orkestrering? | Planens afsnit 5. Et script gør fan-out til kode og er lettere at dokumentere |
| **Q-51** | Er ledger-auditen (Fase 3) det primære bevis på at refaktoreringen er komplet? | Hvis ja, skal ledgeren fryses efter Fase 1 |

**Materiale I allerede har fra Fase 0** — det er værd at skrive ned nu, mens det er friskt:

- **Kontekstisolation virkede målbart.** Fem agenter læste hver sit område. `analyse:viewmodels` fandt 90 regler i ét pass uden at have set SQL, tests eller views.
- **Uafhængig verifikation fangede reelle modsigelser.** `analyse:tests` konkluderede af T-20/T-21 at man kan checke ind uden at bekræfte — og `analyse:viewmodels` nåede uafhængigt samme konklusion fra koden. To agenter uden fælles kontekst der peger på samme hul er stærkere bevis end én agent der læser alt.
- **Agenterne fandt ting det gamle hold ikke vidste:** to slettede tests (`DELETE TESTS` og `UPDATE TESTS` med tomme huller), 22 uoverensstemmelser mellem model, skema og diagrammer, og at README'en beskriver en anden version af koden end den der ligger der.
- **Snævre kontrakter gav ærlige svar.** Fire af fem agenter afsluttede med "det kan jeg ikke afgøre inden for mit læseområde" i stedet for at gætte — fx `analyse:repos`, der ikke kunne se hvem der instantierer repositories, og sagde det.
- **PostgreSQL-skiftet blev billigt.** Beslutningen kom efter analysen og ramte kun ét afsnit i planen og fire spørgsmål — fordi analysen var lagdelt fra start.

---

## Fælles domæne — kan ikke uddelegeres

Disse fire skal hele holdet afgøre sammen, før Fase 1.

| Id | Spørgsmål | Hvem det rammer |
|---|---|---|
| **Q-01** | Er `Confirmed` en forudsætning for check-in? I dag: nej (BR-06, bekræftet af test T-21) — men dialogen siger "Payment has been received" | Alle. Tilstandsmaskinen er kernen |
| **Q-02** | Frigiver en `CheckedOut` booking rummet for perioden? To modstridende regler i dag (BR-19 vs. BR-39) | Backend, Frontend, Mobil |
| **Q-03** | Hvor ligger overlapstjekket, og er det transaktionelt? Der findes **intet** i dag ved oprettelse | Backend, DevOps (DB-constraint) |
| **Q-04** | Skal `RoomType` med pris ind? | ✅ **Reelt afgjort af kundesiden: ja.** En kunde kan ikke booke uden pris. Tilbage står *hvordan* — pris pr. nat pr. `RoomType`, sæsonpriser, eller noget tredje |

**Q-61 er den nye dyreste.** Forholdet mellem `Guest` (en person i en booking) og `User` (nogen der kan logge ind) er fundamentet under både autorisation, kundesiden og JWT-udstedelsen. Vælger I forkert, rammer det Domain, Application, Api og begge klienter.

**Q-04 er ikke længere et ja/nej, men et hvordan.** Prismodellen ændrer stadig domænemodellen, ER-diagrammet og use case-listen — tre af Fase 1's leverancer.

---

## Restspørgsmål uden fast ejer

Ikke blokerende, men skal ikke gå tabt. Fuld beskrivelse i `OpenQuestions.md`.

- **Datamodel:** Q-05 (hvilket gammelt skema er reference), Q-05b (`date` vs. `timestamptz`), Q-10 (`BookingNumber` persisteret eller afledt), Q-11 (pasnummer obligatorisk — overlapper Q-38), Q-12 (dedupliker gæster på e-mail), Q-13 (enum-mapping), Q-14 (casing/navngivning), Q-15 (rumstatusser i XAML matcher ikke enum'en), Q-16 (seed-data)
- **PostgreSQL:** Q-29 (snake_case via `EFCore.NamingConventions`), Q-32 (brug `jsonb`/arrays eller hold skemaet portabelt)
- **Adfærdsfejl:** Q-18 (redigeret gæst mister sit id), Q-19 (ny gæst forsvinder ved næste søgning), Q-20 (eksisterende gæsts ændringer gemmes ikke), Q-22 (ingen øvre grænse på check-in)
