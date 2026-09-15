# Architecture-Domain.md — bilag

> Fuldt output fra agenten `design:domain` (Fase 1). **Bilag.** `Architecture.md` har forrang hvor de modsiger hinanden — se især afgørelserne A-01 til A-15.

# Domænedesign — `NFHotel.Domain`

> Fase 1, `design:domain`. Read-only designdokument. Ingen kode skrevet.
> Input: `BusinessRules.md` (126 regler), `Analysis.md` §1, `Conventions.md`, `Decisions.md`, `Fase1-Scope.md`.
> Præmisser B-01..B-07 er lagt til grund uændret.

---

## 0. Projektstruktur

Feature-først, ikke type-først (Conventions.md: *"Organisér efter feature først"*, *"Hold namespaces identiske med mappestruktur"*, *"1 public class pr. fil"*).

```
NFHotel.Domain/
├── Common/
│   ├── DateRange.cs                 // value object
│   ├── DomainException.cs           // base
│   └── InvalidStateTransitionException.cs
├── Bookings/
│   ├── Booking.cs
│   ├── BookingStatus.cs
│   └── BookingRules.cs              // rene funktioner, ingen state
├── Guests/
│   ├── Guest.cs
│   └── GuestRules.cs
└── Rooms/
    ├── Room.cs
    ├── RoomStatus.cs                // bookbarhed
    ├── HousekeepingStatus.cs        // rengøringsforløb
    ├── RoomSize.cs
    └── RoomRules.cs
```

Namespaces: `NFHotel.Domain.Bookings` osv. Ingen `using` mod andre projekter — ingen NuGet-pakker overhovedet. `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>` anbefales for netop dette projekt.

**Håndhævelse af "ingen afhængigheder":** ingen `DateTime.Now`/`DateTimeOffset.UtcNow` nogen steder i Domain. Alle regler der har brug for "i dag" får det som parameter. Det er den eneste måde tilstandsmaskinen kan unit-testes deterministisk, og det er en direkte konsekvens af Conventions.md's krav om at Domain ikke må kende til framework/omverden.

---

## 1. Enums

### 1.1 `BookingStatus` — uændret (B-06)

```csharp
namespace NFHotel.Domain.Bookings;

/// <summary>
/// Bookingens livscyklus. Ordinalværdierne 0-4 er bevaret fra det gamle system,
/// fordi de allerede findes som int i eksisterende data (jf. Fase1-Scope pkt. 4).
/// Værdierne må aldrig omnummereres uden en datamigrering.
/// </summary>
public enum BookingStatus
{
    /// <summary>Oprettet, men ikke betalingsgaranteret. Startstatus for enhver booking (BR-01).</summary>
    Pending = 0,

    /// <summary>Betaling garanteret. Er IKKE en forudsætning for check-in (B-01).</summary>
    Confirmed = 1,

    /// <summary>Gæsten er tjekket ind.</summary>
    CheckedIn = 2,

    /// <summary>Gæsten er tjekket ud. Frigiver ikke rummet i perioden (B-02).</summary>
    CheckedOut = 3,

    /// <summary>Annulleret. Soft-delete — rækken slettes aldrig (BR-13). Eneste status der frigiver rummet (B-02).</summary>
    Cancelled = 4
}
```

### 1.2 `RoomStatus` — bookbarhed (B-04)

Ordinalværdierne 0-2 bevares fra `Models/Enums/RoomStatus.cs`. Kun **betydningen** indsnævres: enum'en svarer nu udelukkende på spørgsmålet *"må rummet udlejes?"*. Det koster nul datamigrering, og det er hele pointen i F-01 at undgå netop den.

```csharp
namespace NFHotel.Domain.Rooms;

/// <summary>
/// Rummets bookbarhed — udelukkende det der forhindrer udlejning.
/// Rengøringstilstand hører i <see cref="HousekeepingStatus"/> (B-04).
/// Ordinalværdierne 0-2 er bevaret fra det gamle system.
/// </summary>
public enum RoomStatus
{
    /// <summary>Kan bookes. Siger intet om hvorvidt rummet er rent lige nu.</summary>
    Available = 0,

    /// <summary>Taget ud af drift på ubestemt tid (ombygning, omdannet til depot). Administrativ beslutning.</summary>
    OutOfService = 1,

    /// <summary>Midlertidigt spærret af en fejl der gør rummet ubeboeligt. Forventes at vende tilbage til Available.</summary>
    Maintenance = 2
}
```

**Hvad der er droppet fra den gamle XAML-dropdown (BR-126 løses her):** `Cleaning` var aldrig en bookbarhedstilstand og flytter til `HousekeepingStatus`. `Maintanance` var en stavefejl for `Maintenance`. `Disabled` er semantisk identisk med `OutOfService` og får ingen egen værdi. Dermed er der ét sæt tilladte værdier, og UI kan bindes direkte til enum'en i stedet for til en hardkodet stringliste.

### 1.3 `HousekeepingStatus` — rengørings- og serviceforløb (B-04, D-10)

Ny enum. Ingen eksisterende data at tage hensyn til, så nummereringen er fri; jeg starter på 0 = `Clean`, så `default(HousekeepingStatus)` og databasens `DEFAULT 0` betyder det samme som `RoomStatus.Available`: "intet i vejen".

```csharp
namespace NFHotel.Domain.Rooms;

/// <summary>
/// Rummets rengørings- og servicestand. Opdateres af rengøringspersonale og
/// serviceteknikere (D-10). Påvirker IKKE bookbarhed — et rum kan være
/// <see cref="RoomStatus.Available"/> og samtidig afvente rengøring (B-04).
/// </summary>
public enum HousekeepingStatus
{
    /// <summary>Rent og klar til gæst. Startværdi for et nyoprettet rum.</summary>
    Clean = 0,

    /// <summary>Beboet rum der afventer daglig rengøring.</summary>
    DailyCleaningDue = 1,

    /// <summary>Gæsten er rejst; rummet afventer slutrengøring.</summary>
    DepartureCleaningDue = 2,

    /// <summary>Rengøringspersonalet har kvitteret og er i gang.</summary>
    CleaningInProgress = 3,

    /// <summary>Et problem er rapporteret; rummet afventer servicetekniker.</summary>
    ServiceRequired = 4,

    /// <summary>Serviceteknikeren har kvitteret og er i gang.</summary>
    ServiceInProgress = 5
}
```

**Hvorfor service ligger i samme enum som rengøring, og ikke i `RoomStatus`:**
`ServiceRequired` betyder "nogen skal kigge på det" — en dryppende hane, en pære der er gået. Det forhindrer ikke udlejning. Skal rummet spærres, er det en *separat, bevidst* handling: `RoomStatus` sættes til `Maintenance`. Præcis dén adskillelse er det gamle system aldrig fik lavet, og det er derfor `Available/Maintenance/OutOfService` var inkohærent. To enums, to spørgsmål, ingen overlap.

**Bevidst udeladt:** et `Inspected`/godkendelsestrin for ejer/manager. D-10 nævner tre aktiviteter (daglig rengøring, service, slutrengøring), ikke et fjerde inspektionstrin. Tilføjes som ny enum-værdi når mobil-guilden beder om det — nye enum-værdier på enden er billige, omnummerering er dyr. Se §8.

### 1.4 `RoomSize` — ny enum (Fase1-Scope, "billig insurance")

`RoomSize` er de facto en enum i data (`'Single'/'Double'/'Suite'`) men lever som fri tekst i både C# og SQL (Analysis §1, uklart pkt. 5). Fase1-Scope listerdet eksplicit som billig insurance: *"Lad `RoomSize` blive en enum eller lookup, ikke en fri streng."* Samme arbejde nu, migrering senere.

```csharp
namespace NFHotel.Domain.Rooms;

/// <summary>
/// Værelsestype. Erstatter det gamle fritekst-felt <c>Room.RoomSize</c>.
/// Ordinalværdier er nye — der er ingen eksisterende int-data at bevare.
/// Bliver senere ophæng for <c>RoomType</c> med pris (Fase1-Scope, udskudt).
/// </summary>
public enum RoomSize
{
    Single = 0,
    Double = 1,
    Suite = 2
}
```

> **Afhængighed til infrastruktur-agenten:** eksisterende `NVARCHAR(20)`-data skal mappes til int i migreringen (`'Single'→0` osv.). Det er ikke mit design, men det skal videregives.

---

## 2. Value object: `DateRange`

**Vurdering (KISS-tjek, jf. opgavens pkt. 3):** ja, den bærer sin vægt. Uden den ligger reglen "slutdato skal være strengt efter startdato" fire steder i det gamle system (BR-38, BR-43, BR-88, BR-103), overlapsreglen to steder med to forskellige definitioner (BR-19 vs. BR-39), og `NumberOfNights` beregnes ét sted og reimplementeres i en converter (BR-109 vs. BR-117). Én type samler dem alle.

**Men den persisteres ikke som owned type.** `Booking` beholder `StartDate` og `EndDate` som to `DateOnly`-kolonner præcis som i dag, og eksponerer `Period` som afledt værdi. Det giver reglerne ét hjem uden at pålægge EF-agenten en owned-type-mapping, og uden at ændre kolonnenavne. Value object'et er et *regelsted og en parametertype*, ikke en lagringsstruktur.

```csharp
namespace NFHotel.Domain.Common;

/// <summary>
/// En bookingperiode: ankomstdato (inklusiv) til afrejsedato (eksklusiv).
/// Halvåbent interval — afrejse og ankomst samme dag er ikke overlap (B-03).
/// Typen kan ikke konstrueres i en ugyldig tilstand.
/// </summary>
public readonly record struct DateRange
{
    /// <summary>Ankomstdato, inklusiv.</summary>
    public DateOnly Start { get; }

    /// <summary>Afrejsedato, eksklusiv. Altid strengt større end <see cref="Start"/>.</summary>
    public DateOnly End { get; }

    /// <summary>
    /// Opretter en periode.
    /// </summary>
    /// <param name="start">Ankomstdato.</param>
    /// <param name="end">Afrejsedato. Skal være strengt efter <paramref name="start"/>.</param>
    /// <exception cref="DomainException">
    /// Kastes hvis en dato er <c>default</c>, eller hvis <paramref name="end"/> ikke er efter
    /// <paramref name="start"/> (0 nætter er ikke tilladt) — BR-101, BR-102, BR-103.
    /// </exception>
    public DateRange(DateOnly start, DateOnly end);

    /// <summary>Antal overnatninger. Erstatter <c>Booking.NumberOfNights</c> (BR-109, BR-117).</summary>
    public int Nights => End.DayNumber - Start.DayNumber;

    /// <summary>
    /// Halvåben overlapstest: <c>Start &lt; other.End &amp;&amp; End &gt; other.Start</c> (B-03).
    /// Ren funktion — kræver ingen database.
    /// </summary>
    public bool Overlaps(DateRange other);

    /// <summary>Sand hvis <paramref name="date"/> ligger i intervallet [Start, End).</summary>
    public bool Contains(DateOnly date);

    /// <summary>Sand hvis perioden starter på eller efter <paramref name="today"/> (BR-104, BR-44).</summary>
    public bool StartsOnOrAfter(DateOnly today) => Start >= today;
}
```

`readonly record struct` giver værdilighed, `ToString()` og uforanderlighed gratis, uden en klasse-allokering pr. sammenligning i et overlapstjek der kan køre over hundredvis af bookinger.

**Value objects jeg bevidst IKKE laver:**

| Kandidat | Hvorfor ikke |
|---|---|
| `EmailAddress` | BR-93 er præcis "indeholder `@` og `.`". En VO ville friste til at stramme til regex — det ville bryde paritet med det gamle system og fejle test T-13's søskende. Reglen bor i `GuestRules.IsValidEmail`. |
| `PassportNumber` | B-07 siger feltet ikke må være søgenøgle og skal kunne krypteres. En VO gør hverken fra eller til for det — den tilføjer kun et lag mellem entiteten og en kolonne som EF-agenten skal kryptere. `string?` er det rigtige. |
| `RoomNumber` | Ingen regel ud over "ikke tom" (BR-97). En VO for én whitespace-check er ceremoni. |
| `Money`/`Price` | Der findes ingen pris i domænet (BR-31, Analysis §Sammenfatning). Tilføjes når `RoomType` med pris bygges. |

---

## 3. Entiteter

Alle tre følger samme mønster: private setters, privat parameterløs konstruktør til EF, statisk `Create`-factory der håndhæver invarianterne, og adfærdsmetoder i stedet for offentlige settere. Det er dét der gør BR-84 ("valideringen ignoreres") strukturelt umulig at gentage.

### 3.1 `Booking`

```csharp
namespace NFHotel.Domain.Bookings;

/// <summary>
/// En reservation af ét rum til én gæst i én periode.
/// Aggregatrod. Kender kun sine egne felter — regler der kræver kendskab til
/// andre bookinger (fx overlap mod eksisterende) hører i Application-laget.
/// </summary>
public sealed class Booking
{
    private Booking() { }   // EF

    /// <summary>Primærnøgle. 0 indtil bookingen er persisteret.</summary>
    public int BookingId { get; private set; }

    /// <summary>Ankomstdato uden klokkeslæt (B-05).</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>Afrejsedato uden klokkeslæt, eksklusiv (B-05).</summary>
    public DateOnly EndDate { get; private set; }

    /// <summary>Faktisk indtjekningstidspunkt. Null indtil check-in (B-05).</summary>
    public DateTimeOffset? CheckInTime { get; private set; }

    /// <summary>Faktisk udtjekningstidspunkt. Null indtil check-ud (B-05).</summary>
    public DateTimeOffset? CheckOutTime { get; private set; }

    /// <summary>Bookingens livscyklustilstand. Ændres kun via metoderne på klassen.</summary>
    public BookingStatus Status { get; private set; }

    /// <summary>Fremmednøgle til rummet. Altid &gt; 0 (BR-105).</summary>
    public int RoomId { get; private set; }

    /// <summary>Fremmednøgle til gæsten. Altid &gt; 0 (BR-106).</summary>
    public int GuestId { get; private set; }

    /// <summary>Navigation. Kun udfyldt når kaldet har bedt om den; brug aldrig som guard.</summary>
    public Room? Room { get; private set; }

    /// <summary>Navigation. Kun udfyldt når kaldet har bedt om den; brug aldrig som guard.</summary>
    public Guest? Guest { get; private set; }

    /// <summary>Bookingens periode som value object.</summary>
    public DateRange Period => new(StartDate, EndDate);

    /// <summary>Antal overnatninger (BR-109, BR-117).</summary>
    public int NumberOfNights => Period.Nights;

    /// <summary>Vist bookingnummer, formatet <c>FLZ-000123</c> (BR-109). Afledt, aldrig persisteret.</summary>
    public string BookingNumber => $"{BookingNumberPrefix}{BookingId:D6}";

    private const string BookingNumberPrefix = "FLZ-";
}
```

**Beslutninger og begrundelser pr. felt**

| Felt | Før | Nu | Hvorfor |
|---|---|---|---|
| `BookingID` | `int` | `BookingId` | Conventions.md: ét PK-navnemønster. Løser U-19's casinguoverensstemmelse konsekvent på tværs af alle tre entiteter. |
| `StartDate`/`EndDate` | `DateTime` | `DateOnly` | B-05. Fjerner U-05 (`DATE` vs. `DATETIME2`) og hele `.Date`-sammenlignings-familien (BR-44, BR-104, BR-108). |
| `CheckInTime`/`CheckOutTime` | `DateTime?` | `DateTimeOffset?` | B-05. Et faktisk tidsstempel med offset; ingen tvetydighed ved sommertid. |
| `RoomID`/`GuestID` | `int`, kunne være 0 hvis navigation var sat (BR-105/106) | `int`, altid > 0 | Den gamle "enten navigation eller FK"-regel var et symptom på manglende factory. Domænet kræver FK'en; navigationen er en bekvemmelighed. |
| `Room`/`Guest` | `Room`/`Guest` (ikke-nullable, men reelt null) | `Room?`/`Guest?` | Conventions.md: nullable reference types. Navigationen *er* null ved lazy/ikke-inkluderet load — typen skal sige sandheden. |
| `BookingNumber` | afledt string | uændret | U-11: findes kun i kode. Korrekt — det er en visningsidentitet, ikke data. |
| `NumberOfNights` | `(End - Start).Days`, 0 hvis default | `Period.Nights` | `default` kan ikke længere opstå, fordi `DateRange` afviser den. Særtilfældet forsvinder. |

**Factory og adfærd**

```csharp
    /// <summary>
    /// Opretter en ny booking med status <see cref="BookingStatus.Pending"/> (BR-01, BR-48).
    /// </summary>
    /// <param name="period">Bookingperioden. Skal starte på eller efter <paramref name="today"/>.</param>
    /// <param name="roomId">Rummets id. Skal være større end 0.</param>
    /// <param name="guestId">Gæstens id. Skal være større end 0.</param>
    /// <param name="today">Dagens dato i hotellets tidszone. Leveres af kaldet — Domain kender ikke uret.</param>
    /// <returns>En ny, gyldig booking der endnu ikke er persisteret.</returns>
    /// <exception cref="DomainException">
    /// Ved startdato i fortiden (BR-44, BR-104), manglende rum (BR-45, BR-105)
    /// eller manglende gæst (BR-106).
    /// </exception>
    public static Booking Create(DateRange period, int roomId, int guestId, DateOnly today);

    /// <summary>Sand hvis bookingen må bekræftes (BR-02, BR-121).</summary>
    public bool CanConfirm => Status == BookingStatus.Pending;

    /// <summary>Sand hvis bookingen må tjekkes ind på den angivne dato (BR-06). Se B-01: Confirmed er ikke et krav.</summary>
    public bool CanCheckIn(DateOnly today) =>
        (Status is BookingStatus.Pending or BookingStatus.Confirmed)
        && CheckInTime is null
        && StartDate <= today;

    /// <summary>Sand hvis bookingen må tjekkes ud (BR-09).</summary>
    public bool CanCheckOut =>
        Status == BookingStatus.CheckedIn && CheckInTime is not null && CheckOutTime is null;

    /// <summary>Sand hvis bookingen må annulleres (BR-11, BR-122).</summary>
    public bool CanCancel => Status is BookingStatus.Pending or BookingStatus.Confirmed;

    /// <summary>Sand hvis bookingens datoer og rum må ændres (BR-14, BR-123).</summary>
    public bool CanReschedule => Status is BookingStatus.Pending or BookingStatus.Confirmed;
```

De fem `Can*`-medlemmer er den konsolidering BusinessRules.md efterlyser: BR-02/BR-121, BR-11/BR-122 og BR-14/BR-123 var samme regel skrevet både i ViewModel og som XAML-trigger. Nu findes hver af dem ét sted, og både Blazor-UI (til at disable en knap) og Application-servicen (til at afvise et kald) læser fra samme kilde. BR-22 bliver dermed triviel — der er intet at "genberegne", værdierne er properties.

### 3.2 `Room`

```csharp
namespace NFHotel.Domain.Rooms;

/// <summary>
/// Et fysisk værelse. Bærer to uafhængige tilstande: bookbarhed
/// (<see cref="Status"/>) og rengøringsstand (<see cref="Housekeeping"/>) — B-04.
/// </summary>
public sealed class Room
{
    private Room() { }   // EF

    /// <summary>Primærnøgle.</summary>
    public int RoomId { get; private set; }

    /// <summary>Værelsesnummer som tekst, fx "101" eller "12B" (BR-97). Løser U-01 til fordel for C#-typen.</summary>
    public string RoomNumber { get; private set; } = string.Empty;

    /// <summary>Etage. Skal være større end 0 (BR-98).</summary>
    public int Floor { get; private set; }

    /// <summary>Værelsestype. Var fri tekst; er nu enum.</summary>
    public RoomSize Size { get; private set; }

    /// <summary>Antal personer rummet kan rumme. Skal være større end 0 (BR-100).</summary>
    public int Capacity { get; private set; }

    /// <summary>Bookbarhed. Ændres kun via <see cref="TakeOutOfService"/>, <see cref="SendToMaintenance"/> og <see cref="ReturnToService"/>.</summary>
    public RoomStatus Status { get; private set; }

    /// <summary>Rengørings- og servicestand. Ændres kun via housekeeping-metoderne.</summary>
    public HousekeepingStatus Housekeeping { get; private set; }

    /// <summary>
    /// Sand hvis rummet overhovedet må udlejes (BR-110). Afhænger UDELUKKENDE af
    /// <see cref="Status"/> — rengøringsstand er bevidst ikke med (B-04).
    /// </summary>
    public bool IsBookable => Status == RoomStatus.Available;
}
```

`RoomSize` → `Size`: property-navnet `RoomSize` på typen `Room` var stammer-gentagelse (`room.RoomSize`), og typen hedder nu `RoomSize`. `room.Size` af typen `RoomSize` læser bedre. Kolonnenavnet er infrastruktur-agentens beslutning.

```csharp
    /// <summary>
    /// Opretter et nyt rum. Status sættes til <see cref="RoomStatus.Available"/> (BR-81)
    /// og rengøringsstand til <see cref="HousekeepingStatus.Clean"/>.
    /// </summary>
    /// <exception cref="DomainException">
    /// Ved tomt værelsesnummer (BR-97), etage ≤ 0 (BR-98), ukendt <paramref name="size"/> (BR-99)
    /// eller kapacitet ≤ 0 (BR-100).
    /// </exception>
    public static Room Create(string roomNumber, int floor, RoomSize size, int capacity);

    /// <summary>Opdaterer rummets stamdata. Rører hverken status eller rengøringsstand.</summary>
    /// <exception cref="DomainException">Samme invarianter som <see cref="Create"/>.</exception>
    public void UpdateDetails(string roomNumber, int floor, RoomSize size, int capacity);
```

Bemærk at `Create`/`UpdateDetails` kaster i stedet for at returnere en fejlliste. Det er dét der gør BR-84 umulig at gentage: der findes ingen vej til et persisteret, ugyldigt rum, og repoets `Room.Validate()`-tjek (BR-113) kan udgå.

### 3.3 `Guest`

```csharp
namespace NFHotel.Domain.Guests;

/// <summary>
/// En gæst. Ren domæneentitet — bevidst IKKE en login-/kontoentitet
/// (Fase1-Scope, "billig insurance").
/// </summary>
public sealed class Guest
{
    private Guest() { }   // EF

    /// <summary>Primærnøgle. Ændres aldrig efter oprettelse (BR-72).</summary>
    public int GuestId { get; private set; }

    /// <summary>Fornavn. Aldrig tomt eller kun whitespace (BR-91).</summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>Efternavn. Aldrig tomt eller kun whitespace (BR-92).</summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>E-mail. Indeholder altid både '@' og '.' (BR-93).</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Telefonnummer. Aldrig tomt; intet formatkrav (BR-94).</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    /// <summary>Land. Aldrig tomt (BR-95).</summary>
    public string Country { get; private set; } = string.Empty;

    /// <summary>
    /// Pasnummer. Valgfrit og uvalideret (BR-57, U-22).
    /// Behandles som følsomt: må ikke bruges som søge- eller sorteringsnøgle (B-07, D-04).
    /// Domænet kender ikke krypteringen — den er infrastrukturens ansvar.
    /// </summary>
    public string? PassportNumber { get; private set; }

    /// <summary>Fuldt navn, "Fornavn Efternavn". Bruges af Application-lagets søgning (BR-25, BR-68).</summary>
    public string FullName => $"{FirstName} {LastName}";
}
```

```csharp
    /// <summary>Opretter en ny gæst.</summary>
    /// <exception cref="DomainException">Ved brud på BR-91..BR-95.</exception>
    public static Guest Create(
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        string country,
        string? passportNumber);

    /// <summary>
    /// Overskriver præcis de seks redigérbare felter. <see cref="GuestId"/> ændres aldrig (BR-72).
    /// </summary>
    /// <exception cref="DomainException">Ved brud på BR-91..BR-95.</exception>
    public void UpdateDetails(
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        string country,
        string? passportNumber);
```

**`FullName` som computed property er bevidst.** BR-25 og BR-68 søger begge i "fornavn efternavn"; BR-69 sorterer på det. Beregningen hører i Domain, filtreringen og sorteringen i Application. Conventions.md's forbud mod UI-formatering i domænemodeller er ikke brudt: `FullName` er en domæneidentitet, ikke en visningsstreng — der er ingen kultur, intet format, ingen afkortning.

**Længdebegrænsninger — ny regel N-01.** Analysis §1 noterer at C# ingen længdekontrol har mod `NVARCHAR(100)`/`(50)`. Jeg foreslår `public const int MaxNameLength = 100;` osv. på `Guest`/`Room` og en guard i `Create`/`UpdateDetails`, så en for lang værdi fejler i Domain i stedet for som en databaseexception. Det er en **ny regel uden BR-id** — flagget her, så ledger-auditen i Fase 3 ikke bogfører den som en uforklaret afvigelse. Konstanterne kan genbruges af EF-konfigurationen (infrastruktur-agentens valg).

---

## 4. Regelklasser (rene funktioner)

```csharp
namespace NFHotel.Domain.Bookings;

/// <summary>
/// Statsløse bookingregler. Rene funktioner uden database- eller tidsafhængighed,
/// så de kan unit-testes direkte (B-03).
/// </summary>
public static class BookingRules
{
    /// <summary>
    /// Halvåben overlapstest mellem to perioder: <c>aStart &lt; bEnd &amp;&amp; aEnd &gt; bStart</c>.
    /// Afrejse- og ankomstdag samme dag er IKKE overlap (B-03, BR-39).
    /// </summary>
    public static bool Overlaps(DateRange a, DateRange b) => a.Overlaps(b);

    /// <summary>
    /// Sand hvis en booking med den angivne status blokerer rummet i sin periode.
    /// Kun <see cref="BookingStatus.Cancelled"/> frigiver rummet — <see cref="BookingStatus.CheckedOut"/>
    /// gør ikke (B-02, løser modstriden BR-19 vs. BR-39 til fordel for BR-39).
    /// </summary>
    public static bool BlocksRoom(BookingStatus status) => status != BookingStatus.Cancelled;

    /// <summary>
    /// Sand hvis en eksisterende booking er i konflikt med den ønskede periode på samme rum.
    /// Kaldet er ansvarligt for at ekskludere bookingen selv ved redigering (BR-19).
    /// </summary>
    public static bool Conflicts(Booking existing, int roomId, DateRange desiredPeriod) =>
        existing.RoomId == roomId
        && BlocksRoom(existing.Status)
        && Overlaps(existing.Period, desiredPeriod);
}
```

`Conflicts` er den ene funktion Application-servicen skal kalde pr. kandidat-booking. Servicen henter bookingerne, Domain afgør om de er i vejen. Dermed findes overlapsdefinitionen præcis ét sted i hele kodebasen — mod to indbyrdes uenige steder i det gamle system.

```csharp
namespace NFHotel.Domain.Guests;

/// <summary>
/// Gæstevalidering. <see cref="Guest.Create"/> fejler hurtigt på første brud;
/// <see cref="Validate"/> samler ALLE fejl, så en formular kan vise dem på én gang (BR-60, BR-96).
/// Begge veje bruger de samme private prædikater — reglen findes ét sted.
/// </summary>
public static class GuestRules
{
    /// <summary>Samler alle valideringsfejl. Tom liste betyder gyldig.</summary>
    /// <returns>Fejlbeskeder i feltrækkefølge: fornavn, efternavn, e-mail, telefon, land.</returns>
    public static IReadOnlyList<string> Validate(
        string? firstName,
        string? lastName,
        string? email,
        string? phoneNumber,
        string? country);

    /// <summary>
    /// Sand hvis e-mailen indeholder både '@' og '.' (BR-93, BR-55).
    /// Bevidst ikke regex — det ville bryde paritet med det gamle system.
    /// </summary>
    public static bool IsValidEmail(string? email);
}
```

Fejlbeskederne i `Validate` er de engelske strenge fra BR-52..BR-56 ("First name is required." osv.), uændret. **Åbent punkt:** brugervendte tekster i Domain er strengt taget en lokaliseringsbeslutning der hører i UI. Jeg beholder dem for at bevare paritet og fordi det gamle system havde dem i `Guest.Validate()`. Se §8.

`RoomRules` bliver tilsvarende lille: `IsValidRoomNumber`, `IsValidFloor`, `IsValidCapacity`, `Validate(...)`.

---

## 5. Tilstandsmaskiner

### 5.1 `BookingStatus`

```
                    ┌──────────── Cancel() ────────────┐
                    │                                  ▼
[Create] ──▶ Pending ──Confirm()──▶ Confirmed ──Cancel()──▶ Cancelled ●
              │  ▲                    │  ▲
              │  └─ Reschedule()      │  └─ Reschedule()
              │                       │
              └──CheckIn()──▶ CheckedIn ◀──CheckIn()──┘      (B-01: Confirmed må springes over)
                                 │
                            CheckOut()
                                 ▼
                            CheckedOut ●
```

```csharp
    /// <summary>
    /// Bekræfter bookingen — betaling er garanteret (BR-02, BR-03, BR-05).
    /// Bemærk: bekræftelse er ikke en forudsætning for check-in (B-01).
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">
    /// Kastes hvis status ikke er præcis <see cref="BookingStatus.Pending"/>.
    /// </exception>
    public void Confirm();

    /// <summary>
    /// Tjekker gæsten ind (BR-06, BR-07). Tilladt fra både Pending og Confirmed (B-01, T-21).
    /// </summary>
    /// <param name="occurredAt">Det faktiske indtjekningstidspunkt.</param>
    /// <param name="today">
    /// Dagens dato i hotellets tidszone. Leveres separat, fordi omregningen fra et
    /// tidsstempel til en lokal kalenderdato er en tidszonepolitik der hører i Application-laget.
    /// </param>
    /// <exception cref="InvalidStateTransitionException">
    /// Kastes hvis status hverken er Pending eller Confirmed, hvis <see cref="CheckInTime"/>
    /// allerede er sat, eller hvis <see cref="StartDate"/> ligger efter <paramref name="today"/>.
    /// </exception>
    public void CheckIn(DateTimeOffset occurredAt, DateOnly today);

    /// <summary>
    /// Tjekker gæsten ud (BR-09, BR-10). Frigiver IKKE rummet i perioden (B-02).
    /// </summary>
    /// <param name="occurredAt">Det faktiske udtjekningstidspunkt. Skal være strengt efter <see cref="CheckInTime"/> (BR-107).</param>
    /// <exception cref="InvalidStateTransitionException">
    /// Kastes hvis status ikke er <see cref="BookingStatus.CheckedIn"/>, hvis <see cref="CheckOutTime"/>
    /// allerede er sat, eller hvis <paramref name="occurredAt"/> ikke er efter <see cref="CheckInTime"/>.
    /// </exception>
    public void CheckOut(DateTimeOffset occurredAt);

    /// <summary>
    /// Annullerer bookingen (BR-11, BR-13). Soft-delete — rækken slettes aldrig.
    /// Eneste overgang der frigiver rummet i perioden (B-02).
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">
    /// Kastes hvis status hverken er Pending eller Confirmed. En CheckedIn/CheckedOut booking kan ikke annulleres.
    /// </exception>
    public void Cancel();

    /// <summary>
    /// Flytter bookingen til en ny periode og/eller et nyt rum (BR-14, BR-17, BR-18, BR-20).
    /// Kontrollerer IKKE overlap mod andre bookinger — det kræver kendskab til andre
    /// bookinger og hører derfor i Application-laget (BR-19).
    /// </summary>
    /// <param name="newPeriod">Den nye periode. Skal starte på eller efter <paramref name="today"/> (BR-108).</param>
    /// <param name="newRoomId">Det nye rums id. Skal være større end 0.</param>
    /// <param name="today">Dagens dato i hotellets tidszone.</param>
    /// <exception cref="InvalidStateTransitionException">Kastes hvis status hverken er Pending eller Confirmed.</exception>
    /// <exception cref="DomainException">Kastes hvis den nye periode starter i fortiden.</exception>
    public void Reschedule(DateRange newPeriod, int newRoomId, DateOnly today);
```

**Guard-tabel**

| Metode | Statuskrav | Øvrige guards | BR |
|---|---|---|---|
| `Confirm()` | `Pending` | — | BR-02, BR-03, BR-05, BR-121 |
| `CheckIn(...)` | `Pending` ∨ `Confirmed` | `CheckInTime is null`; `StartDate <= today` | BR-06, BR-07, BR-08 |
| `CheckOut(...)` | `CheckedIn` | `CheckInTime is not null`; `CheckOutTime is null`; `occurredAt > CheckInTime` | BR-09, BR-10, BR-107 |
| `Cancel()` | `Pending` ∨ `Confirmed` | — | BR-11, BR-13, BR-122 |
| `Reschedule(...)` | `Pending` ∨ `Confirmed` | `newPeriod.Start >= today`; `newRoomId > 0` | BR-14, BR-17, BR-18, BR-20, BR-108, BR-123 |

Ingen metode kan nås fra `CheckedOut` eller `Cancelled` — begge er slutstatusser. Det håndhæves af statuskravene, ikke af en separat tabel.

**Bemærk om BR-03/BR-08/BR-17:** i det gamle system gentjekkes reglen både i `CanExecute` og i selve handlingen. I den nye model *er* handlingen tjekket — `Confirm()` kaster hvis den ikke må. `Can*`-propertyen findes så UI kan disable knappen på forhånd, men den er ikke sikkerheden. Dermed forsvinder dobbeltimplementeringen uden at reglen svækkes.

### 5.2 `HousekeepingStatus`

```
        MarkDailyCleaningDue()
   Clean ──────────────────▶ DailyCleaningDue ──┐
     ▲                                          │ StartCleaning()
     │  MarkDepartureCleaningDue()              ▼
     ├────────────────────▶ DepartureCleaningDue ──▶ CleaningInProgress
     │                                                     │
     │  CompleteCleaning() ◀───────────────────────────────┘
     │
     │  CompleteService()
     └──────────── ServiceInProgress ◀──StartService()── ServiceRequired
                                                              ▲
                                    ReportServiceNeeded() ────┘  (fra alle andre tilstande)
```

```csharp
    /// <summary>Markerer at det beboede rum afventer daglig rengøring (D-10).</summary>
    /// <exception cref="InvalidStateTransitionException">Kastes hvis rummet er under rengøring eller service.</exception>
    public void MarkDailyCleaningDue();

    /// <summary>
    /// Markerer at rummet afventer slutrengøring efter afrejse (D-10).
    /// Kaldes typisk af Application-laget umiddelbart efter <see cref="Booking.CheckOut"/>.
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">Kastes hvis rummet er under rengøring eller service.</exception>
    public void MarkDepartureCleaningDue();

    /// <summary>Rengøringspersonalet kvitterer og går i gang.</summary>
    /// <exception cref="InvalidStateTransitionException">
    /// Kastes medmindre status er DailyCleaningDue eller DepartureCleaningDue.
    /// </exception>
    public void StartCleaning();

    /// <summary>Rengøringen er færdig; rummet er klar.</summary>
    /// <exception cref="InvalidStateTransitionException">Kastes medmindre status er CleaningInProgress.</exception>
    public void CompleteCleaning();

    /// <summary>
    /// Rapporterer at rummet skal ses af en servicetekniker.
    /// Spærrer IKKE rummet for booking — det kræver <see cref="SendToMaintenance"/> (B-04).
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">Kastes hvis service allerede er i gang.</exception>
    public void ReportServiceNeeded();

    /// <summary>Serviceteknikeren kvitterer og går i gang.</summary>
    /// <exception cref="InvalidStateTransitionException">Kastes medmindre status er ServiceRequired.</exception>
    public void StartService();

    /// <summary>
    /// Servicen er afsluttet.
    /// </summary>
    /// <param name="requiresCleaning">
    /// Sand hvis arbejdet har efterladt rummet snavset; sætter da status til DepartureCleaningDue
    /// i stedet for Clean.
    /// </param>
    /// <exception cref="InvalidStateTransitionException">Kastes medmindre status er ServiceInProgress.</exception>
    public void CompleteService(bool requiresCleaning);
```

**Guard-tabel**

| Metode | Tilladt fra | Til | Rolle (D-03) |
|---|---|---|---|
| `MarkDailyCleaningDue()` | `Clean`, `DailyCleaningDue`, `ServiceRequired` | `DailyCleaningDue` | System/manager |
| `MarkDepartureCleaningDue()` | `Clean`, `DailyCleaningDue`, `DepartureCleaningDue`, `ServiceRequired` | `DepartureCleaningDue` | System/manager |
| `StartCleaning()` | `DailyCleaningDue`, `DepartureCleaningDue` | `CleaningInProgress` | Rengøringspersonale |
| `CompleteCleaning()` | `CleaningInProgress` | `Clean` | Rengøringspersonale |
| `ReportServiceNeeded()` | alt undtagen `ServiceInProgress` | `ServiceRequired` | Rengøring, tekniker, manager |
| `StartService()` | `ServiceRequired` | `ServiceInProgress` | Servicetekniker |
| `CompleteService(bool)` | `ServiceInProgress` | `Clean` \| `DepartureCleaningDue` | Servicetekniker |

`MarkDailyCleaningDue`/`MarkDepartureCleaningDue` er idempotente fra deres egen tilstand — mobilappen har ingen offline-kø (D-11), men netværksretries kan stadig give dublerede kald.

### 5.3 `RoomStatus`

```csharp
    /// <summary>Tager rummet ud af drift på ubestemt tid. Rummet kan ikke bookes.</summary>
    /// <exception cref="InvalidStateTransitionException">Kastes hvis rummet allerede er OutOfService.</exception>
    public void TakeOutOfService();

    /// <summary>Spærrer rummet midlertidigt på grund af en fejl der gør det ubeboeligt.</summary>
    /// <exception cref="InvalidStateTransitionException">Kastes hvis rummet allerede er Maintenance.</exception>
    public void SendToMaintenance();

    /// <summary>
    /// Gør rummet bookbart igen. Rører ikke rengøringsstanden — et rum kan være
    /// Available og samtidig afvente rengøring (B-04).
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">Kastes hvis rummet allerede er Available.</exception>
    public void ReturnToService();
```

Alle tre overgange er frit tilladte mellem hinanden (kun "ingen ændring" afvises). Der findes ingen forretningsregel der forbyder fx `OutOfService → Maintenance`.

**Den centrale invariant for hele B-04, og den test der beviser den:**
`Room.IsBookable` læser kun `Status`. En testcase skal hedde noget i retning af `IsBookable_ShouldBeTrue_WhenStatusIsAvailableAndHousekeepingIsDepartureCleaningDue()`. Hvis den test nogensinde fejler, er splittet ophævet ved et uheld.

---

## 6. Regelplacering — alle 126 BR-id'er

### 6.1 Regler der hører i Domain

| BR-id | Hvor i Domain | Metode/medlem |
|---|---|---|
| BR-01 | `Booking` | `Booking.Create` (sætter `Pending`) |
| BR-02 | `Booking` | `CanConfirm` |
| BR-03 | `Booking` | `Confirm()` guard |
| BR-05 (statusdelen) | `Booking` | `Confirm()` |
| BR-06 | `Booking` | `CanCheckIn(DateOnly)` |
| BR-07 | `Booking` | `CheckIn(DateTimeOffset, DateOnly)` |
| BR-08 (guarddelen) | `Booking` | `CheckIn(...)` guard |
| BR-09 | `Booking` | `CanCheckOut` |
| BR-10 (statusdelen) | `Booking` | `CheckOut(DateTimeOffset)` |
| BR-11 | `Booking` | `CanCancel` |
| BR-13 | `Booking` | `Cancel()` |
| BR-14 | `Booking` | `CanReschedule` |
| BR-17 | `Booking` | `Reschedule(...)` guard |
| BR-18 | `Booking` / `DateRange` | `Reschedule(...)` + `DateRange` ctor |
| BR-20 (mutationsdelen) | `Booking` | `Reschedule(...)` |
| BR-22 (prædikaterne) | `Booking` | `CanConfirm`, `CanCheckIn`, `CanCheckOut`, `CanCancel`, `CanReschedule` |
| BR-38 | `DateRange` | ctor-invariant (`End > Start`) |
| BR-39 (prædikatet) | `BookingRules` | `Overlaps`, `BlocksRoom`, `Conflicts` |
| BR-43 | `DateRange` | ctor-invariant |
| BR-44 | `Booking` | `Create(...)` guard (`period.StartsOnOrAfter(today)`) |
| BR-45 | `Booking` | `Create(...)` guard (`roomId > 0`) |
| BR-46 (reglerne) | `GuestRules` | `Validate(...)` |
| BR-48 | `Booking` | `Create(...)` |
| BR-49 (beregningerne) | `Booking` | `NumberOfNights`, `BookingNumber` |
| BR-52 | `GuestRules` | `Validate(...)` — fornavn |
| BR-53 | `GuestRules` | `Validate(...)` — efternavn |
| BR-54 | `GuestRules` | `Validate(...)` — telefon |
| BR-55 | `GuestRules` | `IsValidEmail` |
| BR-56 | `GuestRules` | `Validate(...)` — land |
| BR-57 | `Guest` | `PassportNumber` som `string?`, ingen guard |
| BR-60 (aggregeringen) | `GuestRules` | `Validate(...)` returnerer alle fejl |
| BR-72 | `Guest` | `UpdateDetails(...)` — `GuestId` har ingen setter |
| BR-81 | `Room` | `Room.Create(...)` sætter `Available` + `Clean` |
| BR-84 | `Room` | Ophævet strukturelt: `Create`/`UpdateDetails` kaster, resultatet kan ikke ignoreres |
| BR-88 | `DateRange` | ctor-invariant (erstatter `DateGreaterThanAttribute`) |
| BR-91 | `GuestRules` / `Guest` | `Validate(...)` + `Create`/`UpdateDetails` guard |
| BR-92 | `GuestRules` / `Guest` | do. |
| BR-93 | `GuestRules` / `Guest` | `IsValidEmail` |
| BR-94 | `GuestRules` / `Guest` | do. |
| BR-95 | `GuestRules` / `Guest` | do. |
| BR-96 | `GuestRules` | `Validate(...)` samler alle fejl |
| BR-97 | `RoomRules` / `Room` | `IsValidRoomNumber` + `Create`/`UpdateDetails` |
| BR-98 | `RoomRules` / `Room` | `IsValidFloor` (`floor > 0`) |
| BR-99 | `Room` | `RoomSize`-enum + `Enum.IsDefined`-guard |
| BR-100 | `RoomRules` / `Room` | `IsValidCapacity` (`capacity > 0`) |
| BR-101 | `DateRange` | ctor afviser `default(DateOnly)` |
| BR-102 | `DateRange` | do. |
| BR-103 | `DateRange` | ctor-invariant (`End > Start`) |
| BR-104 | `Booking` | `Create(...)` guard |
| BR-105 | `Booking` | `Create(...)` guard (`roomId > 0`) |
| BR-106 | `Booking` | `Create(...)` guard (`guestId > 0`) |
| BR-107 | `Booking` | `CheckOut(...)` guard |
| BR-108 | `Booking` | `Reschedule(...)` guard |
| BR-109 | `Booking` / `DateRange` | `NumberOfNights` (`DateRange.Nights`), `BookingNumber` |
| BR-110 (prædikatet) | `Room` | `IsBookable` |
| BR-113 | `Room` | Flyttet fra repo til `Create`/`UpdateDetails` — repoet skal ikke længere validere |
| BR-117 | `DateRange` | `Nights` (samme beregning som BR-109; dubletten konsolideret) |
| BR-121 | `Booking` | `CanConfirm` (dublet af BR-02, konsolideret) |
| BR-122 | `Booking` | `CanCancel` (dublet af BR-11, konsolideret) |
| BR-123 | `Booking` | `CanReschedule` (dublet af BR-14, konsolideret) |
| BR-126 | `RoomStatus` + `HousekeepingStatus` | Løst ved splittet (B-04); ét sæt tilladte værdier, UI binder til enum |

**64 regler i Domain.**

### 6.2 Regler der IKKE hører i Domain

| BR-id | Hører i | Hvorfor |
|---|---|---|
| BR-04 | UI | Bekræftelsesdialog. Ren brugerinteraktion. |
| BR-05 (persist/reload) | Application | Repository-orkestrering. |
| BR-08 (ingen dialog) | UI | Fravær af dialog er en UI-beslutning. |
| BR-10 (persist) | Application | do. |
| BR-12 | UI | Dialogtekst og Ja/Nej. |
| BR-15 | UI | Forudfyldning af redigeringsformular. |
| BR-16 | UI | `IsEditMode` er præsentationstilstand. |
| BR-19 | Application (kalder `BookingRules.Conflicts`) | Kræver kendskab til *andre* bookinger. Domain leverer prædikatet, servicen leverer datasættet. **B-02: `CheckedOut`-undtagelsen fjernes.** |
| BR-20 (persist/reload) | Application | do. |
| BR-21 | UI | Forlader redigeringstilstand. |
| BR-22 (genberegning) | UI | I Blazor er der ingen `CanExecute` at raise — properties læses ved render. |
| BR-23 | Application | Query-filter over mange bookinger. |
| BR-24 | Application | Periodefiltrering af en liste. |
| BR-25 | Application | Søgning over mange bookinger. Domain leverer `Guest.FullName` og `Room.RoomNumber`. |
| BR-26 | UI | Automatisk skift af visningsperiode. |
| BR-27 | UI | Kalenderberegning. |
| BR-28 | UI | Navigation frem/tilbage. |
| BR-29 | UI | Kolonneantal. |
| BR-30 | UI | Overskriftsformatering, kulturafhængig. Conventions.md forbyder UI-formatering i Domain. |
| BR-31 | Ingen steder (endnu) | Omsætning beregnes aldrig; der findes ingen pris i domænet. Genopstår med `RoomType`+pris (udskudt). |
| BR-32 | UI | Sortering af tabelvisning. |
| BR-33 | UI | Genindlæsning efter lukket dialog. |
| BR-34 | Application | Tilgængelighedsforespørgsel. Domain leverer `Room.IsBookable` + `BookingRules.Conflicts`. |
| BR-35 | UI | Forudfyldning af formular. |
| BR-36 | UI | Reaktiv genberegning ved feltændring. |
| BR-37 | UI | Tom-tilstand for rumlisten. |
| BR-40 | UI | Fejlbesked ved nul ledige rum. |
| BR-41 | Application/UI | "Dato er udfyldt" er formularvalidering — med `DateOnly` findes tomheden kun i input-modellen. Domain-modparten er BR-101. |
| BR-42 | Application/UI | do. (Domain-modpart: BR-102) |
| BR-47 | Application | Beslutter om gæsten er ny; kræver repository. |
| BR-49 (kvitteringstekst) | UI | Formatering af beskeden. |
| BR-50 | UI | 2000 ms auto-luk. |
| BR-51 | Application/UI | Exception-til-besked-oversættelse. |
| BR-58 | UI | Knap-aktivering. |
| BR-59 | UI | Revalidering ved hver ændring. |
| BR-60 (visningen) | UI | Domain leverer listen, UI viser den. |
| BR-61 | UI | Bekræftelsesdialog. |
| BR-62 | Application | Vælger create vs. update; kræver repository. |
| BR-63 | UI | Titel- og knaptekster. |
| BR-64 | UI | Annuller uden gem. |
| BR-65 | UI | Knap-guard på valgt gæst. |
| BR-66 | UI | Redigering på kopi. I Blazor bliver det en edit-model — ikke domænets ansvar. |
| BR-67 | UI | Fejlbesked ved manglende valg. |
| BR-68 | Application | Søgning over mange gæster (AND-logik på ord). |
| BR-69 | Application | Sortering af liste. |
| BR-70 | Application | Tom søgning viser alle. |
| BR-71 | UI | "Ryd gæst" nulstiller formularen. |
| BR-73 | UI | Dobbelt-guard på event. |
| BR-74 | Application | Sammensat rumfilter over mange rum. |
| BR-75 | Application | Udleder distinkte filterværdier fra datasættet. |
| BR-76 | UI | Nulstilling af filter. |
| BR-77 | UI | Advarselsdialog ved sletning. |
| BR-78 | Application | Exception-håndtering fra repository. |
| BR-79 | UI | Null-parameter-guard i kommando. |
| BR-80 | UI | Genindlæsning + lukning af sidepanel. |
| BR-82 | Application | Create vs. update ud fra id. |
| BR-83 | UI | Titel- og knaptekster. |
| BR-85 | UI | Startskærm. |
| BR-86 | UI | Navigation og komponentlevetid. |
| BR-87 | Bortfalder | `RelayCommand` findes ikke i Blazor. |
| BR-89 | Bortfalder | `DateGreaterThanAttribute`s interne fejlhåndtering. Attributten slettes; `DateRange` erstatter den. |
| BR-90 | Bortfalder | "Ikke-DateTime = gyldig". Med `DateOnly` non-nullable kan tilfældet ikke opstå. |
| BR-110 (filtret) | Infrastructure/Application | Query'en hører i dataadgangslaget; prædikatet i Domain. |
| BR-111 | Infrastructure | In-memory-filter efter fuldt tabeltræk — skal i øvrigt rettes til et rigtigt `WHERE`. |
| BR-112 | Application/Infrastructure | FK-guard ved sletning af rum. Skal væk fra `catch (SqlException 547)` og op i en eksplicit forespørgsel. |
| BR-114 | Application/Infrastructure | Eksistenstjek før update/delete. |
| BR-115 | Infrastructure | `GetById` returnerer null. Repository-kontrakt. |
| BR-116 | Infrastructure | SQL-navnesøgning. |
| BR-118 | UI | Halvdags-tegning i kalenderen. Ren visuel konvention. |
| BR-119 | UI | Klipning ved periodegrænse. |
| BR-120 | UI | Filtrering af tidslinje pr. rum. |
| BR-124 | UI | `IsReadOnly` på felter. |
| BR-125 | UI | Knap-guard. |

**62 regler uden for Domain. 64 + 62 = 126.** ✓

---

## 7. Ændringer ift. det gamle domæne

| Hvad | Før | Nu | Hvorfor |
|---|---|---|---|
| `Booking.StartDate`/`EndDate` | `DateTime` | `DateOnly` | B-05. Fjerner U-05 og alle `.Date`-sammenligninger. |
| `Booking.CheckInTime`/`CheckOutTime` | `DateTime?` | `DateTimeOffset?` | B-05. Rigtige tidsstempler med offset. |
| Tidskilde | `DateTime.Now` inde i ViewModel/model | `DateOnly today` / `DateTimeOffset occurredAt` som parametre | Domain må ikke kende omverdenen; gør tilstandsmaskinen deterministisk testbar. |
| Overlapsregel | To uenige varianter (BR-19 ekskluderer `CheckedOut`, BR-39 gør ikke) | Én ren funktion `BookingRules.Overlaps`/`Conflicts` | B-02, B-03. Modstriden løses til fordel for den strengeste. |
| `CheckedOut` frigiver rum | Ja ved redigering, nej ved oprettelse | Nej, aldrig | B-02. Tidlig udtjekning håndteres ved at forkorte `EndDate`. |
| `RoomStatus` | Available/OutOfService/Maintenance, blandede to spørgsmål | Kun bookbarhed, samme tre ordinaler | B-04, F-01. Nul datamigrering, kohærent betydning. |
| Rengøringstilstand | Fandtes ikke i enum; kun i XAML-dropdown (`Cleaning`) | Ny `HousekeepingStatus` med 6 værdier | B-04, D-10. Mobilappen har brug for forløbet. |
| Tilladte rumstatusser | Hardkodet stringliste i XAML, uenig med enum (BR-126) | Kun enums | BR-126 løst. UI binder til enum. |
| `Room.RoomSize` | `string` fri tekst | `RoomSize`-enum | Fase1-Scope "billig insurance". Gør `RoomType`+pris billig senere. |
| `Room.RoomNumber` | `string` i C#, `INT` i SQL | `string` | U-01 løst til fordel for C#-typen; understøtter "12B". |
| PK-navngivning | `Booking.BookingID`, `Booking.RoomID`, `Room.RoomId` | `BookingId`, `RoomId`, `GuestId` overalt | U-19. Conventions.md: ét PK-mønster. |
| "Rum ELLER FK" | Fejl kun hvis både navigation null OG id 0 (BR-105/106) | FK altid > 0; navigation er `Room?`/`Guest?` | Den gamle regel var et symptom på manglende factory; navigationen er ikke en gyldighedskilde. |
| Validering | `Validate()` returnerer `List<string>`, kaldes frivilligt, ignoreres i BR-84 | Invarianter i `Create`/`UpdateDetails` der kaster + `*Rules.Validate` til formularer | BR-84 og BR-113 bliver strukturelt umulige. Repoet skal ikke længere validere. |
| Statusændring | Offentlig setter, sat fra ViewModel | `Confirm()`, `CheckIn()`, `CheckOut()`, `Cancel()`, `Reschedule()` med guards | Fase1-Scope pkt. 1. En konsol-app kan kalde metoderne og få reglerne gratis. |
| `Can*`-regler | Dubleret i ViewModel og XAML-trigger (BR-02/121, 11/122, 14/123) | Én property pr. regel på `Booking` | Konsolidering som BusinessRules.md efterlyser. |
| `DateGreaterThanAttribute` | Reflection-baseret attribut, brugt ingen steder | Slettet; `DateRange` erstatter | Conventions.md: slet død kode. BR-89/BR-90 bortfalder. |
| `NumberOfNights` | Beregnet i model + reimplementeret i converter | `DateRange.Nights`, ét sted | BR-109/BR-117 konsolideret. |
| `Guest.PassportNumber` | `string`, altid udfyldt i data | `string?`, følsomt, aldrig søgenøgle | B-07, D-04, U-22. |
| Nullability | Ingen NRT-disciplin | NRT slået til, navigationer nullable | Conventions.md. |
| Længdekontrol | Ingen (Analysis §1) | `MaxLength`-konstanter + guards (**ny regel N-01**) | Fejl fanges i Domain i stedet for som DB-exception. Flagget som ny regel. |

---

## 8. Uklart, modstridende eller bevidst efterladt til nogen andre

**D-01 — Fejlbeskeder på engelsk i Domain.**
`GuestRules.Validate` returnerer de gamle engelske strenge ("First name is required."). Det bevarer paritet, men brugervendt tekst i Domain er en lokaliseringsbeslutning der egentlig hører i UI. Alternativet er at returnere fejl*koder* og lade UI oversætte — det er renere, men koster ledger-paritet på BR-52..BR-56 og BR-60. **Skal besluttes før Fase 2.**

**D-02 — Hvem markerer et rum til slutrengøring efter check-ud?**
`Booking.CheckOut()` kan ikke kalde `Room.MarkDepartureCleaningDue()` — det er to aggregater, og `Booking.Room` er ofte null. Jeg har lagt koordineringen i Application-laget. Alternativet er en domain event (`BookingCheckedOut`), men det er infrastruktur (dispatcher, handlers) som ikke er besluttet, og som Fase1-Scope ikke beder om. **Anbefaling: Application-service nu, domain events når/hvis der bliver behov for flere lyttere.**

**D-03 — `Floor > 0` (BR-98) forbyder stueetage og kælder.**
Reglen er bevaret uændret, fordi ingen U-uoverensstemmelse eller præmis dikterer andet. Men et hotel med reception i stueplan kan have værelse 001. **Er det en bevidst regel eller en overset off-by-one?** Bør bekræftes med forretningen.

**D-04 — `Confirmed → Cancelled` efter betaling.**
BR-11 tillader annullering fra `Confirmed`. Med B-01 betyder `Confirmed` "betaling garanteret", så en annullering herfra har en økonomisk konsekvens (refusion). Domænet har ingen pris og kan derfor ikke modellere det. **Overgangen er bevaret som i dag; refusionsregler tilføjes når `Payment` bygges.**

**D-05 — Tilbageførsel af en fejlagtig check-in/check-ud findes ikke.**
Der er ingen `UndoCheckIn()` i det gamle system og ingen BR der beder om det. Personalet kan i praksis komme til at tjekke forkert booking ind. **Jeg har ikke opfundet reglen** — men det er sandsynligvis et reelt behov, og `CheckedOut` er i dag en absolut slutstatus.

**D-06 — Tidlig udtjekning ved at forkorte `EndDate` (B-02) har ingen metode endnu.**
`Reschedule(...)` kræver `Pending` ∨ `Confirmed` og kan derfor ikke bruges på en `CheckedIn` booking. B-02 siger tidlig udtjekning "håndteres senere". Hvis den skal håndteres i Fase 2, mangler der en `ShortenStay(DateOnly newEndDate)` med statuskrav `CheckedIn`. **Ikke designet, da præmissen udskyder det.**

**D-07 — `HousekeepingStatus` har intet inspektions-/godkendelsestrin.**
D-03 giver "Manager/ejer" adgang til appen, hvilket kunne antyde en godkendelse efter slutrengøring. D-10 nævner det ikke. **Udeladt bevidst.** Tilføjes som `Inspected = 6` når mobil-guilden beder om det — nye værdier på enden er gratis.

**D-08 — Hvem sætter `DailyCleaningDue`, og hvornår?**
Et beboet rum skal formentlig markeres hver morgen. Det kræver enten et scheduled job eller en afledning fra bookinger. **Domain leverer metoden; trigger-mekanismen er ikke min beslutning.**

**D-09 — Persondata-sletning (D-05).**
"Persondata opbevares i X år, eller indtil gæsten selv beder om sletning." Det kunne pege på en `Guest.Anonymize()` i Domain (overskriv navn/mail/telefon/pas, behold `GuestId` så bookinghistorik overlever). **Jeg har ikke designet den**, fordi X'erne ikke er fastsat og fordi retention-mekanismen ligger uden for Fase 1's leverance. Men hvis `Anonymize()` skal findes, hører den i Domain — og den kolliderer med invarianten "FirstName må ikke være tom" (BR-91). **Det skal afklares før retention bygges.**

**D-10 — `RoomSize`-migrering.**
Enum-omlægningen kræver at eksisterende `NVARCHAR(20)`-værdier mappes til int. Testdata bruger `'Single'/'Double'/'Suite'`, men der er ingen CHECK-constraint, så **ukendte værdier kan findes i produktionsdata.** Videregives til infrastruktur-agenten som et krav om en `default`-strategi (fejl vs. `Single`).

**D-11 — `RoomStatus.OutOfService` vs. `Maintenance`.**
Det gamle system dokumenterer aldrig forskellen. Jeg har tildelt dem betydningen "administrativt/langvarigt" vs. "teknisk fejl/midlertidigt". **Det er min fortolkning, ikke en fundet regel** — begge blokerer bookbarhed identisk, så en fejlfortolkning har ingen funktionel konsekvens i Fase 1, men den bør bekræftes før mobilappen viser dem forskelligt.

---

## 9. Grænseflade til de øvrige agenter

Videregives uden at jeg designer det:

- **Infrastruktur:** entiteterne har private settere og privat parameterløs ctor — EF skal bruge backing fields/`UsePropertyAccessMode`. `DateRange` persisteres **ikke**; `StartDate`/`EndDate` er to almindelige `DateOnly`-kolonner. `PassportNumber` skal krypteres og må ikke indekseres (B-07, D-04). `RoomSize`-datamigrering fra tekst til int. Enum-ordinaler for `BookingStatus` og `RoomStatus` er låst.
- **Application:** BR-19, BR-34 og BR-110 skal kalde `BookingRules.Conflicts` / `Room.IsBookable` — ikke reimplementere overlap. `today` og `occurredAt` skal leveres til `Booking`-metoderne fra én tidszonepolitik ét sted.
- **Test:** de 22 tests i `BookingOverviewViewModelTests` (T-20..T-33) kan porteres næsten 1:1 mod `Booking`-metoderne uden mocks, fordi tilstandsmaskinen nu er ren. T-21 (`Pending → CheckedIn`) skal bestå uændret (B-01).

---

### Critical Files for Implementation

- `/mnt/user-data/uploads/Development/New folder/NFHotel/docs/BusinessRules.md`
- `/mnt/user-data/uploads/Development/New folder/NFHotel/docs/Analysis.md`
- `/mnt/user-data/uploads/Development/New folder/NFHotel/docs/Decisions.md`
- `/mnt/user-data/uploads/Development/New folder/NFHotel/docs/Fase1-Scope.md`
- `/mnt/user-data/uploads/Development/Conventions.md`