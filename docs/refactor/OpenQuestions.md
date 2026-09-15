# Åbne spørgsmål — Fase 0

> Samlet fra alle fem analyse-agenter. Skal besvares (eller bevidst parkeres) før Fase 1 / Opgave 1b.
> Q-01 til Q-08 er **blokerende** for målarkitekturen. Resten er noteret så de ikke går tabt.
> **Fordelt på valgfag: se `OpenQuestions-Tracks.md`** (DevOps, Cybersecurity, Mobilapp, Frontend, AI Integration) — samme Q-id'er, plus Q-33 til Q-51 rejst af fagopdelingen.

---

## Blokerende — kræver din beslutning

**Q-01 — Skal `Confirmed` være en forudsætning for check-in?**
I dag kan man checke ind direkte fra `Pending` (BR-06, bekræftet af test T-21), mens bekræftelsesdialogen selv siger "Payment has been received / Room is guaranteed" (`BookingOverviewViewModel.cs:499-508`). Enten er det en bevidst walk-in-regel, eller også er det et hul. Tilstandsmaskinen kan ikke låses uden svaret.

**Q-02 — Hvilken overlap-regel er den rigtige?**
Oprettelse ekskluderer kun `Cancelled` fra overlapstjekket (BR-39, `NewBookingViewModel.cs:204-209`); redigering ekskluderer `Cancelled` OG `CheckedOut` (BR-19, `BookingOverviewViewModel.cs:768-775`). Samme rum og periode kan afvises ét sted og accepteres et andet. Skal en `CheckedOut` booking frigive rummet for perioden eller ej?

**Q-03 — Hvor skal overlapstjekket ligge, og skal det være transaktionelt?**
Der findes i dag **intet** overlapstjek i `CreateBooking()` eller i `BookingRepo` — kun i listefiltreringen forinden. Dobbeltbooking er mulig. I ny kode hører reglen i `Domain/Rules/BookingRules` + et unikhedsværn i databasen; det skal besluttes om der skal en constraint/transaktion på.

**Q-04 — Skal `RoomType` med pris ind nu?**
Der findes ingen pris nogen steder i domænet. `Room.RoomSize` er fri tekst ('Single'/'Double'/'Suite' i data) uden enum, lookup-tabel eller CHECK-constraint. Konsekvenser: BR-31 (omsætning) kan ikke implementeres, og "Sales overview" kan ikke bygges meningsfuldt. Dette er spørgsmål 1 og 3 i planens afsnit 7.

**Q-05 — Hvilket af de to gamle skemaer er referencen? [nedgraderet 2026-09-02]**
Der findes to indbyrdes uforenelige SQL-sæt (`Database/` og `NFHotel/SQL/`) med samme filnavne og forskelligt indhold (U-15, U-16), og den faktiske kolonnetype for `ROOM.RoomNumber` og `BOOKING.StartDate/EndDate` afhænger af hvilke patch-scripts der er kørt (U-01, U-05).
**Efter PostgreSQL-beslutningen er dette ikke længere blokerende:** første migration skrives forfra mod PostgreSQL, så de gamle scripts er reference og ikke kilde. Tilbage står kun det semantiske spørgsmål: er `RoomNumber` en streng (svar: ja, det er C#-modellen og patch-scriptet enige om) og har datoerne brug for klokkeslæt (`date` vs. `timestamptz`)? Det sidste hænger sammen med Q-22 og BR-118 (halvdags-reglen i kalenderen).

**Q-05b — Skal datoer være `date` eller `timestamptz`?**
`Booking.StartDate`/`EndDate` bruges i dag som rene datoer (BR-39 overlapstjek, BR-109 antal nætter), mens `CheckInTime`/`CheckOutTime` er rigtige tidsstempler (BR-07, BR-10). I PostgreSQL er `date` + `timestamptz` det naturlige valg — men `timestamptz` kræver en beslutning om tidszone (hotellet kører i Europe/Copenhagen; `DateTime.Now` i den gamle kode er lokal tid uden zoneinfo).

**Q-06 — Testframework: MSTest eller xUnit?** (planens afsnit 7, spørgsmål 2)
Relevant kontekst: de 22 tests i `BookingOverviewViewModelTests` er den eneste rigtige facit-kilde og bruger Moq mod `IBookingRepo`/`IRoomRepo` — de kan porteres til begge frameworks næsten uændret. De 40 repo-tests kan ikke porteres som de er (se Q-07).

**Q-07 — Hvad gør vi med de 40 DB-afhængige repo-tests?**
De rammer `HotelBooking` — **samme database som applikationen** — uden transaktion-rollback, med `Parallelize(MethodLevel)` og hardkodede nøgler (`"999"`, `TEST101`). De er hverken uafhængige eller gentagelige. Valg: (a) skriv om til EF InMemory, (b) behold som integrationstests mod en dedikeret PostgreSQL-container, (c) drop dem og dæk reglerne i Application-laget i stedet.
**Note efter PostgreSQL-beslutningen:** SQLite som teststand-in bliver mindre attraktivt, fordi den ikke deler typesystem med PostgreSQL (jsonb, arrays, `timestamptz`). Testcontainers med `postgres:16` giver testene samme database som produktionen og er værd at overveje frem for InMemory — planens oprindelige "EF InMemory/SQLite" bør revurderes i Fase 1b.

**Q-08 — ASP.NET Identity nu eller senere?** (planens afsnit 7, spørgsmål 4)
Bemærk at der i dag ikke findes en eneste adgangsregel knyttet til en bruger — alle BR-regler af typen "adgang" handler om UI-tilstand, ikke om hvem der er logget ind. Der er intet `Employee`/`User`-begreb overhovedet.

---

## Sikkerhed — skal håndteres uanset

**Q-09 — Azure SQL-credentials i klartekst.** `Database/README.md:18` indeholder et databasepassword i klartekst, i modstrid med filens eget sikkerhedsafsnit (`:77-91`). Skal fjernes og roteres, jf. planens afsnit 2. Bemærk også at `appsettings.json` ikke findes i repoet (hverken i WPF-projektet eller testprojektet), så det er uklart om den nogensinde har været committet.

---

## Datamodel og skema

- **Q-10** — `Booking.BookingNumber` (`FLZ-{BookingID:D6}`) findes kun i C#, ikke i SQL eller diagrammer (U-11). Skal det være et persisteret felt i ny kode, eller forblive afledt?
- **Q-11** — `Guest.PassportNumber` er nullable i SQL, ikke valideret i C#, men altid udfyldt i testdata (U-22, BR-57, T-14). Obligatorisk eller valgfrit i ny kode?
- **Q-12** — `Guest.Email` har ingen UNIQUE-constraint. Skal gæster dedupliceres på e-mail? (Relaterer til BR-47: en gæst med `GuestID == 0` oprettes altid som ny — der findes ingen "findes gæsten allerede?"-logik.)
- **Q-13** — Enum-værdier har ingen håndhævelse i databasen (U-18) — hverken CHECK-constraint eller lookup-tabel. EF Core-konfiguration skal beslutte: int, string, lookup-tabel — eller en **rigtig PostgreSQL-enum** (`CREATE TYPE booking_status AS ENUM (...)`), som Npgsql understøtter direkte og som giver håndhævelse i databasen uden ekstra tabel. Bemærk at ordinalværdierne 0-4 findes i eksisterende data, så en int-mapping er den mest bagudkompatible.
- **Q-14** — Casing-inkonsistens: C# `Room.RoomId` vs. `Booking.RoomID` vs. SQL `RoomID` (U-19). Vælg én standard (Conventions.md siger "Primærnøgler: `Id` eller `[Entity]Id` — vælg én standard"). **Skærpet af PostgreSQL:** ikke-quotede identifiers foldes til lowercase, så `RoomID` bliver `roomid` medmindre alt quotes. Anbefaling: PascalCase i C#, snake_case i databasen via `EFCore.NamingConventions` — ét sted, ingen quoting.
- **Q-15** — Værelsesstatusserne i rumformularens XAML (Available / Maintanance / Cleaning / Disabled, med stavefejl) matcher ikke enum'en `RoomStatus` (Available / OutOfService / Maintenance) — BR-126. Hvilket sæt er det rigtige? Ligger der "Cleaning"/"Disabled"-værdier i databasen?
- **Q-16** — `Database/03_InsertExtendedTestData.sql` (~99 bookinger over 2025-2028) er ikke nævnt i nogen README og forudsætter en bestemt kørerækkefølge. Skal testdata porteres til EF seed-data, og i så fald hvilket datasæt?

---

## Adfærd der ser ud som fejl (skal reglen bevares eller rettes?)

- **Q-17** — **Uge- og månedsvisning filtrerer ikke på periode.** Kun årsvisningen gør (BR-24, `:389-402`). I uge/måned vises ALLE ikke-annullerede bookinger. Utilsigtet?
- **Q-18** — **Redigeret gæst mister sit id.** `OnEditGuest()` kopierer seks felter men ikke `GuestID` (BR-66), hvilket giver `UpdateGuest` på id 0. Ser ud til at være en fejl.
- **Q-19** — **`AddGuestToOverview` opdaterer ikke den bagvedliggende liste** — en netop oprettet gæst forsvinder ved næste søgning.
- **Q-20** — **Eksisterende gæsts ændringer gemmes ikke ved ny booking** (BR-47) — retter man en eksisterende gæsts oplysninger i bookingformularen, går de tabt.
- **Q-21** — **Søgning låser visningen permanent til år** (BR-26) — der skiftes ikke tilbage når søgefeltet ryddes.
- **Q-22** — **Ingen øvre tidsgrænse på check-in** (BR-06) — en booking hvis `EndDate` for længst er passeret kan stadig checkes ind.
- **Q-23** — **Enter i gæste-søgefeltet udløser sortering, ikke søgning** (`GuestOverviewView.xaml.cs:68-77`). Navne-/adfærdsforveksling?
- **Q-24** — **Dialogteksten "Cancel Booking Permanently From System?"** modsiger den faktiske soft-delete (BR-13). Ordlyd eller adfærd der skal rettes?
- **Q-25** — **CheckIn/CheckOut-fejl er usynlige for brugeren** — skrives kun til `Debug.WriteLine` (`:592`, `:639`), selvom `Status` allerede er ændret in-memory før `Update` kaldes. Inkonsistent state ved fejl.

---

## PostgreSQL-specifikke valg (tilføjet 2026-09-02)

- **Q-29** — Navngivningsstrategi: `EFCore.NamingConventions` med snake_case, eller quotede PascalCase-identifiers? (Se Q-14. Anbefaling: snake_case.)
- **Q-30** — Testdatabase: Testcontainers med `postgres:16`, en fast lokal container, eller EF InMemory? (Se Q-07.)
- **Q-31** — Hosting i Fase 2/deployment-diagrammet: lokal Docker er givet, men hvad er målet — managed PostgreSQL (Azure Database for PostgreSQL, Supabase, Neon) eller ingenting ud over lokalt? Påvirker komponent-/deployment-diagrammet.
- **Q-32** — Skal vi bruge PostgreSQL-specifikke features overhovedet (`jsonb`, arrays, full-text search), eller holde skemaet portabelt? Ved et hobby-/eksamensprojekt taler enkelhed for portabelt — men `jsonb` kunne være relevant hvis `Service`/add-ons (planens afsnit 7) kommer med.

---

## Scope

- **Q-26** — `GuestPolicyViewModel` og `SalesOverviewViewModel` er **helt tomme klasser** med hver sin placeholder-skærm og sit menupunkt. Skal de to features bygges i det nye projekt, eller udgår de? (Sales afhænger af Q-04.)
- **Q-27** — "Payment Status"-boksen i bookingdetaljerne er hardkodet "Coming soon..." (`BookingOverviewView.xaml:599-602`). Skal `Payment`/`Invoice` med? (planens afsnit 7, spørgsmål 1)
- **Q-28** — Ingen af de 7 views har tilstand for "ingen data" ud over booking-detaljepanelet. Skal tomme tilstande med i Blazor-versionen?

---

## Noteret, ikke blokerende

- Dokumentationen i `NFHotel/Documentation/Diagrams/` er delvist forkert: ERD mangler `Status` på både ROOM og BOOKING (U-08) og er syntaktisk ufuldstændig (U-09); sekvensdiagrammet bruger en `Booking`-konstruktør der ikke findes (U-13) og en samlet INSERT der ikke findes (U-14). Planens afsnit 9 siger allerede: lav ikke nye as-is-diagrammer — lav to-be og sammenlign.
- `DateGreaterThanAttribute` bruges **ingen steder** — hverken på modeller eller i ViewModels. Kan formentlig droppes i ny kode (dens regel er BR-88, dækket af BR-103/BR-108).
- `RoomRepo` bruger stored procedures mens `BookingRepo`/`GuestRepo` bruger inline-SQL, uden forklaring. Filterlogikken i `uspGetRoomsFromCriteria` er dermed ikke verificerbar fra C#-koden — den skal læses fra SQL-scriptet før `GetRoomsFromCriteria` genimplementeres i EF.
- `GuestRepo.GetByID`/`GetAll` mapper via hardkodede kolonneindeks 0-6 (`GuestRepo.cs:66-72`) — brækker lydløst hvis SELECT-rækkefølgen ændres.
- To positive tests ser ud til at være **slettet** fra testsuiten: der findes ingen test der beviser at man kan slette en booking eller opdatere et værelse med succes (`BookingRepoTests.cs:325-330`, `RoomRepoTests.cs:333-339`). Hullet må ikke forveksles med "reglen findes ikke".
- `NFHotel_Tests/README.md` kan ikke bruges som autoritativ kilde: forkert testtal, forkerte pakkeversioner, henviser til fire dokumenter der ikke findes, og er delvist en indsat chatsamtale.
- `Microsoft.Extensions.Configuration*` er ikke refereret i testprojektets `.csproj` — det bygger kun via transitiv afhængighed gennem projektreferencen til WPF-projektet.
