# Architecture-Layers.md — bilag

> Fuldt output fra agenten `design:layers` (Fase 1). **Bilag.** `Architecture.md` har forrang hvor de modsiger hinanden — se især afgørelserne A-01 til A-15.

# Designdokument — `Application` og `Infrastructure` (Fase 1)

Agent: `design:layers`. Kilder læst: `BusinessRules.md` (126 regler), `Analysis.md` afsnit 1–5, `Conventions.md`, `NFHotel-Refactor-Plan.md` afsnit 1–3 + 7, `Fase1-Scope.md`, `Decisions.md`.

Præmisserne B-03, B-05, B-06, B-07, B-08, B-09 er lagt til grund og gentænkes ikke. Entiteter, enums og domæneregler er `design:domain`s område; hvor jeg er afhængig af deres valg, står det som en eksplicit afhængighed i afsnit 10.

---

## 1. Projektstruktur

### 1.1 Solution

```
NFHotel.sln
├── src/
│   ├── NFHotel.Domain/          (design:domain)
│   ├── NFHotel.Application/     ← dette dokument
│   ├── NFHotel.Infrastructure/  ← dette dokument
│   ├── NFHotel.Web/             (design:web — kun DI-kontrakten her)
│   └── NFHotel.Api/             TEGNES, BYGGES IKKE (D-09)
└── tests/
    ├── NFHotel.Domain.Tests/
    ├── NFHotel.Application.Tests/
    └── NFHotel.Infrastructure.Tests/
```

Referencer, én vej: `Web → Application → Domain` og `Web → Infrastructure → Application → Domain`. **`Application` må ikke referere EF Core, Npgsql eller ASP.NET.** Det håndhæves af en arkitekturtest, ikke af disciplin (afsnit 9).

### 1.2 Mappeprincip

`Conventions.md` siger: *"Organisér efter feature først, ikke kun efter type"* og *"Hold namespaces identiske med mappestruktur"*. Målarkitekturen i refaktoreringsplanen (afsnit 3) tegner derimod `Interfaces/`, `Services/`, `DTOs/` — altså type-først. Conventions er source of truth, så **feature-først vinder**; se afsnit 10, punkt 1 for konflikten skrevet ud.

Konsekvens: én feature-mappe rummer sit service-interface, sin service, sit repository-interface, sine DTO'er og sin mapping. En udvikler der skal ændre "opret booking" åbner én mappe.

### 1.3 `NFHotel.Application`

```
src/NFHotel.Application/
├── NFHotel.Application.csproj
│     ProjectReference: NFHotel.Domain
│     PackageReference: Microsoft.Extensions.DependencyInjection.Abstractions
│                       Microsoft.Extensions.Options
│     (INGEN EF Core, INGEN Npgsql)
├── DependencyInjection.cs                      → AddApplication()
├── Common/
│   ├── Results/
│   │   ├── Error.cs
│   │   ├── ErrorCodes.cs
│   │   ├── Result.cs
│   │   └── ResultOfT.cs                        → Result<TValue>
│   ├── Exceptions/
│   │   ├── BookingOverlapConflictException.cs  → oversat fra DB-værnet, B-03
│   │   ├── ConcurrencyConflictException.cs
│   │   └── UniqueConstraintViolationException.cs
│   ├── Sorting/
│   │   └── SortDirection.cs
│   └── Time/
│       ├── IClock.cs
│       └── HotelTimeOptions.cs
├── Abstractions/
│   └── Persistence/
│       └── IUnitOfWork.cs
├── Bookings/
│   ├── IBookingRepository.cs
│   ├── IBookingService.cs
│   ├── BookingService.cs
│   ├── BookingSortField.cs
│   ├── Dtos/
│   │   ├── AvailableRoomDto.cs
│   │   ├── BookingActionsDto.cs
│   │   ├── BookingDetailsDto.cs
│   │   └── BookingListItemDto.cs
│   ├── Requests/
│   │   ├── BookingOverviewQuery.cs
│   │   ├── CreateBookingRequest.cs
│   │   └── RescheduleBookingRequest.cs
│   └── Mapping/
│       └── BookingMappings.cs
├── Rooms/
│   ├── IRoomRepository.cs
│   ├── IRoomService.cs
│   ├── RoomService.cs
│   ├── Dtos/
│   │   ├── RoomDetailsDto.cs
│   │   ├── RoomFilterOptionsDto.cs
│   │   └── RoomListItemDto.cs
│   ├── Requests/
│   │   ├── CreateRoomRequest.cs
│   │   ├── RoomFilter.cs
│   │   └── UpdateRoomRequest.cs
│   └── Mapping/
│       └── RoomMappings.cs
└── Guests/
    ├── IGuestRepository.cs
    ├── IGuestService.cs
    ├── GuestService.cs
    ├── Dtos/
    │   ├── GuestDetailsDto.cs
    │   └── GuestListItemDto.cs
    ├── Requests/
    │   ├── CreateGuestRequest.cs
    │   ├── GuestFields.cs
    │   └── UpdateGuestRequest.cs
    └── Mapping/
        └── GuestMappings.cs
```

Namespaces følger mapperne: `NFHotel.Application.Bookings.Dtos` osv. Én public type pr. fil, filnavn = typenavn (Conventions).

`GuestFields` ligger i `Guests/Requests/` og bruges også af `Bookings` (BR-47, gæst oprettes sammen med bookingen). Det er den eneste tilladte krydsreference mellem feature-mapper, og den går altid mod en request/DTO — aldrig mod en anden features service.

### 1.4 `NFHotel.Infrastructure`

```
src/NFHotel.Infrastructure/
├── NFHotel.Infrastructure.csproj
│     ProjectReference: NFHotel.Application
│     PackageReference: Npgsql.EntityFrameworkCore.PostgreSQL
│                       EFCore.NamingConventions
│                       Microsoft.EntityFrameworkCore.Design (PrivateAssets=all)
│                       Microsoft.Extensions.Options.ConfigurationExtensions
├── DependencyInjection.cs                     → AddInfrastructure(IConfiguration)
├── Persistence/
│   ├── HotelDbContext.cs
│   ├── HotelDbContextDesignTimeFactory.cs     → så `dotnet ef` ikke skal starte Web
│   ├── UnitOfWork.cs
│   ├── DbUpdateExceptionTranslator.cs         → SQLSTATE → Application-exception
│   ├── Configurations/
│   │   ├── BookingConfiguration.cs
│   │   ├── GuestConfiguration.cs
│   │   └── RoomConfiguration.cs
│   ├── Converters/
│   │   └── EncryptedStringConverter.cs        → B-09
│   └── Seed/
│       └── DevelopmentSeedData.cs             (kun Development)
├── Repositories/
│   ├── BookingRepository.cs
│   ├── GuestRepository.cs
│   └── RoomRepository.cs
├── Security/
│   ├── IStringEncryptor.cs
│   ├── AesGcmStringEncryptor.cs
│   └── EncryptionOptions.cs
├── Time/
│   └── SystemClock.cs
└── Migrations/                                (genereret)
```

`IStringEncryptor` ligger i **Infrastructure**, ikke Application. Application skal aldrig vide at et felt er krypteret — den ser klartekst, EF gør resten. Det er hele pointen med en `ValueConverter` frem for at kryptere i servicen.

---

## 2. Repository-interfaces

### 2.1 Designregler for laget

Fire regler, alle direkte modsvar til fund i `Analysis.md` afsnit 2:

1. **Repositories beslutter ikke.** De gamle repos indeholdt BR-110, BR-111, BR-112, BR-113, BR-114, BR-116 (`Analysis.md`, "Forretningsregler fundet i repos"). Nyt lag: filtre er *parametre*, ikke hardkodede valg. Når `BookingService` vil vide hvilke bookinger der spærrer et rum, sender den selv status-sættet med — hentet fra `Domain`. Reglen bliver dermed ét sted, ikke i SQL.
2. **Nullability er ærlig.** Det gamle `IBookingRepo.GetById` lovede `Booking`, klassen returnerede `Booking?`. Alle `GetById*Async` returnerer `T?`, og servicen oversætter `null` til `Result.Failure(NotFound)` (BR-115).
3. **Ingen `SaveChanges` i repositories.** Skrivemetoder er synkrone (`Add`/`Update`/`Remove`) fordi EF's `Add` ikke laver IO — `Conventions.md`: *"Metoder uden reelt async arbejde skal ikke være async bare for syns skyld"*. Kun `SaveChangesAsync` på `IUnitOfWork` rører databasen.
4. **`CancellationToken` på alt der rører IO** (B-05), altid sidste parameter med default.

### 2.2 `IBookingRepository`

```csharp
namespace NFHotel.Application.Bookings;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(int bookingId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int bookingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetByPeriodAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyCollection<BookingStatus> includedStatuses,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetByStatusAsync(
        BookingStatus status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetByRoomIdAsync(
        int roomId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetByGuestIdAsync(
        int guestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetCandidatesForOverlapAsync(
        int roomId,
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyCollection<BookingStatus> blockingStatuses,
        int? excludeBookingId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetOccupiedRoomIdsAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyCollection<BookingStatus> blockingStatuses,
        CancellationToken cancellationToken = default);

    void Add(Booking booking);
    void Update(Booking booking);
    void Remove(Booking booking);
}
```

`GetCandidatesForOverlapAsync` returnerer *kandidater* — den grovfiltrerer på rum, statussæt og datovindue i SQL. Selve overlapsafgørelsen træffes af den rene funktion i `Domain` (B-03), så repository og domæne aldrig kan komme til at være uenige om `<` versus `<=`. Det er samme funktion der bruges ved oprettelse og ved opdatering, hvilket lukker modstriden BR-19 mod BR-39.

`GetOccupiedRoomIdsAsync` findes fordi alternativet — at hente alle bookinger i perioden og filtrere i hukommelsen — er præcis BR-111's fejl. Den returnerer id'er, ikke entiteter, fordi kaldet kun bruges som negation i tilgængelighedsopslaget.

**Sammenligning med `IBookingRepo`:**

| Gammel | Ny | Ændring og begrundelse |
|---|---|---|
| `void Create(Booking)` | `void Add(Booking)` | Omdøbt. EF-semantik: metoden persisterer ikke, den markerer. `Create` løj om hvornår rækken fandtes. |
| `List<Booking> GetAll()` | `Task<IReadOnlyList<Booking>> GetAllAsync(ct)` | Async (B-05). `IReadOnlyList` fordi kalderen ikke må mutere resultatet; `List<T>` inviterede til det. |
| `Booking GetById(int)` | `Task<Booking?> GetByIdAsync(int, ct)` | Nullability rettet — interfacet løj (`Analysis.md` afsnit 2). BR-115 bevaret som kontrakt. |
| `List<Booking> GetByStatus(BookingStatus)` | `Task<IReadOnlyList<Booking>> GetByStatusAsync(...)` | Bevaret, men implementeres nu som `WHERE status = @p` i stedet for fuldt tabeltræk plus LINQ. BR-111 dør som regel. |
| `List<Booking> GetByRoomID(int)` | `GetByRoomIdAsync` | Casing rettet (U-19). |
| `List<Booking> GetByGuestID(int)` | `GetByGuestIdAsync` | Samme. |
| `void Update(Booking)` | `void Update(Booking)` | Bevaret, men uden det gamle for-`GetById` og `ArgumentException` — eksistenstjekket (BR-114) flytter til servicen, hvor det kan blive et `Result`, og det læs-så-skriv over to connections forsvinder. |
| `void Delete(int)` | `void Remove(Booking)` | Tager entiteten, ikke id'et, fordi servicen alligevel har hentet den for at kunne afvise "findes ikke" pænt. Sletning bruges i praksis ikke på bookinger — BR-13 er soft-delete via status. |
| — | `ExistsAsync` | **Tilføjet.** BR-114 uden at hente hele grafen. |
| — | `GetByPeriodAsync` | **Tilføjet.** BR-24 og oversigtens periodefiltrering rykker i SQL. Retter samtidig fejlen fra `Analysis.md` afsnit 3 punkt 2, hvor uge- og månedsvisning slet ikke filtrerede. |
| — | `GetCandidatesForOverlapAsync` | **Tilføjet.** B-03. Det gamle lag havde *intet* overlapstjek. |
| — | `GetOccupiedRoomIdsAsync` | **Tilføjet.** Bærer BR-39/BR-34 uden at flytte reglen ned i SQL. |

### 2.3 `IRoomRepository`

```csharp
namespace NFHotel.Application.Rooms;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(int roomId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int roomId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Room>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Room>> GetByStatusAsync(
        RoomStatus status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Room>> GetByFilterAsync(
        RoomFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Room>> GetByIdsAsync(
        IReadOnlyCollection<int> roomIds,
        CancellationToken cancellationToken = default);

    Task<bool> RoomNumberExistsAsync(
        string roomNumber,
        int? excludeRoomId,
        CancellationToken cancellationToken = default);

    Task<bool> HasAnyBookingsAsync(int roomId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetDistinctFloorsAsync(CancellationToken cancellationToken = default);

    void Add(Room room);
    void Update(Room room);
    void Remove(Room room);
}
```

| Gammel | Ny | Ændring og begrundelse |
|---|---|---|
| `void CreateRoom(Room)` | `void Add(Room)` | Omdøbt; typenavnet står allerede i interfacet, `Room`-suffikset var støj (Conventions: undgå gentagelse). Validering (BR-113) fjernet fra repoet. |
| `List<Room> GetAll()` | `GetAllAsync` | Async. |
| `Room? GetById(int)` | `GetByIdAsync` | Nullability nu ens i interface og klasse. |
| `List<Room> GetRoomsFromCriteria(int?, string, RoomStatus?)` | `GetByFilterAsync(RoomFilter, ct)` | Omdøbt og samlet i et filter-objekt. De tre løse parametre var på vej mod fem (BR-74 plus housekeeping). `uspGetRoomsFromCriteria` forsvinder; filterlogikken bliver læsbar C# i stedet for uverificerbar T-SQL (`Analysis.md` afsnit 2, "Uklart"). Signaturafvigelsen `string?` mod `string` dør med den. |
| `void UpdateRoom(Room)` | `void Update(Room)` | Validering ud af repoet. |
| `void DeleteRoom(int)` | `void Remove(Room)` | `catch (SqlException 547)` erstattes af et eksplicit `HasAnyBookingsAsync`-tjek i servicen. FK'en bevares som sidste værn, men reglen står ikke længere i en catch-blok. |
| `List<Room> GetAllByAvailability()` | **Fjernet** | Metoden var det tydeligste eksempel på BR-110: repoet besluttede hvad "tilgængelig" betød. Erstattes af `GetByStatusAsync(RoomStatus.Available)`, hvor *kalderen* vælger status, plus overlapsopslaget i servicen. Bonus: den lå slet ikke på interfacet i det gamle system, så kaldere skulle caste. |
| — | `GetByIdsAsync` | **Tilføjet.** Tilgængelighedsopslaget skal hente et sæt rum efter id uden N+1. |
| — | `RoomNumberExistsAsync` | **Tilføjet.** Se afsnit 10, punkt 5: unikt rumnummer er en *ny* regel og kræver godkendelse. |
| — | `HasAnyBookingsAsync` | **Tilføjet.** Bærer BR-112 op i servicen. |
| — | `GetDistinctFloorsAsync` | **Tilføjet.** BR-75's filtervalgmuligheder. Kun etage kræver et DB-opslag; størrelse og status kommer fra enums og hentes ikke fra data. |

### 2.4 `IGuestRepository`

```csharp
namespace NFHotel.Application.Guests;

public interface IGuestRepository
{
    Task<Guest?> GetByIdAsync(int guestId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int guestId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guest>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returnerer gæster hvor HVERT af de angivne søgeord (case-insensitivt) findes i
    /// enten fornavn, efternavn, land eller e-mail. Tomt sæt returnerer alle gæster.
    /// Pasnummer indgår aldrig i søgningen — feltet er krypteret at-rest (B-09).
    /// </summary>
    Task<IReadOnlyList<Guest>> SearchAsync(
        IReadOnlyCollection<string> searchTerms,
        CancellationToken cancellationToken = default);

    Task<bool> HasAnyBookingsAsync(int guestId, CancellationToken cancellationToken = default);

    void Add(Guest guest);
    void Update(Guest guest);
    void Remove(Guest guest);
}
```

| Gammel | Ny | Ændring og begrundelse |
|---|---|---|
| `int AddGuest(Guest)` | `void Add(Guest)` | Returnerede id'et fra `SCOPE_IDENTITY` med en ucheckt unbox (`Analysis.md`: `(int)cmd.ExecuteScalar()`). EF sætter id'et på entiteten efter `SaveChangesAsync`; servicen læser det derfra. |
| `Guest? GetByID(int id)` | `Task<Guest?> GetByIdAsync(int guestId, ct)` | Casing rettet, parameternavn ensrettet (det afveg mellem interface og klasse og brækkede named arguments). |
| `List<Guest> GetAll()` | `GetAllAsync` | Async. |
| `List<Guest> GetAllByName(string)` | `SearchAsync(IReadOnlyCollection<string> terms, ct)` | Omdøbt og udvidet. Det gamle `%navn%` på for- ELLER efternavn (BR-116) var kun halvdelen af den søgning brugeren faktisk fik: BR-68 i ViewModel'en delte teksten i ord og krævede at ALLE ord fandtes i navn, land eller e-mail. To søgninger, to semantikker. Ny: én. Tokeniseringen (reglen) sker i servicen, matchningen (dataadgangen) i repoet, og kontrakten står i XML-doc'en og pinnes af en Infrastructure-test. Se afsnit 10, punkt 6. |
| `void UpdateGuest(Guest)` | `void Update(Guest)` | Den gamle ignorerede `rowsAffected`, så opdatering af en ikke-eksisterende gæst fejlede lydløst. Nu fanges det af EF's `DbUpdateConcurrencyException`, som oversættes til en fejlkode. |
| `void DeleteGuest(int)` | `void Remove(Guest)` | FK-violation boblede op som rå `SqlException`. Servicen tjekker `HasAnyBookingsAsync` først. |
| — | `ExistsAsync`, `HasAnyBookingsAsync` | **Tilføjet.** Guard for sletning og for D-05's persondata-spor. |

Bemærk hvad der *ikke* er tilføjet: ingen søgning eller sortering på `PassportNumber`. Det er verificeret mod ledgeren i afsnit 6.4.

### 2.5 `IUnitOfWork` — nødvendig eller overflødig?

**Nødvendig.** Ikke fordi `DbContext` mangler unit-of-work-semantik — den har den — men fordi Application-laget skal kunne *styre* transaktionsgrænsen uden at kende EF.

Alternativet er selv-gemmende repositories, hvor `Add` internt kalder `SaveChangesAsync`. Det falder på tre punkter:

1. **BR-47 kræver atomicitet.** "Opret booking for ny gæst" skriver en `Guest` og en `Booking`. Gemmer repoet hver for sig, kan bookingen fejle på exclusion-constrainten (B-03) efter at gæsten er committet — og systemet står med en forældreløs gæst. Med én `SaveChangesAsync` er begge inserts i samme implicitte transaktion, og EF sorterer selv insert-rækkefølgen efter navigationen.
2. **Selv-gemmende repos lyver.** De tre repositories deler den samme scoped `DbContext`. Kalder `GuestRepository.Add` derfor `SaveChangesAsync`, committer den samtidig alt hvad `BookingRepository` og `RoomRepository` måtte have liggende som pending. Det er en usynlig kobling mellem tre klasser der tilsyneladende ikke kender hinanden. Det er værre end den eksplicitte afhængighed.
3. **Uden UoW kan servicen ikke skrive to entiteter og rulle tilbage.** Og servicen er præcis det lag der ejer use casen.

Interfacet holdes minimalt:

```csharp
namespace NFHotel.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

Ingen `BeginTransactionAsync`, ingen `Rollback`, ingen `IRepository<T>`-property-samling. Alle Fase 1-use cases klarer sig med ét `SaveChangesAsync`-kald; en eksplicit transaktions-API tilføjes den dag en use case skriver i to omgange, og ikke før. En generisk `IUnitOfWork` med repository-properties ville desuden gøre servicernes konstruktør-afhængigheder usynlige for test.

Implementeringen i Infrastructure er en tolinjers adapter over `HotelDbContext`, som samtidig er det sted hvor Postgres-fejlkoder oversættes til Application-exceptions (afsnit 5.4).

---

## 3. Services

Fælles form: konstruktørinjektion (Conventions), `CancellationToken` sidst, `Result`/`Result<T>` retur, ingen UI-typer, ingen `MessageBox`, ingen `Debug.WriteLine`. Testen fra `Fase1-Scope.md` gælder for hver eneste metode: *kan en konsol-app kalde den og få alle regler håndhævet?*

### 3.1 `IBookingService`

```csharp
namespace NFHotel.Application.Bookings;

public interface IBookingService
{
    Task<Result<BookingDetailsDto>> GetByIdAsync(
        int bookingId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BookingListItemDto>>> GetOverviewAsync(
        BookingOverviewQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AvailableRoomDto>>> GetAvailableRoomsAsync(
        DateOnly checkInDate,
        DateOnly checkOutDate,
        int? excludeBookingId,
        CancellationToken cancellationToken = default);

    Task<Result<BookingDetailsDto>> CreateAsync(
        CreateBookingRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<BookingDetailsDto>> ConfirmAsync(
        int bookingId,
        CancellationToken cancellationToken = default);

    Task<Result<BookingDetailsDto>> CheckInAsync(
        int bookingId,
        CancellationToken cancellationToken = default);

    Task<Result<BookingDetailsDto>> CheckOutAsync(
        int bookingId,
        CancellationToken cancellationToken = default);

    Task<Result<BookingDetailsDto>> CancelAsync(
        int bookingId,
        CancellationToken cancellationToken = default);

    Task<Result<BookingDetailsDto>> RescheduleAsync(
        RescheduleBookingRequest request,
        CancellationToken cancellationToken = default);
}
```

```csharp
public sealed class BookingService : IBookingService
{
    public BookingService(
        IBookingRepository bookingRepository,
        IRoomRepository roomRepository,
        IGuestRepository guestRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<BookingService> logger);
}
```

| Metode | Håndhæver | Noter |
|---|---|---|
| `GetByIdAsync` | BR-115 | `null` fra repoet → `Result.Failure(Booking.NotFound)`. |
| `GetOverviewAsync` | BR-23, BR-24, BR-25, BR-32, BR-111 | Periodefilter altid aktivt (retter at uge/måned ikke filtrerede). Sortering via `BookingSortField`-enum, ikke magiske strenge. |
| `GetAvailableRoomsAsync` | BR-34, BR-36, BR-37, BR-38, BR-39, BR-40, BR-110 | `excludeBookingId` sat ved redigering, så bookingen ikke blokerer sig selv (den manglede i det gamle system og gjorde BR-19 unødigt indviklet). |
| `CreateAsync` | BR-01, BR-41, BR-42, BR-43, BR-44, BR-45, BR-46, BR-47, BR-48, BR-49, BR-51, BR-101–BR-106, plus overlapstjek (B-03) | Overlapstjekket er nyt — det fandtes hverken i `CreateBooking` eller i repoet. Alle valideringsfejl samles (BR-46's "kun første fejl" dør, konsolideret med BR-60/BR-96). |
| `ConfirmAsync` | BR-02, BR-03, BR-05, BR-121 | Bekræftelsesdialogen (BR-04) er UI og bliver i Web. |
| `CheckInAsync` | BR-06, BR-07, BR-08 | `CheckInTime = _clock.UtcNow`; "i dag" er hotel-lokal dato via `IClock`, ikke `DateTime.Now`. |
| `CheckOutAsync` | BR-09, BR-10, BR-107 | |
| `CancelAsync` | BR-11, BR-13, BR-122 | Soft-delete: status sættes, rækken bliver. |
| `RescheduleAsync` | BR-14, BR-17, BR-18, BR-19, BR-20, BR-108, BR-123, plus B-03 | Samme overlapsfunktion som `CreateAsync`. Navnet er bevidst ikke `UpdateAsync`: use casen er "flyt bookingen", ikke "sæt vilkårlige felter". |

BR-22 håndteres ikke som en metode, men som `BookingActionsDto` på hver booking-DTO — se afsnit 4.2.

Bemærk placeringen af `GetAvailableRoomsAsync`: den returnerer rum, men reglen er en *bookingregel* (overlap i en periode). Lægges den på `RoomService`, skal `RoomService` kende `IBookingRepository`, og så har begge services begge repositories. Med den nuværende fordeling er `RoomService` fri for bookinger på nær sletteguarden, som bruger et rum-side-opslag.

### 3.2 `IRoomService`

```csharp
namespace NFHotel.Application.Rooms;

public interface IRoomService
{
    Task<Result<RoomDetailsDto>> GetByIdAsync(
        int roomId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<RoomListItemDto>>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<RoomListItemDto>>> SearchAsync(
        RoomFilter filter,
        CancellationToken cancellationToken = default);

    Task<Result<RoomFilterOptionsDto>> GetFilterOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<Result<RoomDetailsDto>> CreateAsync(
        CreateRoomRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RoomDetailsDto>> UpdateAsync(
        UpdateRoomRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<RoomDetailsDto>> ChangeStatusAsync(
        int roomId,
        RoomStatus newStatus,
        CancellationToken cancellationToken = default);

    Task<Result<RoomDetailsDto>> ChangeHousekeepingStatusAsync(
        int roomId,
        HousekeepingStatus newStatus,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int roomId,
        CancellationToken cancellationToken = default);
}
```

```csharp
public sealed class RoomService : IRoomService
{
    public RoomService(
        IRoomRepository roomRepository,
        IUnitOfWork unitOfWork,
        ILogger<RoomService> logger);
}
```

| Metode | Håndhæver | Noter |
|---|---|---|
| `GetByIdAsync` | BR-115 | |
| `GetAllAsync` | — | |
| `SearchAsync` | BR-74, BR-76 | "Ryd filter" = et tomt `RoomFilter`; ingen særskilt metode. Retter samtidig at filteret kørte to gange pr. popup-åbning (view-fejl). |
| `GetFilterOptionsAsync` | BR-75 | Etager fra data; størrelse, status og housekeeping fra enums — hvilket automatisk løser BR-126, hvor XAML og enum var uenige. |
| `CreateAsync` | BR-81, BR-84, BR-97, BR-98, BR-99, BR-100, BR-113 | BR-84 (validering blev ignoreret i formularen) kan ikke gentages: entiteten kan ikke konstrueres ugyldigt, og servicen returnerer `Result.Failure` i stedet for at gemme. |
| `UpdateAsync` | BR-82, BR-84, BR-97–BR-100, BR-113 | BR-82 ("RoomId == 0 betyder opret") dør: to metoder, to use cases. |
| `ChangeStatusAsync` | BR-126, F-01 | Egen use case, fordi et statusskift ikke skal kræve at hele rummet sendes med. |
| `ChangeHousekeepingStatusAsync` | F-01, D-10 | Backoffice skal kunne markere et rum rengjort. Metoden er samtidig den flade mobil-guilden senere hænger sit API på — uden at der bygges noget mobilspecifikt nu. |
| `DeleteAsync` | BR-78, BR-112 | Eksplicit `HasAnyBookingsAsync` før sletning. FK'en er stadig sidste værn, men reglen står ikke i en catch. BR-77's advarselsdialog er UI. |

### 3.3 `IGuestService`

```csharp
namespace NFHotel.Application.Guests;

public interface IGuestService
{
    Task<Result<GuestDetailsDto>> GetByIdAsync(
        int guestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<GuestListItemDto>>> SearchAsync(
        string? searchText,
        CancellationToken cancellationToken = default);

    Task<Result<GuestDetailsDto>> CreateAsync(
        CreateGuestRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<GuestDetailsDto>> UpdateAsync(
        UpdateGuestRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int guestId,
        CancellationToken cancellationToken = default);

    Result ValidateFields(GuestFields fields);
}
```

```csharp
public sealed class GuestService : IGuestService
{
    public GuestService(
        IGuestRepository guestRepository,
        IUnitOfWork unitOfWork,
        ILogger<GuestService> logger);
}
```

| Metode | Håndhæver | Noter |
|---|---|---|
| `GetByIdAsync` | BR-115, BR-35 | Bruges også til at forudfylde ny booking for kendt gæst. |
| `SearchAsync` | BR-68, BR-69, BR-70, BR-116 | Tokenisering og AND-logik her; sortering på "fornavn efternavn" i lowercase (BR-69) sker som en del af use casen, ikke i UI. Tom/whitespace-søgning giver alle (BR-70). |
| `CreateAsync` | BR-52–BR-57, BR-60, BR-91–BR-96 | Alle fejl samlet i ét `Result` (BR-60/BR-96). BR-46's "kun første fejl" er dermed konsolideret væk. |
| `UpdateAsync` | BR-62, BR-66, BR-72, BR-91–BR-96 | `GuestId` er påkrævet og ikke-nul i requesten, hvilket lukker fejlen fra `Analysis.md` afsnit 3 punkt 7, hvor en redigeret gæst mistede sit id og blev opdateret på id 0. BR-66's "arbejd på en kopi" bliver overflødigt: requesten *er* kopien. |
| `DeleteAsync` | D-05 (nyt), guard mod eksisterende bookinger | Ikke i ledgeren; markeret som ny regel der kræver godkendelse. |
| `ValidateFields` | BR-58, BR-59 | **Bevidst ikke async** — der er ingen IO (Conventions). Lader UI'et give feedback pr. tastetryk og aktivere/deaktivere gemknappen uden at duplikere en eneste valideringsregel. Det er metoden der gør at BR-52..BR-56 aldrig behøver eksistere i Web-laget. |

**Ingen `SalesService` i Fase 1.** Refaktoreringsplanens afsnit 3 nævner den, men BR-31 fastslår at omsætning altid sættes til 0, og der findes ingen pris nogen steder i domænet. En service der returnerer 0 er værre end ingen service: den ligner en implementering. Se afsnit 10, punkt 7.

---

## 4. DTO'er og mapping

### 4.1 Princip

DTO'erne defineres i `Application` (Fase1-Scope, "billig insurance") som `sealed record`-typer med init-only properties. De indeholder ingen formatering: datoer er `DateOnly`/`DateTimeOffset`, ikke strenge; status er enum, ikke farve. `Conventions.md`: *"Domain models må ikke fyldes med UI-formatering"* — det gælder også DTO'erne, ellers flytter problemet bare ét lag.

### 4.2 Bookings

```csharp
public sealed record BookingActionsDto(
    bool CanConfirm,
    bool CanCheckIn,
    bool CanCheckOut,
    bool CanCancel,
    bool CanEdit);

public sealed record BookingListItemDto(
    int BookingId,
    string BookingNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    int Nights,
    BookingStatus Status,
    DateTimeOffset? CheckInTime,
    DateTimeOffset? CheckOutTime,
    int RoomId,
    string RoomNumber,
    int GuestId,
    string GuestFullName,
    string GuestCountry,
    string GuestEmail,
    BookingActionsDto Actions);

public sealed record BookingDetailsDto(
    int BookingId,
    string BookingNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    int Nights,
    BookingStatus Status,
    DateTimeOffset? CheckInTime,
    DateTimeOffset? CheckOutTime,
    RoomListItemDto Room,
    GuestListItemDto Guest,
    BookingActionsDto Actions);

public sealed record AvailableRoomDto(
    int RoomId,
    string RoomNumber,
    int Floor,
    RoomSize Size,
    int Capacity);
```

`BookingActionsDto` er den vigtigste enkeltdel i hele DTO-designet. Den er beregnet i `Application` ud fra `Domain`s tilstandsregler og gør at Web-laget kan tegne knapper uden at kende en eneste statusovergang. Det er sådan BR-121, BR-122 og BR-123 (XAML-triggere) og BR-02, BR-11, BR-14, BR-22 (ViewModel) bliver til **én** implementering i stedet for seks.

**Requests:**

```csharp
public sealed record CreateBookingRequest(
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    int RoomId,
    int? GuestId,
    GuestFields? NewGuest);

public sealed record RescheduleBookingRequest(
    int BookingId,
    DateOnly NewStartDate,
    DateOnly NewEndDate,
    int RoomId);

public sealed record BookingOverviewQuery(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string? SearchText,
    BookingSortField SortBy,
    SortDirection Direction,
    bool IncludeCancelled);

public enum BookingSortField
{
    BookingNumber, GuestName, RoomNumber, StartDate, EndDate, Status, Nights
}
```

`CreateBookingRequest` bærer BR-47: enten `GuestId` eller `NewGuest`, præcis én af dem. At begge eller ingen er sat, er en valideringsfejl fra servicen — ikke en `NullReferenceException`.

### 4.3 Rooms og Guests

```csharp
public sealed record RoomListItemDto(
    int RoomId, string RoomNumber, int Floor, RoomSize Size, int Capacity,
    RoomStatus Status, HousekeepingStatus HousekeepingStatus);

public sealed record RoomDetailsDto(
    int RoomId, string RoomNumber, int Floor, RoomSize Size, int Capacity,
    RoomStatus Status, HousekeepingStatus HousekeepingStatus, bool HasBookings);

public sealed record RoomFilterOptionsDto(
    IReadOnlyList<int> Floors,
    IReadOnlyList<RoomSize> Sizes,
    IReadOnlyList<RoomStatus> Statuses,
    IReadOnlyList<HousekeepingStatus> HousekeepingStatuses);

public sealed record RoomFilter(
    int? Floor, RoomSize? Size, RoomStatus? Status, HousekeepingStatus? HousekeepingStatus);

public sealed record CreateRoomRequest(
    string RoomNumber, int Floor, RoomSize Size, int Capacity);

public sealed record UpdateRoomRequest(
    int RoomId, string RoomNumber, int Floor, RoomSize Size, int Capacity, RoomStatus Status);

public sealed record GuestFields(
    string FirstName, string LastName, string Email,
    string PhoneNumber, string Country, string? PassportNumber);

public sealed record GuestListItemDto(
    int GuestId, string FirstName, string LastName, string FullName,
    string Email, string PhoneNumber, string Country, bool HasPassportNumber);

public sealed record GuestDetailsDto(
    int GuestId, string FirstName, string LastName, string FullName,
    string Email, string PhoneNumber, string Country, string? PassportNumber);

public sealed record CreateGuestRequest(GuestFields Fields);

public sealed record UpdateGuestRequest(int GuestId, GuestFields Fields);
```

`GuestListItemDto` har `HasPassportNumber` (bool), ikke selve nummeret. Lister og oversigter har aldrig brug for værdien, og med B-09 er hver visning af feltet en dekryptering. Kun `GuestDetailsDto` bærer klartekst — og kun den bruges i gæstens redigeringsformular. Det er privacy by design (D-04) uden at koste funktionalitet: ingen af de 126 regler viser pasnummer i en liste.

### 4.4 Mapping-strategi

**Eksplicit, håndskrevet, én statisk mapper-klasse pr. feature**, placeret i `<Feature>/Mapping/`, med extension-metoder:

```csharp
namespace NFHotel.Application.Bookings.Mapping;

public static class BookingMappings
{
    public static BookingListItemDto ToListItem(this Booking booking, DateOnly today);
    public static BookingDetailsDto ToDetails(this Booking booking, DateOnly today);
    public static BookingActionsDto ToActions(this Booking booking, DateOnly today);
}
```

**Ingen AutoMapper.** Begrundelse:

- `Conventions.md`: *"Mapping skal være eksplicit og let at finde"*. En profilklasse med konventionsbaseret navnematchning er hverken.
- Omfanget er tre entiteter og ni DTO'er. AutoMapper ville koste konfiguration, en pakke, en opstartsvalidering og en læringstærskel for at spare måske 60 linjer triviel tildeling.
- Feltvalgene er *beslutninger*, ikke boilerplate: at `GuestListItemDto` bærer `HasPassportNumber` i stedet for værdien er en privacy-beslutning (B-09/D-04). Konventionsbaseret mapping ville forsøge at kopiere `PassportNumber` automatisk og dermed dekryptere feltet i enhver liste — stille og af sig selv.
- `ToActions` er ikke mapping, det er regelevaluering, og den skal være læsbar og testbar.

`today` sendes ind fra servicen (der har `IClock`), så mapperne er rene funktioner og kan enhedstestes uden fake-tid.

**Modsat retning — request til entitet — findes ikke som mapping.** Services konstruerer entiteter gennem `Domain`s konstruktører/fabriksmetoder og ændrer dem gennem domænemetoder. Der bliver aldrig bulk-kopieret properties ind i en entitet, for det er præcis dér invarianter plejer at forsvinde.

---

## 5. Fejlhåndtering

### 5.1 Valget

**`Result` / `Result<T>` til regelovertrædelser. Exceptions til programmørfejl og infrastrukturfejl.**

Begrundelse:

- En regelovertrædelse er et **forventet udfald**, ikke en undtagelse. "Denne booking er allerede checket ind" sker hver dag. Exceptions til forventede udfald er dyre, gør stack traces støjende og — vigtigst — kan ignoreres af en kalder der ikke ved de findes.
- `Result` er synligt i signaturen. Kalderen kan ikke undlade at forholde sig til det uden at det ses i koden. Det er den direkte modgift mod BR-07/BR-10, hvor check-in og check-ud fejlede ned i `Debug.WriteLine` mens statussen allerede var ændret i hukommelsen (Q-25).
- Flere fejl på én gang kræves af BR-60 og BR-96 ("saml alle fejl i én liste"). Exceptions er dårlige til det; en fejlliste er naturlig i et `Result`.
- Web/Api er begge klienter til det samme `Result`. Blazor viser en `Alert`, et senere Api mapper fejlkoden til 409/422. Havde vi kastet, skulle begge lag genopfinde oversættelsen.

### 5.2 Typerne

```csharp
namespace NFHotel.Application.Common.Results;

public sealed record Error(string Code, string Message);

public class Result
{
    protected Result(bool isSuccess, IReadOnlyList<Error> errors);

    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public IReadOnlyList<Error> Errors { get; }
    public Error? FirstError { get; }

    public static Result Success();
    public static Result Failure(Error error);
    public static Result Failure(IReadOnlyList<Error> errors);
}

public sealed class Result<TValue> : Result
{
    public TValue Value { get; }

    public static Result<TValue> Success(TValue value);
    public static new Result<TValue> Failure(Error error);
    public static new Result<TValue> Failure(IReadOnlyList<Error> errors);
}
```

```csharp
public static class ErrorCodes
{
    public static class Booking
    {
        public const string NotFound            = "booking.not_found";
        public const string NotPending          = "booking.not_pending";
        public const string CheckInNotAllowed   = "booking.check_in_not_allowed";
        public const string CheckOutNotAllowed  = "booking.check_out_not_allowed";
        public const string CancelNotAllowed    = "booking.cancel_not_allowed";
        public const string EditNotAllowed      = "booking.edit_not_allowed";
        public const string RoomNotAvailable    = "booking.room_not_available";
        public const string StartDateRequired   = "booking.start_date_required";
        public const string EndDateRequired     = "booking.end_date_required";
        public const string EndBeforeStart      = "booking.end_before_start";
        public const string StartInPast         = "booking.start_in_past";
        public const string RoomRequired        = "booking.room_required";
        public const string GuestRequired       = "booking.guest_required";
        public const string GuestSelectionInvalid = "booking.guest_selection_invalid";
    }

    public static class Room { /* ... */ }
    public static class Guest { /* ... */ }
}
```

**Fejlkoden er kontrakten, teksten er en bekvemmelighed.** UI'et vælger tekst ud fra koden — så kan feltet oversættes, og de gamle brugervendte engelske strenge forsvinder fra forretningslaget. Det gamle system havde brugervendt tekst helt nede i `RoomRepo`s catch-blok (BR-112).

### 5.3 Hvad der stadig kastes

| Situation | Reaktion |
|---|---|
| `null` argument, negativt id, tom påkrævet request | `ArgumentNullException` / `ArgumentException` som guard clause. Det er en programmørfejl, ikke et brugerudfald. Bevarer BR-114's `ArgumentNullException`-kontrakt (T-32). |
| Databasen er nede, timeout, netværksfejl | Boblede exception. Fanges centralt i Web (`ErrorBoundary` + `ILogger`), logges teknisk, brugeren ser en neutral besked (Conventions: *"Log tekniske fejl centralt. Vis brugeren neutrale fejlbeskeder"*). |
| Optimistisk samtidighedskonflikt | `DbUpdateConcurrencyException` oversættes i Infrastructure til `ConcurrencyConflictException` (Application), som servicen fanger og returnerer som `Result.Failure`. |
| Exclusion constraint rammer (B-03) | Se nedenfor. |

**Ingen tomme catch-blokke. Ingen `catch (Exception) { return false; }`** som i `DatabaseConfig.TestConnection`. Enhver catch logger eller oversætter.

### 5.4 Samspillet mellem servicetjek og databaseværn (B-03)

Overlap afvises to steder, og de to steder gør ikke det samme:

1. `BookingService` kalder den rene funktion i `Domain` mod kandidaterne fra `GetCandidatesForOverlapAsync`. Det er **den normale vej**, den giver en pæn fejlkode og en brugbar besked, og den er dækket af unit-tests uden database.
2. PostgreSQLs exclusion constraint fanger **kapløbet**: to samtidige oprettelser der begge så et ledigt rum. `SaveChangesAsync` fejler så med `PostgresException` og `SqlState = 23P01`.

`UnitOfWork.SaveChangesAsync` oversætter `23P01` (med constraint-navnet som diskriminator) til `BookingOverlapConflictException`, defineret i `Application.Common.Exceptions`. `BookingService.CreateAsync` og `RescheduleAsync` fanger netop den og returnerer `Result.Failure(ErrorCodes.Booking.RoomNotAvailable)` — **samme fejlkode som servicetjekket**. Brugeren ser det samme uanset hvilket af de to værn der stoppede hende; forskellen er kun synlig i loggen.

Det er også grunden til at oversættelsen sker i Infrastructure og ikke i servicen: Application må ikke kende `PostgresException`, og servicen skal ikke skulle vide hvad `23P01` betyder.

Tilsvarende: `23505` (unique violation) → `UniqueConstraintViolationException` → `ErrorCodes.Room.RoomNumberAlreadyExists`. `23503` (FK violation ved sletning) → oversættes til rum-/gæste-sletteguardens fejlkode, som sidste værn bag det eksplicitte `HasAnyBookingsAsync`-tjek.

---

## 6. EF Core-konfiguration

### 6.1 `HotelDbContext`

```csharp
namespace NFHotel.Infrastructure.Persistence;

public sealed class HotelDbContext : DbContext
{
    public HotelDbContext(DbContextOptions<HotelDbContext> options);

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Guest> Guests => Set<Guest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder);
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder);
}
```

`OnModelCreating` kalder `HasPostgresExtension("btree_gist")` og `ApplyConfigurationsFromAssembly(typeof(HotelDbContext).Assembly)`. Ingen entitetskonfiguration ligger i `OnModelCreating` selv — én konfigurationsklasse pr. entitet, ét ansvar pr. fil (Conventions).

Providers og konventioner sættes ved registrering:

```csharp
options.UseNpgsql(connectionString, npgsql => npgsql
        .MigrationsAssembly(typeof(HotelDbContext).Assembly.FullName)
        .EnableRetryOnFailure())
       .UseSnakeCaseNamingConvention();   // EFCore.NamingConventions — B-06
```

`UseSnakeCaseNamingConvention()` gør `BookingId` til `booking_id` og `Booking` til `booking`. C# forbliver PascalCase, PostgreSQL slipper for quotede identifiers, og U-19's casing-kaos (`RoomID` mod `RoomId` mod `ROOM`) har ingen steder at opstå.

Læsestrategi: alle query-metoder i repositories bruger `AsNoTracking()`, fordi de fodrer DTO-mapping. Kun `GetByIdAsync` i skrive-use cases henter tracked. Lazy loading er slået fra; navigationer hentes med eksplicit `Include`. Det er den tekniske forudsætning for T-35 (eager loading af `Room` og `Guest`).

### 6.2 `BookingConfiguration`

- Nøgle: `BookingId`, identity (`ValueGeneratedOnAdd`).
- `StartDate`, `EndDate`: `DateOnly` → `date`. Npgsql mapper `DateOnly` til `date` uden konverter (B-08). Ingen `ValueConverter` skal skrives.
- `CheckInTime`, `CheckOutTime`: `DateTimeOffset?` → `timestamptz`, nullable. Npgsql kræver at værdien har offset 0; det sikres ved at alle tidsstempler kommer fra `IClock.UtcNow` — aldrig fra `DateTime.Now` (som i BR-07/BR-10).
- `Status`: `.HasConversion<int>()` (B-07), ordinal 0–4 bevaret.
- Valgfri, anbefalet: `HasCheckConstraint("ck_booking_status_range", "status BETWEEN 0 AND 4")`. Det gamle skema havde ingen, så en vilkårlig int kunne indsættes (`Analysis.md` afsnit 1).
- Relationer: `HasOne(b => b.Room).WithMany().HasForeignKey(b => b.RoomId).OnDelete(DeleteBehavior.Restrict)` og tilsvarende for `Guest`. `Restrict` er det der bærer BR-112 som DB-værn.
- `BookingNumber` og `NumberOfNights`: `.Ignore(...)` — afledte værdier, ingen kolonne. Uændret fra det gamle system, hvor `BookingNumber` heller ikke havde en kolonne.
- Samtidighed: `Property<uint>("xmin").IsRowVersion().HasColumnName("xmin")`. Gratis i PostgreSQL, og det lukker det læs-så-skriv-hul som `BookingRepo.Update` havde over to separate connections.

**Exclusion constraint (B-03).** EF kan ikke modellere den, så den skrives i migrationen:

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE booking
  ADD CONSTRAINT ex_booking_room_period
  EXCLUDE USING gist (
      room_id WITH =,
      daterange(start_date, end_date, '[)') WITH &&
  )
  WHERE (status <> ALL (ARRAY[3, 4]));
```

Tre ting er værd at bemærke:

- **`'[)'` er halvåben** og svarer nøjagtigt til den halvåbne test i BR-19 og BR-39 (`andenStart < nytUdtjek AND andenSlut > nytIndtjek`). Ankomst samme dag som en afrejse er dermed tilladt, både i domænet og i databasen.
- **`WHERE`-prædikatet skal spejle `BookingRules`' spærrende statussæt præcist.** Ovenstående udelader `CheckedOut` (3) og `Cancelled` (4), altså BR-19's variant. Vælger `design:domain` BR-39's variant (kun `Cancelled` udelades), skal prædikatet være `status <> 4`. Prædikatet må aldrig være strengere end domænereglen — se afsnit 10, punkt 3, hvor konsekvensen for tidlig udtjekning er skrevet ud.
- Constrainten skaber sit eget GiST-indeks på `(room_id, daterange(...))`, som samtidig betjener overlapsopslaget fra `GetCandidatesForOverlapAsync`.

### 6.3 `RoomConfiguration`

- Nøgle: `RoomId`.
- `RoomNumber`: `string`, `HasMaxLength(20)`, `IsRequired()`. Løser konflikten fra `Analysis.md` afsnit 1, hvor C# sagde `string` og begge create-scripts sagde `INT`, patchet bagefter af et script der kun fandtes i det ene SQL-sæt. Nyt skema, én type, ingen patch.
- Unikt indeks på `room_number` — **ny regel**, se afsnit 10, punkt 5.
- `Status` og `HousekeepingStatus`: `.HasConversion<int>()`.
- `RoomSize`: `.HasConversion<int>()` hvis `design:domain` gør den til enum (anbefalet af Fase1-Scope, "billig insurance"). Bliver den en string, ændres denne linje og `RoomFilter`s felttype.
- `Floor`, `Capacity`: `int`, plus valgfri check constraints `floor > 0` (BR-98) og `capacity > 0` (BR-100) som DB-værn bag domæneinvarianten.

### 6.4 `GuestConfiguration` og kryptering (B-09)

```csharp
public sealed class EncryptedStringConverter : ValueConverter<string, string>
{
    public EncryptedStringConverter(IStringEncryptor encryptor);
}
```

Konfigureres som:

```csharp
builder.Property(g => g.PassportNumber)
       .HasConversion(encryptedStringConverter)
       .HasColumnType("text");
```

Konverteren instantieres i `GuestConfiguration`s konstruktør med en injiceret `IStringEncryptor` (AES-GCM, nøgle fra `EncryptionOptions`, bundet fra `dotnet user-secrets` i dev og fra miljøvariabler/Actions Secrets i CI, jf. D-07). Konfigurationsklassen registreres derfor eksplicit i `OnModelCreating` frem for via assembly-scanning, da den har en afhængighed.

**Konsekvenser, konkret:**

| Konsekvens | Detalje |
|---|---|
| Ingen `LIKE`/`ILIKE` på feltet | Ciphertext er ikke søgbar. Enhver `WHERE passport_number LIKE ...` ville ramme krypteret tekst. |
| Ingen `ORDER BY` på feltet | Sorteringsrækkefølge er meningsløs på ciphertext. |
| Ingen index, heller ikke unikt | AES-GCM er randomiseret (ny nonce pr. kryptering), så samme pasnummer giver forskellig ciphertext hver gang. Selv lighedsopslag virker ikke. |
| Kolonnetype | `text`, ikke `varchar(50)`. Ciphertext + nonce + tag, base64-kodet, fylder betydeligt mere end de 50 tegn den gamle `NVARCHAR(50)` gav. |
| `null` bevares | EF kalder ikke value converters for `null`, så `NULL` forbliver `NULL` i databasen. Det bevarer T-41 ("pasnummer kan fjernes igen, sættes til NULL, ikke tom streng") — men det kræver at Domain skelner `null` fra `""`. Skal verificeres i en Infrastructure-test. |
| Nøglerotation | Ikke løst i Fase 1. Rotation kræver læs-dekrypter-genkrypter over hele tabellen. Noteres som en åben opgave, ikke som en implementering. |

**Verifikation mod ledgeren.** Jeg har gennemgået alle regler der rører søgning eller sortering:

| Regel | Felter der søges/sorteres på | Rammer pasnummer? |
|---|---|---|
| BR-116 (`GuestRepo.GetAllByName`) | fornavn, efternavn | Nej |
| BR-68 (gæstesøgning i VM) | fuldt navn, land, e-mail | Nej |
| BR-69 (gæstesortering) | "fornavn efternavn", lowercase | Nej |
| BR-25 (bookingsøgning) | navn, land, e-mail, værelsesnummer | Nej |
| BR-74/BR-75 (rumfilter) | etage, størrelse, status | Nej |
| BR-32 (bookingsortering) | kolonner i bookingoversigten | Nej |
| BR-35 (forudfyld fra valgt gæst) | opslag på `GuestId`, ikke søgning | Nej |
| BR-57 / T-14 (pasnummer valgfrit) | ingen søgning | Nej |
| T-41 (pasnummer kan nulstilles) | opdatering på id | Nej |

**Konklusion: ingen af de 126 regler søger eller sorterer på pasnummer.** B-09's antagelse holder. Den eneste reelle konsekvens er den tekniske (kolonnetype, ingen index, nøglehåndtering) plus DTO-valget i afsnit 4.3.

### 6.5 Indekser — hvilke af de gamle bevares

Det gamle skema havde fem: `IX_Booking_StartDate`, `IX_Booking_EndDate`, `IX_Booking_Status`, `IX_Booking_RoomID`, `IX_Booking_GuestID` — og de forsvandt hvis `fix-booking-identity.sql` blev kørt.

| Gammelt indeks | Beslutning | Begrundelse |
|---|---|---|
| `IX_Booking_RoomID` | **Bevares** som `ix_booking_room_id` | PostgreSQL opretter ikke automatisk indeks på FK-siden. Nødvendigt for `GetByRoomIdAsync` og for FK-tjekket ved rumsletning (BR-112). |
| `IX_Booking_GuestID` | **Bevares** som `ix_booking_guest_id` | Samme, plus `GetByGuestIdAsync`. |
| `IX_Booking_Status` | **Bevares, men som composite** `ix_booking_status_start_date (status, start_date)` | Status alene har fem distinkte værdier og er sjældent selektiv nok. Oversigten filtrerer altid på status *og* periode (BR-23 + BR-24), så det sammensatte indeks matcher det faktiske query-mønster. |
| `IX_Booking_StartDate` | **Erstattes** | Dækket af det sammensatte indeks ovenfor og af GiST-indekset. |
| `IX_Booking_EndDate` | **Droppes** | Ingen query filtrerer på slutdato alene. Periodeoverlap går gennem GiST-indekset. |
| — | **Nyt:** GiST-indeks via `ex_booking_room_period` | Følger automatisk med exclusion constrainten (B-03). Betjener overlapsopslaget. |
| — | **Nyt:** `ux_room_room_number` (unikt) | Ny regel, kræver godkendelse — se afsnit 10, punkt 5. |
| — | **Nyt:** `ix_guest_last_name_first_name` | Gæstesøgningen (BR-68) og sorteringen (BR-69). Overvej i stedet et GIN/`pg_trgm`-indeks hvis `ILIKE '%x%'` bliver langsomt; med det datavolumen der er tale om, er btree rigeligt i Fase 1. |
| — | **Nyt:** `ix_room_status` | Tilgængelighedsopslaget (BR-110's efterfølger). |

Ingen indeks på `guest.passport_number` — kan ikke lade sig gøre, jf. 6.4.

### 6.6 Migrations

Migrations skrives forfra mod PostgreSQL; `01_CreateSchema.sql` er reference, ikke kilde. De to uforenelige SQL-sæt og `fix-booking-identity.sql` porteres ikke. Navngivning: `20260907_InitialSchema`, `20260907_AddBookingOverlapConstraint` osv. — én ting pr. migration, som ved commits.

Exclusion constraint, `btree_gist`-extension og eventuelle check constraints tilføjes med `migrationBuilder.Sql(...)` i `Up` og modsvarende `DROP` i `Down`. `Down` skal virke; en migration der ikke kan rulles tilbage, er ikke færdig.

Migrations køres **ikke** automatisk ved opstart i andet end Development. `context.Database.Migrate()` i `Program.cs` er bekvemt og farligt; brug `dotnet ef database update` eller et separat migrator-trin i pipelinen.

---

## 7. DI-registrering

### 7.1 Composition root

`NFHotel.Web/Program.cs` er composition root (Conventions). Den kender to extension-metoder og ellers ingenting om hverken EF eller repositories:

```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWebFeatures();   // ViewModels — design:web
```

### 7.2 `AddApplication`

```csharp
namespace NFHotel.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services);
}
```

| Registrering | Levetid | Begrundelse |
|---|---|---|
| `IBookingService` → `BookingService` | Scoped | Følger `DbContext`s levetid gennem repositories. Tilstandsløs i sig selv, men må ikke overleve sin kontekst. |
| `IRoomService` → `RoomService` | Scoped | Samme. |
| `IGuestService` → `GuestService` | Scoped | Samme. |

Ingen assembly-scanning. Tre linjer er lettere at læse end en konvention der finder dem, og en glemt registrering fejler så ved opstart i stedet for at blive fundet ved et tilfælde.

### 7.3 `AddInfrastructure`

```csharp
namespace NFHotel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration);
}
```

| Registrering | Levetid | Begrundelse |
|---|---|---|
| `HotelDbContext` (via `AddDbContextFactory` + scoped adapter) | Scoped | Se 7.4. |
| `IUnitOfWork` → `UnitOfWork` | Scoped | Skal være *samme* instans som repositories' kontekst. |
| `IBookingRepository` → `BookingRepository` | Scoped | Holder en `DbContext`. |
| `IRoomRepository` → `RoomRepository` | Scoped | Samme. |
| `IGuestRepository` → `GuestRepository` | Scoped | Samme. |
| `IClock` → `SystemClock` | Singleton | Tilstandsløs. |
| `IStringEncryptor` → `AesGcmStringEncryptor` | Singleton | Holder nøglen; `AesGcm` instantieres pr. operation. |
| `EncryptionOptions` | Options + `ValidateOnStart()` | **Appen skal nægte at starte uden krypteringsnøgle.** Alternativet er at pasnumre gemmes i klartekst uden at nogen opdager det. |
| `HotelTimeOptions` (tidszone) | Options + `ValidateOnStart()` | "I dag" i BR-06 og BR-44 skal være hotel-lokal, ikke server-lokal. |

Connection string læses fra `configuration.GetConnectionString("HotelDatabase")` med `ValidateOnStart`. Ingen statisk mutérbar global som `DatabaseConfig`, der stille kunne blive `null` hvis nøglen manglede — den enkeltklasse er ansvarlig for en stor del af det gamle systems utestbarhed.

### 7.4 Blazor Server og `DbContext`-levetid

Dette er den ene DI-detalje der kan give reelle produktionsfejl, og den skal koordineres med `design:web`.

I Blazor Server er en scope **hele SignalR-kredsløbet** — potentielt timer. En scoped `DbContext` ville derfor: (a) akkumulere tracked entiteter i hele brugerens session, og (b) kunne blive brugt af to samtidige komponent-hændelser, hvilket kaster `InvalidOperationException: A second operation started on this context`.

Design:

```csharp
services.AddDbContextFactory<HotelDbContext>(
    (sp, options) => { /* UseNpgsql + UseSnakeCaseNamingConvention */ },
    lifetime: ServiceLifetime.Scoped);

services.AddScoped<HotelDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<HotelDbContext>>().CreateDbContext());
```

Plus et krav til Web-laget, som skrives ind i `Architecture.md`: **hver side/ViewModel får sin egen DI-scope** (via `OwningComponentBase` eller en eksplicit `IServiceScopeFactory`-scope pr. use case), og to samtidige operationer må aldrig dele samme kontekst. Så bliver "scoped" i praksis "pr. side", ikke "pr. session".

Et senere `Api`-projekt kan genbruge `AddApplication` og `AddInfrastructure` uændret — dér er scoped = pr. HTTP-request, hvilket er den levetid EF er designet til. Det er en af de konkrete gevinster ved at registrere via interfaces fra start (Fase1-Scope, "billig insurance").

### 7.5 Forberedelse til autorisation (F-03), uden at bygge den

Autorisation er udskudt, men F-03 kræver at den ender i Application-laget. Der er to måder at få brugerkontekst ind i en service: som **parameter** eller som **injiceret afhængighed**. Vælger vi parameter, skal hver eneste servicesignatur ændres den dag Identity kommer — altså præcis den slags dyre ændring Fase1-Scope siger skal undgås.

Anbefaling: **injicer, parametrisér ikke.** Der bygges intet auth nu, men når det kommer, tilføjes `ICurrentUser` som konstruktørafhængighed i de services der har brug for den. Ingen af signaturerne i afsnit 3 skal ændres. Det koster nul at beslutte nu.

---

## 8. Regelplacering

### 8.1 Placeringsprincip

| Lag | Hvad hører til |
|---|---|
| **Domain** | Invarianter og rene funktioner: er denne dato gyldig, overlapper disse to perioder, hvor mange nætter, er denne statusovergang lovlig |
| **Application (service)** | Use casen: hent, spørg domænet, orkestrér repositories, gem, oversæt til `Result`. Alt der kræver at kigge i databasen for at afgøre noget |
| **Infrastructure** | Oversættelse til SQL, skema-værn (FK, exclusion constraint, check constraints), fejlkodeoversættelse |
| **Web** | Rendering, dialoger, aktivering af knapper ud fra flag som Application har beregnet |

### 8.2 BR-110 til BR-116 — regler der i dag ligger i repos

| Id | Regel i dag | Ny placering | Hvordan |
|---|---|---|---|
| BR-110 | Kun `Available` tæller som tilgængelig — filtret i `RoomRepo.cs:220` | **Application** — `BookingService.GetAvailableRoomsAsync` | Repoet får `GetByStatusAsync(RoomStatus status)` som neutralt filter; servicen vælger `Available` og kombinerer med overlapsopslaget. `GetAllByAvailability()` udgår. |
| BR-111 | Statusfilter som in-memory-filter efter fuldt tabeltræk — `BookingRepo.cs:173` | **Infrastructure** (som query, ikke som regel) | `GetByStatusAsync` bliver `WHERE status = @p`. Reglen var aldrig en forretningsregel, den var en implementeringsdefekt. Ophører med at være en ledger-post. |
| BR-112 | Rum med bookinger må ikke slettes — kodet som `catch (SqlException 547)` i `RoomRepo.cs:208-210` | **Application** — `RoomService.DeleteAsync`, med **Infrastructure** som sidste værn | Eksplicit `HasAnyBookingsAsync` før sletning → `Result.Failure(ErrorCodes.Room.HasBookings)`. FK'en (`OnDelete.Restrict`) bevares og oversættes til samme fejlkode ved kapløb. Den brugervendte tekst ("sæt det Out of service") flytter til Web. |
| BR-113 | `Room.Validate()` før persistering, håndhævet i repoet — `RoomRepo.cs:21-23, 164-166` | **Domain** (invariant) + **Application** (oversættelse til `Result`) | Entiteten kan ikke konstrueres ugyldigt. Repoet validerer ikke — et repository der validerer, er et repository der har forretningslogik. |
| BR-114 | Booking skal eksistere før update/delete; null → `ArgumentNullException` — `BookingRepo.cs:190-196, 235-239` | **Application** (eksistens) + **guard clause** (null) | Eksistenstjekket bliver `Result.Failure(Booking.NotFound)` i servicen, hvilket samtidig fjerner det gamle læs-så-skriv over to connections. `ArgumentNullException` for null-argument bevares som guard (T-32). |
| BR-115 | `GetById` på ukendt id returnerer `null` — tre repos | **Kontrakt mellem Infrastructure og Application** | Repository-interfacene returnerer `T?` (og lyver dermed ikke længere, jf. `IBookingRepo`s `Booking GetById`). Servicen oversætter `null` til `NotFound`. Bevaret adfærd, ærlig signatur. |
| BR-116 | Navnesøgning = "indeholder" på for- ELLER efternavn — `GuestRepo.cs:127, 130` | **Application** — `GuestService.SearchAsync` | Konsolideres med BR-68's semantik: alle søgeord skal findes i navn, land eller e-mail (AND). Tokeniseringen sker i servicen, matchningen i repoet efter en kontrakt der står i XML-doc og pinnes af en test. **Adfærdsændring** — kræver godkendelse, se afsnit 10, punkt 6. |

### 8.3 BR-117 til BR-126 — regler der i dag ligger i converters og views

| Id | Regel i dag | Ny placering | Hvordan |
|---|---|---|---|
| BR-117 | Bredde = antal overnatninger `(EndDate − StartDate).Days` — `CalendarConverters.cs:149` | **Domain** | Allerede `Booking.NumberOfNights`; DTO'en bærer `Nights`. Converteren regnede det samme en ekstra gang. |
| BR-118 | Halvdags-regel: tegnes fra `daysOffset + 0.5` — `CalendarConverters.cs:31, 48` | **Web** (præsentationsgeometri) | Selve 0,5-forskydningen er pixelmatematik og hører i kalenderkomponenten. Men den koder en forretningsantagelse — "værelset er optaget fra middag" — som `design:domain` bør skrive ned som en policy-konstant hvis den skal være en regel. Uden det er den kun en tegning. Se afsnit 10, punkt 8. |
| BR-119 | Bookinger startet før perioden klippes til periodestart, `+0.5` dag på bredden — `CalendarConverters.cs:81, 139` | **Web** (præsentationsgeometri) | Datavinduet leveres af `GetOverviewAsync` (BR-24); afklipningen er ren rendering. |
| BR-120 | Tidslinjen viser kun bookinger hvor `Booking.Room.RoomId` matcher; bookinger uden rum udelades — `CalendarConverters.cs:197` | **Infrastructure** (skema) + **Application** (query) | `room_id` er `NOT NULL` med FK, så "booking uden rum" bliver umulig — halvdelen af reglen forsvinder ved skemaet. Grupperingen pr. rum sker i oversigts-queryen; DTO'en bærer `RoomId`. |
| BR-121 | Confirm-knap kun ved `Pending` (XAML-trigger, dublet af BR-02) | **Application** — `BookingActionsDto.CanConfirm` + `ConfirmAsync` | Ét sted i stedet for to. UI'et binder til et bool-flag og kender ingen statusser. |
| BR-122 | Cancel-knap kun ved `Pending`/`Confirmed` (dublet af BR-11) | **Application** — `BookingActionsDto.CanCancel` + `CancelAsync` | Samme. |
| BR-123 | Edit-knap skjult ved `CheckedIn`/`CheckedOut` (dublet af BR-14) | **Application** — `BookingActionsDto.CanEdit` + `RescheduleAsync` | Samme. |
| BR-124 | Gæstefelter skrivebeskyttede uden for redigeringstilstand — `IsReadOnly = !IsEditing` | **Web** (ren UI-tilstand) | Ingen serviceregel. Den reelle beskyttelse er at `GuestService.UpdateAsync` er den eneste skrivevej og validerer uanset hvad UI'et gjorde. |
| BR-125 | "Edit Guest" kræver valgt gæst; "New Booking" har ingen tilsvarende guard | **Web** (knap-aktivering) + **Application** (guard) | Uoverensstemmelsen dør ved typerne: `UpdateGuestRequest.GuestId` og `CreateBookingRequest` kan ikke udfyldes uden en gæst, og servicen returnerer `NotFound` hvis id'et ikke findes. Det er ikke længere muligt at have guard ét sted og ikke det andet. |
| BR-126 | Rumstatusser hardkodet i XAML (Available / Maintanance / Cleaning / Disabled) og uenige med `RoomStatus`-enum | **Domain** (enum = eneste kilde) + **Application** (`GetFilterOptionsAsync`) | UI'et binder til listen fra Application; en hardkodet dropdown kan ikke længere komme ud af trit. Selve forløbet (F-01, daglig rengøring/service/slutrengøring) fastlægges af `design:domain`; `RoomService.ChangeHousekeepingStatusAsync` er fladen. |

### 8.4 Regler der håndhæves i service-laget — samlet oversigt

Regler der ligger i **Application**, altså ikke i Domain, ikke i UI, ikke i databasen:

| Service | BR-id'er |
|---|---|
| `BookingService` | BR-01, BR-03, BR-05, BR-08, BR-13, BR-17, BR-20, BR-22, BR-23, BR-24, BR-25, BR-32, BR-33*, BR-34, BR-36, BR-37, BR-38, BR-39, BR-40, BR-45, BR-46, BR-47, BR-48, BR-49, BR-51, BR-110, BR-111, BR-114, BR-115, BR-120, BR-121, BR-122, BR-123 |
| `RoomService` | BR-74, BR-75, BR-76, BR-78, BR-80*, BR-82, BR-84, BR-112, BR-113, BR-115, BR-126 |
| `GuestService` | BR-58, BR-59, BR-60, BR-62, BR-65*, BR-66, BR-67*, BR-68, BR-69, BR-70, BR-71*, BR-72, BR-73*, BR-115, BR-116, BR-125 |

\* Disse er delvis UI-adfærd (genindlæsning, valgt-element-guards). Servicen bærer den håndhævende halvdel — at handlingen afvises med `NotFound` hvis id'et mangler — mens knappernes aktivering er Web.

Regler der **ikke** ligger i service-laget, og hvor de i stedet ligger:

- **Domain:** BR-02, BR-06, BR-09, BR-11, BR-14, BR-18, BR-19, BR-41–BR-44, BR-52–BR-57, BR-88–BR-109, BR-117.
- **Web (ren præsentation eller bekræftelsesdialoger):** BR-04, BR-12, BR-15, BR-16, BR-21, BR-26–BR-30, BR-50, BR-61, BR-63, BR-64, BR-77, BR-79, BR-83, BR-85, BR-86, BR-118, BR-119, BR-124.
- **Infrastructure (skemaværn):** BR-112 (FK), overlapsværnet fra B-03, BR-120's "ingen booking uden rum".
- **Ophører:** BR-31 (omsætning er aldrig implementeret; ingen priser i domænet), BR-87 (WPF-`RelayCommand`-detalje uden modstykke i Blazor), BR-111 (defekt, ikke regel).

---

## 9. Testprojekter

Udgangspunktet er ikke behageligt: 40 af de 128 gamle tests rammer den **samme fysiske database som applikationen** (`HotelBooking`), med hardkodede nøgler som `"999"` og `TESTAVAIL`, `Parallelize(MethodLevel)` slået til, ujævn oprydning pakket i `catch {}`, og en `appsettings.json` der slet ikke er i repoet — så suiten er 0 % kørbar out-of-the-box. Det skal ikke porteres, det skal erstattes.

### 9.1 Fordeling

| Projekt | Tester | Afhængigheder | Kørselstid |
|---|---|---|---|
| `NFHotel.Domain.Tests` | Invarianter, datoregler, overlapsfunktionen, statusovergange, `NumberOfNights`, `BookingNumber` | Ingen | Millisekunder |
| `NFHotel.Application.Tests` | Use cases: services med mockede repositories og fake `IClock` | Mocks | Millisekunder |
| `NFHotel.Infrastructure.Tests` | Skema, mapping, queries, constraints | Testcontainers `postgres:16` | Sekunder |

### 9.2 `Application.Tests` — det der erstatter de 22 ViewModel-tests

De 22 tests i `BookingOverviewViewModelTests` er ifølge analysen den eneste rigtige facit-kilde og den eneste fil med ægte mocks. **T-20 til T-31 porteres 1:1 som service-tests** — samme scenarier, ny modtager: `BookingService` i stedet for `BookingOverviewViewModel`. Det er selve beviset for at forretningslogikken faktisk flyttede.

Hvad der testes her:

- Alle statusovergange og deres afvisninger (T-20 til T-27), asserteret på **fejlkode**, ikke på fejltekst — tekster er UI's, koder er kontrakten.
- Overlapsafvisning ved både `CreateAsync` og `RescheduleAsync` (B-03), med mocket repository der returnerer kolliderende kandidater. Dette er ny dækning; det gamle system havde ingen.
- BR-47's to grene: eksisterende gæst genbruges, ny gæst oprettes — og at `SaveChangesAsync` kaldes **én gang** (verificér på mocket `IUnitOfWork`). Det er testen for atomiciteten.
- BR-60/BR-96: at alle valideringsfejl kommer med, ikke kun den første.
- Filtrering og sortering (T-28, T-29), inklusive at periodefilteret nu gælder alle visninger.
- `BookingActionsDto`-flagene, som er den enkeltting UI-laget kommer til at stole blindt på.

To disciplinkrav: **`IClock` mockes altid** — BR-06, BR-44 og BR-104 handler alle om "i dag", og en test der bruger den rigtige systemtid, holder op med at virke i morgen (det gamle projekt havde præcis det problem indbygget). Og **`Verify(...)` bruges faktisk** — det gamle projekt havde Moq med og brugte mocks udelukkende som stubs, uden en eneste `Verify`.

Desuden en **arkitekturtest** (NetArchTest eller tilsvarende) i dette projekt: `NFHotel.Application` må ikke have typer der refererer `Microsoft.EntityFrameworkCore` eller `Npgsql`. Lagdeling håndhævet af build, ikke af hukommelse.

### 9.3 `Infrastructure.Tests` — Testcontainers, ikke delt database

Én `postgres:16`-container pr. test-collection, startet i en xUnit `ICollectionFixture`. Hver test kører i sin egen transaktion der rulles tilbage, eller mod et skema der er nulstillet med Respawn. **Ingen test rammer nogen udviklers eller nogens delte database, og ingen test har hardkodede nøgler.** Det er den direkte modgift mod det gamle setups tre problemer på én gang: delt DB, hardkodede id'er og parallelitet.

Hvad der hører hjemme her og ingen andre steder:

1. **Migrationerne kan køre fra bunden**, og `Down` virker.
2. **Snake_case slår igennem** (B-06): tabellen hedder `booking`, kolonnen `start_date`.
3. **Enum-mapping** (B-07): `BookingStatus.Cancelled` lander som `4` i kolonnen. Assertes med rå SQL, ikke gennem EF — ellers tester man EF mod sig selv.
4. **Datotyper** (B-08): `DateOnly` → `date`, `DateTimeOffset` → `timestamptz`, og en tur frem og tilbage bevarer UTC.
5. **Kryptering** (B-09): pasnummer læses korrekt tilbage gennem EF, **og** en rå SQL-læsning af kolonnen viser noget der ikke er klarteksten. Plus at `null` forbliver `NULL` (T-41) og ikke bliver til krypteret tom streng.
6. **Exclusion constrainten virker under samtidighed** (B-03): to parallelle transaktioner der begge indsætter overlappende bookinger på samme rum — præcis én skal committe, den anden skal fejle med `23P01`, og oversættelsen til `BookingOverlapConflictException` skal ske. Det er den vigtigste enkelttest i hele projektet, fordi den er den eneste der beviser at dobbeltbooking ikke kan ske.
7. **Query-semantik:** gæstesøgningens AND-logik (T-40 udvidet), rumfilterets ekskluderende kriterier (T-38, T-39), eager loading af `Room` og `Guest` (T-35).
8. **Sletteguards:** FK'en afviser sletning af et rum med bookinger (T-36's efterfølger, BR-112).

### 9.4 Hvad der ikke porteres

De ca. 45 boilerplate-tests fra analysen: property get/set, "er den ikke-null", tre identiske `RoomSize_Accepts*`, roundtrip-tests der kun tester ADO.NET-mapping. `Conventions.md` siger det direkte: *"Undgå at unit-teste simpel boilerplate."*

De 46 modeltests (T-01 til T-19) porteres derimod til `Domain.Tests` — de er hurtige, rene og koder valideringskontrakten. Det er `design:domain`s område, men de er værd at nævne her fordi de i dag ikke kan køre: assembly-init kaster `FileNotFoundException` og fælder hele assemblyen, også de rene tests. Med lagdelte testprojekter kan Domain- og Application-testene køre uden Docker, uden database og uden konfiguration.

### 9.5 Framework

**Anbefaling: xUnit** i alle tre projekter, med **NSubstitute eller Moq** til mocks. xUnit har den bedste Testcontainers-integration (`IAsyncLifetime`), og der er reelt ikke noget at porte: de tests der er værd at beholde, skal alligevel omskrives til nye typer. Målramme `net8.0` (ikke `net8.0-windows` — den gamle suite var Windows-låst udelukkende fordi den refererede WPF-projektet).

Navngivning per Conventions: `CheckInAsync_ShouldFail_WhenBookingIsAlreadyCheckedIn()`. Arrange/Act/Assert konsekvent — den disciplin var faktisk høj i det gamle projekt og er værd at tage med.

---

## 10. Uklart og modstridende

1. **Feature-først mod type-først i `Application`.** `Conventions.md` kræver feature-først (`Features/Orders/Create/`); refaktoreringsplanens afsnit 3 tegner `Interfaces/`, `Services/`, `DTOs/`. Jeg har valgt feature-først, fordi Conventions er udpeget som source of truth. Beslutningen skal bekræftes, og `Architecture.md` skal så rette målarkitektur-tegningen — ellers står to dokumenter og modsiger hinanden fra dag ét.

2. **Conventions placerer Entity↔DTO-mapping i Infrastructure.** Afsnittet "SOC → Infrastructure" siger `Mapping (Entity ↔ DTO)`, mens `Fase1-Scope.md` under "billig insurance" kræver at DTO'er defineres i `Application`, ikke i `Web`, så et senere Api kan genbruge dem. De to kan ikke begge holde: ligger DTO'en i Application og mapperen i Infrastructure, får Application-servicen ikke fat i mapperen uden at vende afhængighedsretningen. Jeg har valgt Fase1-Scope (mapping i Application), fordi den beslutning er nyere og scope-specifik, og fordi Infrastructures mapping-ansvar i praksis er entitet↔database, ikke entitet↔DTO. **Bør skrives ind som en rettelse til `Conventions.md`.**

3. **BR-19 mod BR-39 afgør exclusion constraintens prædikat — og valget har en konkret pris.** BR-19 (redigering) udelukker både `Cancelled` og `CheckedOut` fra overlapstjekket; BR-39 (oprettelse) udelukker kun `Cancelled`. `design:domain` vælger ét sæt. Konsekvensen skal med i beslutningen: vælges BR-39's sæt, spærrer databasen for **genudlejning efter tidlig udtjekning** — booking 1.–10. januar, gæsten checker ud den 3., og en ny booking 4.–8. januar vil blive afvist af constrainten, selv om rummet står tomt. Vælges BR-19's sæt, kan der omvendt oprettes en overlappende booking oven i en udtjekket booking hvis periode endnu ikke er udløbet. Den ordentlige løsning er sandsynligvis at **`CheckOutAsync` afkorter bookingens periode til den faktiske udtjekningsdato**, hvorefter BR-39's strammere sæt kan bruges uden bivirkning — men det er en ny domæneregel og dermed `design:domain`s kald. Uanset valget skal prædikatet i migrationen genereres fra én konstant i Domain, og en Infrastructure-test skal asserte at de to er enige.

4. **`RoomSize`s type er ikke fastlagt.** `Fase1-Scope.md` anbefaler enum eller lookup frem for fri streng, men det står ikke i listen over typer jeg må antage. Jeg har skrevet `RoomSize` som enum i `RoomFilter`, DTO'er og requests. Bliver den en string, ændres fire signaturer plus én EF-konfigurationslinje. Bliver den en lookup-tabel, ændres desuden `GetFilterOptionsAsync` til et DB-opslag.

5. **Unikt rumnummer er en ny regel.** Det gamle skema havde ingen unique constraint på `RoomNumber`, og ingen af de 126 regler kræver det. Jeg har foreslået `ux_room_room_number` plus `RoomNumberExistsAsync`, fordi to rum med samme nummer er meningsløst — men det er en tilføjelse, ikke en bevaret regel, og Fase 3's ledger-audit vil registrere den som "opfundet" hvis den ikke godkendes eksplicit. Samme gælder `GuestService.DeleteAsync` (D-05) og check constraints på `floor > 0` / `capacity > 0`.

6. **Gæstesøgningen ændrer adfærd.** BR-116 (repo: for- eller efternavn) og BR-68 (ViewModel: alle ord i navn, land eller e-mail) er to forskellige søgninger. Konsolideringen til BR-68's semantik betyder at en søgning på et efternavn nu også rammer et land med samme navn. Det er en forbedring, men det er en ændring, og den skal godkendes frem for at glide igennem.

7. **`SalesService` står i målarkitekturen, men der er intet at beregne.** BR-31 fastslår at omsætningen altid sættes til 0, og der findes ingen pris nogen steder i domænet — `Room.RoomSize` er fri tekst, ikke en pris. Jeg har udeladt servicen. Alternativet er en service der returnerer 0 og ligner en implementering. Sales-skærmen bør enten udgå af Fase 1 eller vises som en eksplicit placeholder. Prisspørgsmålet (åbent spørgsmål 3 i planen) er ikke besvaret nogen steder.

8. **BR-118's "optaget fra middag" er en forretningsantagelse forklædt som pixelmatematik.** Jeg har placeret geometrien i Web, men hvis hotellet reelt har en check-in-tid og en check-out-tid, er det en domænepolitik der også burde gælde check-in-vinduet (BR-06 tillader i dag check-in på hele startdatoen). Bør afklares af `design:domain`; ellers er 0,5 i converteren det eneste sted den viden findes — igen.

9. **Krypteringsnøgle og rotation er ikke løst.** D-07 er selv markeret som midlertidig og uafklaret (Actions Secrets mod filer i repoet). Fase 1 kan levere `EncryptionOptions` med `ValidateOnStart`, så appen nægter at starte uden nøgle, men nøglerotation kræver en backfill-migration der læser, dekrypterer og genkrypterer hele `guest`-tabellen. Det er ikke designet her. Ligeledes er D-05's to retention-spor (bookingdata efter X måneder, persondata i X år) ikke designet — X'erne findes ikke.

10. **Primærnøgle-navngivning skal fastlægges.** `Conventions.md` siger "Id eller [Entity]Id — vælg én standard", og det gamle system havde `BookingID`, `GuestID` og `RoomId` side om side (U-19). Jeg har antaget `BookingId`, `RoomId`, `GuestId` i C#, hvilket bliver `booking_id`, `room_id`, `guest_id` i databasen. Det er `design:domain`s properties, så valget skal bekræftes dér — men det rammer hver eneste EF-konfiguration og migration, så det skal bekræftes **inden** den første migration genereres.

11. **Blazor Server-scopet skal koordineres.** Mit DI-design forudsætter at Web-laget giver hver side sin egen DI-scope. Gør det ikke det, får en bruger én `DbContext` for hele sin session, og to samtidige komponent-hændelser vil kaste. Det skal stå som et krav i `Architecture.md` og aftales med `design:web`, ikke opdages i drift.

12. **Ingen datamigrering fra det gamle system er forudsat.** Planen siger at testdata porteres til EF seed-data, og at de to T-SQL-sæt ikke kan køres mod PostgreSQL. Jeg har designet ud fra en tom database. Skal produktionsdata faktisk flyttes over, kommer der en engangsopgave oveni: eksisterende pasnumre skal krypteres ved indlæsning, og eksisterende bookinger skal kunne overholde exclusion constrainten — hvis de overlapper i dag (og det kan de godt, for der har aldrig været noget tjek), kan constrainten ikke oprettes før dataene er ryddet op. **Det bør verificeres mod den kørende database før migrationen skrives**, ikke bagefter.

---

### Kritiske filer for implementering

Læste kildedokumenter der styrer implementeringen:

- `/mnt/user-data/uploads/Development/Conventions.md`
- `/mnt/user-data/uploads/Development/New folder/NFHotel/docs/BusinessRules.md`
- `/mnt/user-data/uploads/Development/New folder/NFHotel/docs/Analysis.md`
- `/mnt/user-data/uploads/Development/New folder/NFHotel/docs/Fase1-Scope.md`
- `/mnt/user-data/uploads/Development/New folder/NFHotel-Refactor-Plan.md`

De fem filer der skal skrives først, i denne rækkefølge (planlagte stier under solution-roden):

1. `src/NFHotel.Application/Common/Results/Result.cs` — alt andet i Application afhænger af returtypen.
2. `src/NFHotel.Application/Bookings/IBookingRepository.cs` — definerer kontrakten som både `BookingService` og `BookingRepository` bygges imod.
3. `src/NFHotel.Application/Bookings/BookingService.cs` — bærer flest regler og er den metrik Fase 3's ledger-audit måler op imod.
4. `src/NFHotel.Infrastructure/Persistence/Configurations/BookingConfiguration.cs` — B-03, B-06, B-07 og B-08 mødes her, og migrationen genereres ud fra den.
5. `src/NFHotel.Infrastructure/DependencyInjection.cs` — levetider, options-validering og `DbContext`-fabrikken, altså det der afgør om Blazor Server-opsætningen holder.