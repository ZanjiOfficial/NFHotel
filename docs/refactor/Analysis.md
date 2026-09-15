# NFHotel — Analyse (Fase 0)

> Genereret 2026-09-02 af 5 parallelle read-only analyse-agenter.
> Kilde: `4. Semester\NFHotel-Hotel-main\` (uændret).
> Ingen kode er skrevet. Ingen filer i kildeprojektet er ændret.

---

## Sammenfatning

- **90 forretningsregler** blev fundet i ViewModel-laget alene; ledgeren i `BusinessRules.md` tæller **126** når model-validering, converter-regler, repo-regler og testbeviste regler lægges til.
- **Al forretningslogik ligger i `BookingOverviewViewModel.cs`** (BR-02 til BR-33) — statusovergange, overlapstjek, filtrering, sortering og kalenderberegning i én klasse.
- **To forskellige overlap-regler eksisterer side om side:** ved oprettelse ekskluderes kun `Cancelled` fra overlapstjekket, ved redigering ekskluderes både `Cancelled` og `CheckedOut`. Samme rum/periode kan afvises ét sted og accepteres et andet.
- **Der er intet overlapstjek ved selve oprettelsen** (`CreateBooking`) — kun ved listefiltreringen forinden. Dobbeltbooking er mulig hvis datoer ændres efter rumvalg.
- **Omsætning beregnes aldrig.** `UpdateRevenue()` sætter altid 0. Der findes ingen pris nogen steder i domænet — `Room` har `RoomSize` som fri tekst, ikke en pris.
- **Check-in er tilladt direkte fra `Pending`**, hvilket modsiger bekræftelsesdialogens egen tekst ("Payment has been received"). Bekræftet af test T-21. Skal afklares før tilstandsmaskinen låses.
- **Uge- og månedsvisning filtrerer ikke på periode** — kun årsvisningen gør. Ser utilsigtet ud.
- **`RoomNumber` er `string` i C# men `INT` i begge create-scripts**; kun et løst patch-script (`03_AlterRoomNumberToString.sql`, som kun findes i det ene af to SQL-sæt) retter det.
- **Der findes to indbyrdes uforenelige SQL-sæt** (`Database/` og `NFHotel/SQL/`) med samme filnavne og helt forskelligt indhold. Kører man det ene efter det andet, slettes data.
- **Ingen DI:** `DatabaseConfig` er en statisk mutérbar global; alle tre repos læser den i konstruktøren. Alt er synkront — nul `async` i hele dataadgangslaget.
- **RoomRepo bruger stored procedures, BookingRepo og GuestRepo bruger inline-SQL** uden forklaring. `GuestRepo` mapper via hardkodede kolonneindeks 0-6.
- **Forretningsregler er lækket ned i repo-laget** (fx "rum med bookinger må ikke slettes" som `catch` på SQL-fejl 547) og op i view-laget (statusknapper styret af XAML-triggere).
- **`GuestPolicyViewModel` og `SalesOverviewViewModel` er helt tomme klasser** — de to menupunkter fører til placeholder-skærme.
- **128 tests, ikke 129 som README påstår.** 40 rammer en delt live SQL Server (`HotelBooking` — samme DB som appen), og `appsettings.json` mangler i repoet, så suiten er 0 % kørbar out-of-the-box.
- **De 22 tests i `BookingOverviewViewModelTests` er den eneste rigtige facit-kilde** — de koder bookingens tilstandsmaskine med mocks og kan genbruges direkte. To positive tests (slet booking / opdater rum) ser ud til at være fjernet.

---

## 1. analyse:models

### Entiteter

#### Booking

Kilder: `NFHotel/Models/Booking.cs`, `Database/01_CreateSchema.sql:43-54`, `NFHotel/SQL/01_CreateSchema.sql:57-68`, `NFHotel/SQL/fix-booking-identity.sql:12-23`

| Felt | C#-type | SQL-type | I diagram? | Bemærkning |
|---|---|---|---|---|
| BookingID | `int` (`Booking.cs:10`) | `INT PRIMARY KEY IDENTITY(1,1)` | Ja (DCD_Domain, DCD_Architecture, ERD som 🔑) | PK. Identity manglede oprindeligt i den kørende DB — se `fix-booking-identity.sql` |
| BookingNumber | `string` read-only, `$"FLZ-{BookingID:D6}"` (`Booking.cs:13`) | — (ingen kolonne) | Nej | Rent afledt display-felt; findes hverken i SQL eller diagram |
| StartDate | `DateTime` (`Booking.cs:15`) | `DATE NOT NULL` i begge 01-scripts; `DATETIME2 NOT NULL` i `fix-booking-identity.sql:14` | Ja (alle 3 diagrammer) | Typen afhænger af hvilket script der er kørt sidst |
| EndDate | `DateTime` (`Booking.cs:16`) | `DATE NOT NULL` / `DATETIME2 NOT NULL` (fix-scriptet) | Ja | Samme typekonflikt som StartDate |
| CheckInTime | `DateTime?` (`Booking.cs:17`) | `DATETIME2 NULL` | Ja | Konsistent |
| CheckOutTime | `DateTime?` (`Booking.cs:18`) | `DATETIME2 NULL` | Ja | Konsistent |
| Status | `BookingStatus` (enum) (`Booking.cs:19`) | `INT NOT NULL DEFAULT 0` (i fix-scriptet: `INT NOT NULL` uden default) | Ja i DCD_Domain/DCD_Architecture, **nej** i ERD_DB | Lagres som ordinalværdi; ingen CHECK-constraint |
| RoomID | `int` (`Booking.cs:22`) | `INT NOT NULL`, FK → `ROOM(RoomID)` | Kun i ERD_DB (🗝️); ikke som felt i DCD'erne | DCD'erne viser relationen som association, ikke som FK-felt |
| GuestID | `int` (`Booking.cs:23`) | `INT NOT NULL`, FK → `GUEST(GuestID)` | Kun i ERD_DB (🗝️) | Samme som ovenfor |
| Room | `Room` (navigation) (`Booking.cs:25`) | — | Association `Booking "0..*" --> "1" Room` | Objektreference, ingen kolonne |
| Guest | `Guest` (navigation) (`Booking.cs:26`) | — | Association `Booking "0..*" --> "1" Guest` | Objektreference, ingen kolonne |
| NumberOfNights | `int` beregnet (`Booking.cs:30-39`) | — | Ja (DCD_Domain + DCD_Architecture) | `(EndDate - StartDate).Days`, 0 hvis en dato er `default` |

Indekser på BOOKING (begge 01-scripts): `IX_Booking_StartDate`, `IX_Booking_EndDate`, `IX_Booking_Status`, `IX_Booking_RoomID`, `IX_Booking_GuestID`. Disse forsvinder hvis `fix-booking-identity.sql` køres (tabellen droppes og genskabes uden indekser og uden navngivne FK-constraints).

#### Guest

Kilder: `NFHotel/Models/Guest.cs`, `Database/01_CreateSchema.sql:29-37`, `NFHotel/SQL/01_CreateSchema.sql:43-51`

| Felt | C#-type | SQL-type | I diagram? | Bemærkning |
|---|---|---|---|---|
| GuestID | `int` (`Guest.cs:16`) | `INT PRIMARY KEY IDENTITY(1,1)` | Ja (alle 3) | PK |
| FirstName | `string` (`Guest.cs:17`) | `NVARCHAR(100) NOT NULL` | Ja | C# har ingen længdebegrænsning |
| LastName | `string` (`Guest.cs:18`) | `NVARCHAR(100) NOT NULL` | Ja | — |
| Email | `string` (`Guest.cs:19`) | `NVARCHAR(100) NOT NULL` | Ja | Ingen UNIQUE-constraint i SQL |
| PhoneNumber | `string` (`Guest.cs:20`) | `NVARCHAR(50) NOT NULL` | Ja | — |
| Country | `string` (`Guest.cs:21`) | `NVARCHAR(50) NOT NULL` | Ja | — |
| PassportNumber | `string` (`Guest.cs:22`) | `NVARCHAR(50) NULL` | Ja | Eneste nullable Guest-kolonne; valideres heller ikke i C# |

Kolonnerækkefølge i SQL: GuestID, FirstName, LastName, PassportNumber, Email, Country, PhoneNumber — ERD_DB følger denne rækkefølge, mens C#/DCD har PassportNumber til sidst.

#### Room

Kilder: `NFHotel/Models/Room.cs`, `Database/01_CreateSchema.sql:16-23`, `NFHotel/SQL/01_CreateSchema.sql:30-37`, `NFHotel/SQL/03_AlterRoomNumberToString.sql`

| Felt | C#-type | SQL-type | I diagram? | Bemærkning |
|---|---|---|---|---|
| RoomId | `int` (`Room.cs:15`) | `RoomID INT PRIMARY KEY IDENTITY(1,1)` | Ja (DCD som `RoomId`, ERD som `RoomID`) | Casing afviger: C# `RoomId` vs. SQL/ERD `RoomID` (modsat Booking/Guest hvor C# bruger `ID`) |
| RoomNumber | `string` (default `""`) (`Room.cs:16`) | `INT NOT NULL` i begge 01-scripts; ændres til `NVARCHAR(20) NOT NULL` af `03_AlterRoomNumberToString.sql` | Ja — DCD siger `string`, ERD utypet | Kernekonflikt: create-scriptene er aldrig rettet, kun patchet bagefter |
| Floor | `int` (`Room.cs:17`) | `INT NOT NULL` | Ja | C# kræver > 0; SQL har ingen CHECK |
| RoomSize | `string` (default `""`) (`Room.cs:18`) | `NVARCHAR(20) NOT NULL` | Ja | Fri tekst ('Single'/'Double'/'Suite' i testdata), ikke enum/lookup |
| Capacity | `int` (`Room.cs:19`) | `INT NOT NULL` | Ja | C# kræver > 0; ingen CHECK i SQL |
| Status | `RoomStatus`, default `Available` (`Room.cs:20`) | `INT NOT NULL DEFAULT 0` | Ja i DCD_Domain/DCD_Architecture, **nej** i ERD_DB | Lagres som ordinal |

### Enums

#### BookingStatus (`NFHotel/Models/Enums/BookingStatus.cs:6-13`)

Ingen eksplicitte værdier, så implicit `int`-backing fra 0:

| Værdi | Tal | Kommentar i koden |
|---|---|---|
| Pending | 0 | Booking created but not confirmed |
| Confirmed | 1 | Booking confirmed by guest/payment |
| CheckedIn | 2 | Guest has checked in |
| CheckedOut | 3 | Guest has checked out |
| Cancelled | 4 | Booking cancelled |

Lagring: `BOOKING.Status INT NOT NULL DEFAULT 0`, med samme mapping dokumenteret i kommentar i `Database/01_CreateSchema.sql:49`, `NFHotel/SQL/01_CreateSchema.sql:63` og i `Database/README.md:64`. Testdata bruger 0–4 direkte. Der er ingen CHECK-constraint eller lookup-tabel, så en vilkårlig int kan indsættes.

#### RoomStatus (`NFHotel/Models/Enums/RoomStatus.cs:9-14`)

| Værdi | Tal |
|---|---|
| Available | 0 |
| OutOfService | 1 |
| Maintenance | 2 |

Lagring: `ROOM.Status INT NOT NULL DEFAULT 0`, mapping i kommentar i begge 01-scripts og i `Database/README.md:63`. `Database/02_InsertTestData.sql:14` bruger værdi 1 for ét rum; `NFHotel/SQL/02_InsertTestData.sql` bruger kun 0. Ingen CHECK-constraint.

Bemærk: filerne ligger i `Models/Enums`, men namespace er `NFHotel.Models` — mappe og namespace følges ikke ad.

### SQL-scripts

Sæt A = `Database/` (Azure-sættet), Sæt B = `NFHotel/SQL/` (projekt-/lokalsættet).

| # | Fil | Sæt | Formål |
|---|---|---|---|
| 1 | `Database/01_CreateSchema.sql` | A | Dropper og opretter ROOM, GUEST, BOOKING med PK/FK samt 5 indekser på BOOKING. Antager at databasen `HotelBooking` allerede findes på Azure. |
| 2 | `Database/02_InsertTestData.sql` | A | Indsætter fast testdata: 8 rum, 5 gæster, 7 bookinger (december 2024) med statusværdier 0–4. |
| 3 | `Database/03_InsertExtendedTestData.sql` | A | Udvider datasættet: 15 ekstra gæster (i alt 20) og ~99 bookinger fordelt over dec. 2025 – jan. 2028. Forudsætter at script 1+2 er kørt. |
| 4 | `NFHotel/SQL/01_CreateSchema.sql` | B | Samme skema som #1, men opretter først databasen `HotelBooking` hvis den ikke findes. Ellers identisk. |
| 5 | `NFHotel/SQL/02_InsertTestData.sql` | B | Helt andet indhold end #2: rydder alle tre tabeller (DELETE + RESEED), indsætter 10 rum og 100 gæster og genererer via cursor tilfældige bookinger pr. rum 5 år frem. |
| 6 | `NFHotel/SQL/03_AlterRoomNumberToString.sql` | B | Patch: `ROOM.RoomNumber` fra `INT` til `NVARCHAR(20) NOT NULL`. |
| 7 | `NFHotel/SQL/fix-booking-identity.sql` | B | Patch (2025-12-13): genskaber BOOKING med `IDENTITY(1,1)`. Sætter Start/EndDate til `DATETIME2`; genskaber ikke indekser eller navngivne FK-constraints. |
| 8 | `NFHotel/SQL/generate room storedprocedures.sql` | B | Opretter `uspGetAllRooms`, `uspGetRoomById`, `uspGetRoomsFromCriteria`, `uspUpdateRoom`, `uspDeleteRoom`. |
| 9 | `NFHotel/SQL/uspCreateRoom StoredProcedure.sql` | B | Opretter `uspCreateRoom`. Ligger separat fra de øvrige room-procedures. |

Overordnet: A er Azure-orienteret, tre trin, ingen stored procedures, ingen patches. B er lokal-orienteret, ét (helt andet) datascript, begge patch-scripts og alle stored procedures. Kun ROOM har stored procedures — GUEST og BOOKING har ingen.

### Uoverensstemmelser

**U-01 — RoomNumber: int vs. string.** `Room.cs:16` har `string`; begge `01_CreateSchema.sql` opretter `INT`. Kun patchet i sæt B. Azure-sættet har INT.

**U-02 — Testdata indsætter RoomNumber som tal.** `(101, 1, 'Single', ...)` uden apostroffer i begge 02-scripts.

**U-03 — uspUpdateRoom bruger stadig INT for RoomNumber.** `generate room storedprocedures.sql:35` erklærer `@RoomNumber INT`, mens `uspCreateRoom StoredProcedure.sql:4` bruger `NVARCHAR(50)`.

**U-04 — RoomSize-længde afviger.** Kolonnen er `NVARCHAR(20)`; `uspCreateRoom` tager `NVARCHAR(50)`.

**U-05 — StartDate/EndDate: DATE vs. DATETIME2.** Begge 01-scripts bruger `DATE`; `fix-booking-identity.sql:14-15` genskaber dem som `DATETIME2`.

**U-06 — BookingID IDENTITY.** 01-scriptet har IDENTITY, men fix-scriptet beskriver at BookingID i praksis ikke auto-inkrementerede. Den kørende DB har afveget fra det checkede-ind skema.

**U-07 — Indekser og navngivne FK-constraints tabes** ved `fix-booking-identity.sql:32-33`.

**U-08 — ERD_DB.md mangler Status-kolonnen** på både ROOM og BOOKING.

**U-09 — ERD_DB.md er syntaktisk ufuldstændig.** Mermaid-blokken lukkes aldrig; kardinaliteter mangler.

**U-10 — DCD-diagrammerne mangler FK-felterne** `RoomID`/`GuestID` på Booking.

**U-11 — BookingNumber findes ingen steder ud over koden.**

**U-12 — DCD_Domain viser `Validate()` men ikke `ValidateEdit()`;** DCD_Architecture udelader `Validate()` helt.

**U-13 — Sekvensdiagrammet bruger en Booking-konstruktør der ikke findes** (`SD_UC01_RegisterBooking.md:22`). `Booking.cs` har ingen erklærede konstruktører.

**U-14 — Sekvensdiagrammet antager samlet INSERT af Booking + Guest;** der findes ingen stored procedure for BOOKING eller GUEST.

**U-15 — De to 02-scripts er indbyrdes uforenelige.** Samme filnavn, helt forskelligt indhold. Kører man B efter A, slettes A's data.

**U-16 — Rumbestand afviger mellem sættene.** A: 8 rum over 3 etager. B: 10 rum over 2 etager. `03_InsertExtendedTestData.sql` forudsætter A.

**U-17 — Kolonnerækkefølge i GUEST-INSERTs afviger** mellem A og B.

**U-18 — Enum-værdier har ingen håndhævelse i databasen.** Ingen CHECK-constraint; intet forhindrer værdien 7.

**U-19 — Casing på Room-PK.** C# `Room.RoomId` mod SQL/ERD `RoomID` og C# `Booking.RoomID`.

**U-20 — README beskriver et repository-API der ikke matcher DCD_Architecture** (`GetAllRooms()` vs. `GetAll()` osv.).

**U-21 — README nævner filer der ikke findes i det læste sæt** og udelader `03_InsertExtendedTestData.sql`.

**U-22 — Guest.PassportNumber:** nullable i SQL, ikke valideret i C#, men altid udfyldt i testdata.

### Validering på modellerne

| Regel | Metode/attribut | Fil:linje |
|---|---|---|
| `FirstName` må ikke være null/tom/whitespace | `Guest.Validate()` | `NFHotel/Models/Guest.cs:49-50` |
| `LastName` må ikke være null/tom/whitespace | `Guest.Validate()` | `NFHotel/Models/Guest.cs:51-52` |
| `Email` skal være udfyldt og indeholde både `@` og `.` | `Guest.Validate()` | `NFHotel/Models/Guest.cs:53-54` |
| `PhoneNumber` må ikke være tom (intet formatkrav) | `Guest.Validate()` | `NFHotel/Models/Guest.cs:55-56` |
| `Country` må ikke være tom | `Guest.Validate()` | `NFHotel/Models/Guest.cs:57-58` |
| `RoomNumber` må ikke være tom/whitespace | `Room.Validate()` | `NFHotel/Models/Room.cs:40-41` |
| `Floor` skal være > 0 | `Room.Validate()` | `NFHotel/Models/Room.cs:43-44` |
| `RoomSize` må ikke være tom (ingen whitelist) | `Room.Validate()` | `NFHotel/Models/Room.cs:46-47` |
| `Capacity` skal være > 0 | `Room.Validate()` | `NFHotel/Models/Room.cs:49-50` |
| `StartDate` må ikke være `default(DateTime)` | `Booking.Validate()` | `NFHotel/Models/Booking.cs:47-48` |
| `EndDate` må ikke være `default(DateTime)` | `Booking.Validate()` | `NFHotel/Models/Booking.cs:50-51` |
| `EndDate` skal være strengt > `StartDate` | `Booking.Validate()` | `NFHotel/Models/Booking.cs:53-54` |
| `StartDate.Date` må ikke være før i dag | `Booking.Validate()` | `NFHotel/Models/Booking.cs:56-57` |
| Fejl kun hvis `Room` er null OG `RoomID` er 0 | `Booking.Validate()` | `NFHotel/Models/Booking.cs:59-60` |
| Fejl kun hvis `Guest` er null OG `GuestID` er 0 | `Booking.Validate()` | `NFHotel/Models/Booking.cs:62-63` |
| `CheckOutTime` skal være strengt > `CheckInTime` når begge er sat | `Booking.Validate()` | `NFHotel/Models/Booking.cs:65-66` |
| `newEndDate > newStartDate` på indsendte datoer | `Booking.ValidateEdit()` | `NFHotel/Models/Booking.cs:75-76` |
| `newStartDate.Date` må ikke være før i dag | `Booking.ValidateEdit()` | `NFHotel/Models/Booking.cs:78-79` |
| Dekoreret DateTime skal være strengt > den property konstruktøren nævner | `DateGreaterThanAttribute.IsValid()` | `NFHotel/Validation/DateGreaterThanAttribute.cs:20-47` |

Bemærkninger:
- `DateGreaterThanAttribute` bruges ikke på nogen af de tre modeller — ingen DataAnnotations-attributter overhovedet.
- `IsValid` returnerer Success når værdien ikke er en `DateTime` eller sammenligningsværdien er null — fravær behandles som gyldigt.
- Ingen `Validate()`-metode kaldes fra modellerne selv; de returnerer blot `List<string>`.
- Ingen validering af `PassportNumber`, ingen længdekontrol mod NVARCHAR-grænser, intet overlapstjek i modellaget.

### Uklart/modstridende (models)

1. Hvilket SQL-sæt er kanonisk? `Database/README.md` nævner ikke `NFHotel/SQL/` med ét ord.
2. Den faktiske kolonnetype for `RoomNumber` og `StartDate`/`EndDate` kan ikke afgøres fra filerne alene.
3. Kørerækkefølge for `03_InsertExtendedTestData.sql` er udokumenteret.
4. Er `fix-booking-identity.sql` kørt eller ej? Hvorfor problemet opstod fremgår ikke.
5. `RoomSize` er de facto en enum i data, men findes hverken som C#-enum, lookup-tabel eller CHECK-constraint.
6. `Room.cs:8` importerer `NFHotel.Core` uden at bruge noget derfra.
7. `DCD_Architecture.md` tegner kun `RoomRepo` og `GuestRepo` mod `DatabaseConfig`, ikke `BookingRepo`.
8. `RoomRepo.GetAllByAvailability()` optræder i diagrammet, men ikke på `IRoomRepo`.
9. ERD_DB angiver hverken typer, nullability eller kardinaliteter.
10. `Database/README.md:18` indeholder et databasepassword i klartekst — i modstrid med filens eget sikkerhedsafsnit (`:77-91`).

---

## 2. analyse:repos

### Repo-metoder

| Repo | Metode | SQL (kort) | Returnerer | Fejlhåndtering |
|---|---|---|---|---|
| BookingRepo | `void Create(Booking)` (`BookingRepo.cs:19`) | Inline `INSERT INTO BOOKING (...) VALUES (@...); SELECT CAST(SCOPE_IDENTITY() as int)` | void — sætter `booking.BookingID` | `ArgumentNullException` ved null (`:21`). Ingen try/catch. `(int)cmd.ExecuteScalar()` unbox uden null-tjek (`:41`) |
| BookingRepo | `List<Booking> GetAll()` (`:47`) | Inline `SELECT b.*, r.*, g.* FROM BOOKING b INNER JOIN ROOM r ... INNER JOIN GUEST g ...` | `List<Booking>` med udfyldt `Room` og `Guest` | Ingen |
| BookingRepo | `Booking? GetById(int)` (`:109`) | Samme join + `WHERE b.BookingID = @BookingID` | `Booking?` — null hvis ikke fundet (`:167`) | Ingen |
| BookingRepo | `List<Booking> GetByStatus(BookingStatus)` (`:170`) | Ingen egen SQL — `GetAll()` + in-memory filter | `List<Booking>` | Ingen |
| BookingRepo | `List<Booking> GetByRoomID(int)` (`:176`) | Ingen egen SQL — `GetAll()` + in-memory filter | `List<Booking>` | Ingen |
| BookingRepo | `List<Booking> GetByGuestID(int)` (`:182`) | Ingen egen SQL — `GetAll()` + in-memory filter | `List<Booking>` | Ingen |
| BookingRepo | `void Update(Booking)` (`:188`) | Inline `UPDATE BOOKING SET ... WHERE BookingID = @BookingID` | void | `ArgumentNullException` (`:190`); forud-`GetById` + `ArgumentException` (`:195`); `InvalidOperationException` hvis rowsAffected == 0 (`:227`) |
| BookingRepo | `void Delete(int)` (`:233`) | Inline `DELETE FROM BOOKING WHERE BookingID = @BookingID` | void | Forud-`GetById` + `ArgumentException` (`:238`); `ArgumentException` hvis rowsAffected == 0 (`:251`) |
| GuestRepo | `int AddGuest(Guest)` (`GuestRepo.cs:21`) | Inline INSERT + SCOPE_IDENTITY | `int` (nyt GuestID) | Ingen. Intet null-tjek; unbox direkte (`:40`) |
| GuestRepo | `Guest? GetByID(int)` (`:48`) | Inline SELECT ... WHERE GuestID = @GuestID | `Guest?` — null hvis ikke fundet (`:79`); mappet via ordinal-index 0-6 | Ingen |
| GuestRepo | `List<Guest> GetAll()` (`:86`) | Inline SELECT (ingen WHERE, ingen ORDER BY) | `List<Guest>` | Ingen |
| GuestRepo | `List<Guest> GetAllByName(string)` (`:119`) | `WHERE FirstName LIKE @Name OR LastName LIKE @Name`, parameter `$"%{name}%"` (`:130`) | `List<Guest>` | Ingen; null-navn giver `"%%"` = alt |
| GuestRepo | `void UpdateGuest(Guest)` (`:155`) | Inline UPDATE | void | Ingen — rowsAffected ignoreres, opdatering af ikke-eksisterende gæst fejler lydløst (`:176`) |
| GuestRepo | `void DeleteGuest(int)` (`:184`) | Inline DELETE | void | Ingen — FK-violation bobler op som rå `SqlException` (`:195`) |
| RoomRepo | `void CreateRoom(Room)` (`RoomRepo.cs:19`) | SP `uspCreateRoom` | void — nyt RoomId læses ikke tilbage | `room.Validate()` → `ArgumentException` (`:21-23`) |
| RoomRepo | `List<Room> GetAll()` (`:46`) | SP `uspGetAllRooms` | `List<Room>` via `GetOrdinal` | Ingen |
| RoomRepo | `Room? GetById(int)` (`:84`) | SP `uspGetRoomById` | `Room?` — null hvis tom (`:101`) | Ingen |
| RoomRepo | `List<Room> GetRoomsFromCriteria(int?, string, RoomStatus?)` (`:120`) | SP `uspGetRoomsFromCriteria` (DBNull ved null/blank) | `List<Room>` | Ingen; filterlogik i proceduren |
| RoomRepo | `void UpdateRoom(Room)` (`:162`) | SP `uspUpdateRoom` | void | `room.Validate()` → `ArgumentException` (`:164-166`). rowsAffected ignoreres |
| RoomRepo | `void DeleteRoom(int)` (`:190`) | SP `uspDeleteRoom` | void | Eneste eksplicitte catch i hele laget: `catch (SqlException ex) when (ex.Number == 547)` → `InvalidOperationException` (`:208-211`) |
| RoomRepo | `List<Room> GetAllByAvailability()` (`:217`) | Ingen egen SQL — `GetAll()` + LINQ `Status == Available` | `List<Room>` | Ingen |
| DatabaseConfig | `static bool TestConnection()` (`DatabaseConfig.cs:23`) | Kun `connection.Open()` | `bool` | Swallow-all: `catch (Exception) { return false; }` (`:33-36`) |
| DatabaseConfig | `static void TestConnectionWithDetails()` (`:42`) | `Open()` + `Console.WriteLine` | void | Ingen catch |

### Interfaces vs. implementering

**IBookingRepo** — alle 8 metoder (`Create`, `GetAll`, `GetById`, `GetByStatus`, `GetByRoomID`, `GetByGuestID`, `Update`, `Delete`) er implementeret. Ingen ekstra public metoder. **Signaturafvigelse:** interfacet lover `Booking GetById(int)` (`IBookingRepo.cs:12`, non-nullable), klassen returnerer `Booking?` og kan give null.

**IGuestRepo** — alle 6 metoder implementeret. **Signaturafvigelse:** `Guest GetByID(int id)` (`IGuestRepo.cs:17`) vs. `Guest? GetByID(int guestId)` (`GuestRepo.cs:48`); parameternavnet afviger også (`id` vs. `guestId`), hvilket bryder named arguments.

**IRoomRepo** — alle 6 metoder implementeret. **Kun i klassen:** `GetAllByAvailability()` (`RoomRepo.cs:217`) — kaldere der programmerer mod interfacet kan ikke nå den uden cast. **Signaturafvigelse:** `string? roomSize` i interfacet (`IRoomRepo.cs:17`) vs. `string roomSize` i klassen (`RoomRepo.cs:120`).

### Connection string og DatabaseConfig

- `DatabaseConfig` er en `static class` med `public static string ConnectionString { get; set; }` (`NFHotel/Database/DatabaseConfig.cs:13-17`) — global mutable state uden `readonly`, uden validering, uden initialisering.
- Værdien sættes ét sted: `App.OnStartup` bygger en `ConfigurationBuilder` (basepath = `AppDomain.CurrentDomain.BaseDirectory`), læser `appsettings.json` (`optional: false`) og tildeler `GetConnectionString("DefaultConnection")` (`NFHotel/App.xaml.cs:18-23`).
- Ingen hardcodede credentials i de læste filer; selve strengen ligger i `appsettings.json`, som ikke var i læseområdet.
- Hver repo læser den statiske property **én gang i konstruktøren** og gemmer i `readonly string _connectionString`: `BookingRepo.cs:14-17`, `GuestRepo.cs:13-16`, `RoomRepo.cs:12-15`. En repo instantieret før `OnStartup` holder permanent null; `reloadOnChange: true` har ingen effekt.
- `TestConnection()`/`TestConnectionWithDetails()` kaldes ikke fra repos eller `App.xaml.cs`.
- Ubrugte `using System.Configuration;` i både `DatabaseConfig.cs:3` og `App.xaml.cs:1`.

### Afhængigheder (hvem new'er hvad)

- `App.OnStartup` new'er `ConfigurationBuilder` (`App.xaml.cs:18`) og skriver til det statiske `DatabaseConfig` (`:23`). **Ingen DI-container, ingen ServiceCollection, ingen registrering af repo-interfaces.**
- Alle tre repos har parameterløs konstruktør og henter selv afhængigheden fra global state (`BookingRepo.cs:14`, `GuestRepo.cs:13`, `RoomRepo.cs:12`). De kan ikke konstrueres med en anden connection string.
- ADO.NET-objekter new'es direkte pr. kald (`new SqlConnection`, `new SqlCommand`) — ingen connection-factory.
- `BookingRepo.GetAll`/`GetById` new'er selv `Room`- og `Guest`-objekter fra join'et (`:78`, `:87`, `:140`, `:149`) — bruger altså ikke `RoomRepo`/`GuestRepo`. Mapping er duplikeret.
- `RoomRepo.CreateRoom`/`UpdateRoom` kalder `room.Validate()` (`:21`, `:164`) — repoet afhænger af modelvalidering.
- Internt genbrug: `GetByStatus/GetByRoomID/GetByGuestID` → `GetAll()`; `Update`/`Delete` → `GetById()`; `GetAllByAvailability` → `GetAll()`.
- **Hvem instantierer selve repoerne kan ikke fastslås** herfra — kaldestederne ligger i ViewModels/Views (se afsnit 3).

### Sync/async, ressourcehåndtering og SQL-injection

**Synkront hele vejen** — ikke én `async`/`await` eller `...Async`-metode i de tre repos eller `DatabaseConfig`. Alle DB-kald blokerer den kaldende tråd. `Open()`: `BookingRepo.cs:25,52,113,200,243`; `GuestRepo.cs:25,52,92,124,159,188`; `RoomRepo.cs:27,52,88,126,170,196`. Ingen `CommandTimeout` sættes nogen steder.

**`using` bruges konsekvent.** Connection, command og reader er alle i `using`-blokke i samtlige metoder. Ingen lækkede ADO.NET-objekter fundet.

**SQL-injection: ingen risiko.** Ingen strenginterpolation eller konkatenering ind i SQL-tekst. Alle inline-queries bruger navngivne parametre; hele `RoomRepo` går gennem `CommandType.StoredProcedure`. Det ene sted med interpolation (`GuestRepo.cs:130`) bygger `$"%{name}%"` som **parameterværdi** — sikkert, men `%` og `_` i brugerinput escapes ikke og virker som wildcards.

**Øvrige robusthedsproblemer:**
- Ingen transaktioner. `BookingRepo.Update` (`:192` + `:198`) og `Delete` (`:235` + `:241`) laver læs-så-skriv over to separate connections — rækken kan slettes imellem.
- `AddWithValue` overalt → typeinferens, plan-cache-bloat, implicitte konverteringer.
- `(int)cmd.ExecuteScalar()` (`BookingRepo.cs:41`, `GuestRepo.cs:40`) kaster NullReferenceException/InvalidCastException i stedet for en meningsfuld fejl.
- `DatabaseConfig.TestConnection` sluger enhver exception (`:33`) — forkert password og nede server er ikke til at skelne.

### Forretningsregler fundet i repos

- **Kun ledige værelser tæller som tilgængelige:** filtret `Status == Available` ligger i dataadgangslaget — `RoomRepo.cs:220`.
- **Statusfilter på bookinger** som in-memory-filter efter fuldt tabeltræk — `BookingRepo.cs:173`.
- **Et værelse med eksisterende bookinger må ikke slettes** (skal i stedet sættes Out of service) — reglen inkl. brugervendt tekst er kodet i catch af FK-violation 547 — `RoomRepo.cs:208-210`.
- **Værelse skal være validt før persistering** — `RoomRepo.cs:21-23` og `:164-166`.
- **En booking skal eksistere før den må opdateres/slettes** — `BookingRepo.cs:192-196` og `:235-239`.
- **Navnesøgning på gæster er "indeholder" på for- eller efternavn** (wildcards begge sider, OR mellem kolonner) — `GuestRepo.cs:127,130`.
- **Ikke fundet:** der er **intet overlap-tjek** nogen steder i `BookingRepo` — hverken i `Create` eller `Update`.

### Uklart/modstridende (repos)

- **App.config vs. appsettings.json:** XML-doc'en siger "hentes fra App.config" (`DatabaseConfig.cs:9`), men kilden er `appsettings.json` (`App.xaml.cs:20-23`). Ubrugte `using System.Configuration;` begge steder tyder på et halvfærdigt skift.
- **Nullability er inkonsistent mellem interface og klasse** i alle tre repos.
- **`ConnectionString` er `string` (ikke `string?`)** men initialiseres aldrig; mangler nøglen `DefaultConnection`, sættes den lydløst til null.
- **To forskellige dataadgangsstile side om side** uden forklaring: `RoomRepo` = stored procedures, de to andre = inline-SQL. Konsekvensen er at filterlogikken i `uspGetRoomsFromCriteria` ikke er verificerbar fra C#-koden.
- **Mapping af Room og Guest findes to steder** (`BookingRepo.cs:78-96`, `:140-158` parallelt med `RoomRepo.GetAll` og `GuestRepo.GetAll`). `BookingRepo` bruger `GetOrdinal`, `GuestRepo` hardkodede indeks 0-6 — sidstnævnte brækker lydløst hvis kolonnerækkefølgen ændres.
- **Kolonnenavnskollision håndteres kun for én kolonne:** `r.Status` aliases til `RoomStatus` (`BookingRepo.cs:57`, `:117`), mens `b.RoomID`/`r.RoomID` og `b.GuestID`/`g.GuestID` selekteres uden alias. Virker kun pga. join-betingelsen.
- **`GetAllByAvailability` er efterladt uden for `IRoomRepo`;** kommentaren `// ✅ Implement interface method` på `GetAll` (`:46`) antyder manuel og ufuldendt interface-synkronisering.
- **`TestConnection`/`TestConnectionWithDetails` bruges ikke** af de læste filer; `TestConnectionWithDetails` skriver til `Console`, hvilket i en WPF-app ikke er synligt.

---

## 3. analyse:viewmodels

Den fulde BR-tabel (BR-01 til BR-90) og tilstandsdiagrammet er flyttet til `BusinessRules.md`. Nedenfor gengives agentens metodedækning og observationer.

### Metodedækning

**BookingOverviewViewModel** (`NFHotel/ViewModels/BookingOverviewViewModel.cs`)
- `BookingOverviewViewModel()` (parameterløs ctor) — ingen regler
- `BookingOverviewViewModel(IBookingRepo, IRoomRepo)` — BR-02, BR-06, BR-09, BR-11, BR-14, BR-16, BR-26, BR-28
- `LoadData()` — BR-34, BR-23, BR-31
- `RefreshData()` — ingen regler
- `ChangeMonth(int)` — BR-28, BR-29
- `UpdateDaysInMonth()` — BR-29
- `MatchesSearch(Booking)` — BR-25
- `FilterBookings()` — BR-23, BR-24, BR-25
- `UpdateRevenue()` — BR-31
- `OpenNewBooking()` — BR-33
- `SortBookings(object)` — BR-32
- `GetPropValue(object, string)` — ingen regler
- `CanExecuteConfirm()` — BR-02 · `ConfirmBooking()` — BR-03, BR-04, BR-05
- `CanExecuteCheckIn()` — BR-06 · `CheckInBooking()` — BR-07, BR-08
- `CanExecuteCheckOut()` — BR-09 · `CheckOutBooking()` — BR-10
- `CanExecuteCancel()` — BR-11 · `CancelBooking()` — BR-12, BR-13
- `CanExecuteEdit()` — BR-14 · `EnterEditMode()` — BR-15
- `SaveEdit()` — BR-17, BR-18, BR-19, BR-20 · `CancelEdit()` — BR-21
- Properties: `ViewStartDate` — BR-27; `SelectedBooking` (set) — BR-22; `SearchText` (set) — BR-26; `ViewDuration` (set) — BR-29; `CurrentMonth` (set) — BR-23, BR-31; `CurrentMonthDisplay` — BR-30; `IsEditMode` (set) — BR-16; `DayCount`, `IsViewMode`, `RevenueThisMonth`, `EditRoom`, `EditCheckInDate`, `EditCheckOutDate` — ingen regler

**NewBookingViewModel**
- ctor `(Guest?)` — BR-34, BR-35 · `LoadAllAvailableRooms()` — BR-34, BR-35
- `UpdateAvailableRooms()` — BR-37, BR-38, BR-39, BR-40 · `LoadFromGuest(Guest)` — BR-35
- `CreateBooking(object)` — BR-41 til BR-51
- `CloseWindow()` — ingen regler · `ClearForm()` — ingen regler (død kode)
- Properties: `CheckInDate`/`CheckOutDate` (set) — BR-36; øvrige — ingen regler

**NewGuestViewModel**
- ctor — BR-58, BR-59, BR-62, BR-63 · `ValidateAll()` — BR-59
- `ValidateFirstName/LastName/PhoneNumber/Email/Country()` — BR-52 til BR-56
- `UpdateCanSave()` — BR-58 · `SaveGuest()` — BR-60, BR-61, BR-62 · `Cancel()` — BR-64
- Properties: de fem felt-settere — BR-59; `PassportNumber` (set) — BR-57; øvrige — ingen regler

**GuestOverviewViewModel**
- ctor — BR-65 · `LoadGuests()` — ingen regler
- `FilterAndSortGuests()` — BR-68, BR-69, BR-70 · `MatchSearchGuest()` — BR-68
- `OnNewGuest()` — ingen regler · `OnEditGuest()` — BR-66, BR-67 · `ClearGuest()` — BR-71
- `OnBookingRequestedForGuest()` — BR-73 · `AddGuestToOverview(Guest)` — ingen regler
- `UpdateSelectedGuest(Guest)` — BR-72 · `SortGuestsByName()` — BR-69
- Properties: `SelectedGuest` (set) — BR-65; `SearchGuest` (set) — BR-68, BR-69, BR-70; øvrige — ingen regler

**RoomOverviewViewModel**
- ctor — BR-75 · `LoadRooms()` — ingen regler · `LoadFilterOptions()` — BR-75
- `ApplyFilter()` — BR-74 · `ClearFilter()` — BR-76
- `OpenCreateRoom()` — BR-81, BR-83 · `OpenEditRoom(Room)` — BR-79, BR-83
- `DeleteRoom(Room)` — BR-77, BR-78, BR-79 · `OnRoomSaved()` — BR-80 · `ClosePanel()` — ingen regler
- Properties — ingen regler

**RoomFormViewModel** — ctor — BR-81, BR-83 · `Save()` — BR-82, BR-84

**MainViewModel** — ctor — BR-85, BR-86 · `CurrentView` — ingen regler

**GuestPolicyViewModel** — ingen metoder (klassen er helt tom)

**SalesOverviewViewModel** — ingen metoder (klassen er helt tom)

**RelayCommand** — ctor — ingen regler · `CanExecute(object)` — BR-87 · `Execute(object)` — ingen regler · `RaiseCanExecuteChanged()` — ingen regler

**DateGreaterThanAttribute** — ctor — ingen regler · `IsValid(object, ValidationContext)` — BR-88, BR-89, BR-90

**ObservableObject** — `OnPropertyChanged(string)` — ingen regler

### Uklart/modstridende (viewmodels)

1. **Omsætning beregnes aldrig.** `UpdateRevenue()` sætter altid 0 (`:415-418`). BR-31 er reelt en ikke-implementeret regel.
2. **Uge- og månedsvisning filtrerer ikke på periode** — kun `ViewDuration == "Year"` filtrerer (`:389-402`). Virker utilsigtet.
3. **To forskellige overlap-regler.** Oprettelse ekskluderer kun `Cancelled` (`NewBookingViewModel.cs:204-209`); redigering ekskluderer `Cancelled` OG `CheckedOut` (`BookingOverviewViewModel.cs:768-775`).
4. **Intet overlapstjek ved selve oprettelsen.** `CreateBooking()` stoler på den filtrerede liste.
5. **Check-in fra `Pending` modsiger betydningen af Confirm** — dialogen siger "Payment has been received", men man kan checke ind uden.
6. **Redundante delbetingelser** i BR-06 og BR-09 (logisk overflødige givet statustjekkene).
7. **Redigeret gæst mister sit id.** `OnEditGuest()` kopierer seks felter men ikke `GuestID` → `UpdateGuest` på id 0.
8. **`AddGuestToOverview` opdaterer ikke `_allGuests`** — en netop oprettet gæst forsvinder ved næste søgning.
9. **Rumvalidering ignoreres.** `RoomFormViewModel.Save():58` tildeler `Room.Validate()` til `errors` og bruger den aldrig.
10. **Død kode.** `NewBookingViewModel.ClearForm()` kaldes aldrig; `GuestPolicyViewModel` og `SalesOverviewViewModel` er tomme.
11. **`DateGreaterThanAttribute` bruges ikke i ViewModel-laget** — datovalidering er håndkodet i stedet. Potentielt dobbelt-vedligeholdt logik.
12. **Søgning låser visningen permanent til år** — skiftes ikke tilbage når søgefeltet ryddes.
13. **Annullering kaldes "permanent" men er soft-delete.**
14. **Ingen øvre tidsgrænse på check-in** — en booking hvis `EndDate` for længst er passeret kan stadig checkes ind.
15. **Eksisterende gæsts ændringer gemmes ikke ved ny booking** — kun `GuestID == 0` skrives til DB.
16. **Uens fejlhåndtering ved statusskift** — Confirm/Cancel/SaveEdit viser MessageBox; CheckIn/CheckOut skriver kun til `Debug.WriteLine` (`:592`, `:639`), selvom `Status` allerede er ændret in-memory.
17. **`NumberOfNights`, `Booking.ValidateEdit`, `Guest.Validate`, `Room.Validate`** ligger i modellaget — se afsnit 1.

---

## 4. analyse:views

### 4.1 MainWindow (`NFHotel/MainWindow.xaml`)

Applikationens skal: fast venstre sidemenu med logo + 5 navigationsknapper, og et `ContentControl` der viser det aktive view. `DataContext` sættes deklarativt til `MainViewModel` (`:15-17`); code-behind indeholder kun `InitializeComponent()`.

| Handling | Command | Bundne properties |
|---|---|---|
| "Booking Overview" (default, `IsChecked=True`) | `BookingOverviewViewCommand` (`:55`) | — |
| "Guest Overview" | `GuestOverviewViewCommand` (`:65`) | — |
| "Room Overview" | `RoomOverviewViewCommand` (`:74`) | — |
| "Sales Overview" | `SalesOverviewViewCommand` (`:83`) | — |
| "Guest Policy" | `GuestPolicyViewCommand` (`:92`) | — |
| (visning af aktivt view) | — | `CurrentView` (`:109`) |

Dialoger/popups: ingen.

### 4.2 BookingOverviewView (1093 linjer)

Hovedskærmen: kalender-tidslinje (rum på Y-aksen, dage på X-aksen) med bookinger som farvede bjælker, plus højrepanel med omsætning og booking-detaljer i view/edit-tilstand. Ved `ViewDuration = Year` skiftes kalenderen ud med en sorterbar ListView (`:405-555`). Code-behind er tom.

| Handling | Command | Bundne properties |
|---|---|---|
| Opret ny booking | `NewBookingCommand` (`:60`) | — |
| Søg på gæst (løbende) | ingen — `UpdateSourceTrigger=PropertyChanged` | `SearchText` (`:96`) |
| Forrige periode | `PreviousMonthCommand` (`:131`) | `CurrentMonthDisplay` (`:138`) |
| Næste periode | `NextMonthCommand` (`:143`) | `CurrentMonthDisplay` |
| Uge-visning | `SetWeekViewCommand` (`:153`) | `ViewDuration` (`:158`) |
| Måneds-visning | `SetMonthViewCommand` (`:170`) | `ViewDuration` (`:175`) |
| Års-visning (liste) | `SetYearViewCommand` (`:186`) | `ViewDuration` (`:191, :210, :414`) |
| Klik på booking-bjælke | `SelectBookingCommand` (MouseBinding, `:363-366`) | booking-objektet som CommandParameter |
| Vælg booking i årslisten | — (SelectedItem) | `FilteredBookings`, `SelectedBooking` (`:408-409`) |
| Sortér liste (7 kolonneknapper) | `SortCommand` via `BindingProxy` (`:430,457,472,487,502,517,532`) | CommandParameter = property-sti som streng |
| Rediger booking | `EditBookingCommand` (`:950`) | `IsViewMode`, `SelectedBooking.Status` |
| Gem ændringer | `SaveEditCommand` (`:922`) | `IsEditMode`, `EditRoom`, `EditCheckInDate`, `EditCheckOutDate` |
| Annullér redigering | `CancelEditCommand` (`:934`) | `IsEditMode` |
| Bekræft booking | `ConfirmBookingCommand` (`:979`) | `SelectedBooking.Status` |
| Check ind | `CheckInBookingCommand` (`:1006`) | (kun `IsEnabled` fra CanExecute) |
| Check ud | `CheckOutBookingCommand` (`:1057`) | (kun `IsEnabled`) |
| Annullér booking | `CancelBookingCommand` (`:1028`) | `SelectedBooking.Status` |

Øvrige bindinger: `Rooms` (`:273`), `DaysInMonth` (`:246,306`), `FilteredBookings` (`:327`), `ViewStartDate`+`DayCount` (`:349-351,357-359`), `RevenueThisMonth` (`:584`), `AllRooms` (`:764`), `SelectedBooking.*`.

Dialoger: ingen. Højrepanelet er inline-detaljepanel; "Payment Status" er placeholder med "Coming soon..." (`:602`). Tom-tilstand: "Select a booking to view details." (`:1078-1088`).

### 4.3 GuestOverviewView

Master/detail over gæster. Code-behind instantierer selv sin ViewModel og kobler fire VM-events til WPF-dialoger (`GuestOverviewView.xaml.cs:26-38`).

| Handling | Command | Bundne properties |
|---|---|---|
| Søg efter gæst | ingen | `SearchGuest` (`:45`) |
| Enter i søgefelt → sortér | code-behind `SearchTextBox_KeyDown` → `vm.SortGuestsByName()` (`.xaml.cs:68-77`) | — |
| Opret ny gæst | `NewGuestCommand` (`:76`) | — |
| Vælg gæst | — (SelectedItem) | `Guests`, `SelectedGuest` (`:95-96`) |
| Rediger gæst | `EditGuestCommand` (`:183`) | `SelectedGuest` via `NotNullToBoolConverter` (`:186`) |
| Opret booking for gæst | `CreateBookingForGuestCommand` (`:188`) | — |
| Ryd felter | `ClearGuestCommand` (`:192`) | — |

Felter (alle `IsReadOnly = !IsEditing`): `SelectedGuest.FirstName/.LastName/.PhoneNumber/.Email/.Country/.PassportNumber` (`:130-181`).

Dialoger (alle fra code-behind): `NewGuestView` modal til oprettelse (callback `AddGuestToOverview`, `:41-49`), `NewGuestView` modal til redigering af en kopi (callback `UpdateSelectedGuest`, `:52-60`), `NewBookingView` modal med valgt gæst (`:61-66`), `MessageBox` via `ShowInfoDialog`-event (`:34-35`).

### 4.4 NewGuestView (modal Window)

Formular til opret/rediger gæst; samme vindue til begge, styret af `isEdit` i konstruktøren.

| Handling | Command | Bundne properties |
|---|---|---|
| Gem (dynamisk knaptekst) | `SaveCommand`, `IsEnabled={Binding CanSave}` (`:55`) | `SaveButtonText`, `CanSave` |
| Annullér | `CancelCommand` (`:56`) | — |
| Udfyld felter | — | `FirstName`, `LastName`, `PhoneNumber`, `Email`, `Country`, `PassportNumber` (`:22-47`) |
| (titel/fejl) | — | `WindowTitle` (`:14`), `ErrorMessage` (`:50`) |

Code-behind kobler `ShowError`, `ShowInfo`, `ShowConfirmation`, `RequestClose`, `OnSave` (`.xaml.cs:14-19`).

### 4.5 NewBookingView (modal Window)

| Handling | Command | Bundne properties |
|---|---|---|
| Vælg check-in dato | — | `CheckInDate` (`:60`) |
| Vælg check-out dato | — | `CheckOutDate` (`:80`) |
| Vælg værelse | — (SelectedItem) | `NewBookingRoomList`, `SelectedRoom` (`:96-97`) |
| Indtast gæsteoplysninger | — | `FirstName`, `LastName`, `Email`, `PassportNumber`, `PhoneNumber`, `Country` (`:147-223`) |
| Gem booking | `SaveBookingCommand` (`:238`) | — |
| (fejlvisning) | — | `ErrorMessage`, OneWay (`:259`) |

**Der er ingen Annullér-knap** — vinduet kan kun lukkes via titellinjens X.

### 4.6 RoomOverviewView

| Handling | Command | Bundne properties |
|---|---|---|
| Opret værelse (åbner sidepanel) | `OpenCreateRoomCommand` (`:50`) | `IsRoomFormOpen`, `CurrentSideContent` |
| Åbn filter-popup / anvend filter | `ApplyFilterCommand` på ToggleButton (`:73`) | `IsChecked` styrer popup (`:79`) |
| Vælg filterværdier | — | `Floors`/`SelectedFloor`, `RoomSizes`/`SelectedRoomSize`, `RoomStatuses`/`SelectedRoomStatus` (`:93-107`) |
| Anvend filter (knap i popup) | `ApplyFilterCommand` (`:113`) | — |
| Ryd filter | `ClearFilterCommand` (`:128`) | — |
| Rediger værelse (pr. række) | `OpenEditRoomCommand`, param = rækkens Room (`:170-171`) | — |
| Slet værelse (pr. række) | `DeleteRoomCommand`, param = rækkens Room (`:183-184`) | — |
| (tabel) | — | `Rooms` med `RoomNumber`, `Floor`, `RoomSize`, `Capacity`, `Status` (`:143-165`) |

Popup med filtervalg forankret til `FilterButton` (`:76-118`). Formularen vises som inline sidepanel (`:207-209`). Tom, ubrugt handler `DataGrid_SelectionChanged` (`.xaml.cs:30-33`).

### 4.7 RoomFormView

| Handling | Command | Bundne properties |
|---|---|---|
| Udfyld værelsesdata | — | `Room.RoomNumber`, `Room.Floor`, `Room.RoomSize`, `Room.Capacity` (`:26-38`) |
| Vælg status (Available/Maintanance/Cleaning/Disabled — hardkodede i XAML) | — | `Room.Status` (`:42-46`) |
| Annullér | `CancelCommand` (`:59`) | — |
| Gem (dynamisk knaptekst) | `SaveCommand` (`:63`) | `ButtonText` (`:61`) |
| (overskrift) | — | `Title` (`:17`) |

### 4.8 GuestPolicyView og SalesOverviewView

Begge er rene placeholder-skærme med udelukkende en overskrift-TextBlock. Ingen bindinger, ingen commands, tom code-behind.

### Regler gemt i converters/code-behind

**Ægte forretningsregler**

1. **Bookingens bredde udtrykker antal overnatninger, ikke antal dage** — `(EndDate − StartDate).Days`. `Converters/CalendarConverters.cs:149`
2. **Halvdags-regel for check-in:** booking tegnes fra midt på check-in-dagen (`daysOffset + 0.5`) — koder antagelsen om at værelset er optaget fra middag. `Converters/CalendarConverters.cs:31`, `:48`
3. **Bookinger startet før den viste periode klippes til periodens start** og får `+0.5` dag på bredden. `Converters/CalendarConverters.cs:81`, `:139`
4. **Et værelses tidslinje viser kun bookinger hvor `Booking.Room.RoomId` matcher;** bookinger uden rum udelades helt. `Converters/CalendarConverters.cs:197`
5. **Booking-statusflow er kodet i XAML-triggere:** Confirm vises kun ved `Pending` (`Views/BookingOverviewView.xaml:988`); Cancel kun ved `Pending`/`Confirmed` (`:1037`, `:1040`); Edit skjules ved `CheckedIn`/`CheckedOut` (`:958`, `:961`).
6. **Redigering af gæstefelter kun tilladt i redigeringstilstand** — alle seks felter `IsReadOnly = !IsEditing`. `Views/GuestOverviewView.xaml:132` (samt `:142,152,162,172,179`)
7. **"Edit Guest" kræver en valgt gæst** (`NotNullToBoolConverter`) — `Views/GuestOverviewView.xaml:186`. "New Booking"-knappen lige under har **ingen** tilsvarende guard (`:187-190`).
8. **Ved redigering af en gæst arbejdes der på en kopi**, som først skrives tilbage via `UpdateSelectedGuest`. `Views/GuestOverviewView.xaml.cs:52-60`
9. **Enter i gæste-søgefeltet udløser sortering, ikke søgning.** `Views/GuestOverviewView.xaml.cs:68-77`
10. **Værelsesstatusserne er hardkodet som fire XAML-elementer** i stedet for enum/VM-collection. `Views/Forms/RoomFormView.xaml:43-46`

**Ren præsentation (ingen forretningsregel)**

11. Statusfarver: Pending=gul, Confirmed=blå, CheckedIn=grøn, CheckedOut=grå, Cancelled=rød. `Converters/BookingStatusColorConverter.cs:15-34`
12. `CheckInStatusConverter` **afgør ikke om check-in er muligt** trods navnet — formaterer kun tekst ud fra tidsstempler. `Converters/CheckInStatusConverter.cs:16-27`
13. `InverseBoolConverter` — negering. `Converters/InverseBoolConverter.cs:11-13`
14. `NotNullToBoolConverter` — null-check til bool. `Converters/NotNullToBoolConverter.cs:11`
15. `DateHeaderConverter` — int til streng, funktionelt en no-op. `Converters/CalendarConverters.cs:171-178`
16. `BindingProxy` — teknisk hjælper til GridView-kolonneheaders. `Core/BindingProxy.cs:5-20`
17. `Theme/`-mapperne indeholder udelukkende styles — ingen Binding, Command eller DataTrigger.

### Navigation

- **Ét ContentControl som navigationsflade** — `MainWindow.xaml:104-109` binder `Content` til `CurrentView`.
- **DataContext sættes deklarativt** til `MainViewModel` (`MainWindow.xaml:15-17`) — ingen DI, ingen container.
- **Fem RadioButtons kalder hver sin command** (`MainWindow.xaml:55,65,74,83,92`). "Booking Overview" er default (`:60`).
- **View-opslag via implicitte DataTemplates i App.xaml** (`App.xaml:19-37`).
- **Global converter-registrering:** `InverseBoolConverter`, `BooleanToVisibilityConverter`, `NotNullToBoolConverter` (`App.xaml:39-41`). Kalender-converterne er lokale til BookingOverviewView (`:17-25`).
- **Intra-view navigation:** RoomOverviewView bruger samme mønster med en lokal DataTemplate (`RoomOverviewView.xaml:17-19`) og `ContentControl` (`:209`) styret af `IsRoomFormOpen` (`:207`).
- **Modal navigation:** `NewGuestView` og `NewBookingView` åbnes med `ShowDialog()` fra GuestOverviewViews code-behind (`:48, 59, 64`). De indgår ikke i DataTemplate-navigationen.
- **Sidemenuen har ingen post til NewBooking/NewGuest/RoomForm** — kun tilgængelige indefra andre views.

### Tomme/uimplementerede views

1. `Views/GuestPolicyView.xaml:11-16` — kun overskrift. Menupunktet findes dog (`MainWindow.xaml:92`).
2. `Views/SalesOverviewView.xaml:11-16` — kun overskrift (`MainWindow.xaml:83`).
3. Delvist: "Payment Status" i BookingOverviewView er hardkodet "Coming soon..." (`:599-602`).
4. Død kode: tom `DataGrid_SelectionChanged` i `Views/RoomOverviewView.xaml.cs:30-33`.

### Uklart/modstridende (views)

1. **Inkonsistent DataContext-strategi.** `GuestOverviewView.xaml.cs:29` og `RoomOverviewView.xaml.cs:27` instantierer selv en ViewModel, hvorved App.xaml's DataTemplate-instans overskrives. Et viewskift nulstiller Guest- og RoomOverview, men ikke BookingOverview.
2. **`ApplyFilterCommand` er bundet til både ToggleButton og Apply-knappen** (`RoomOverviewView.xaml:73` og `:113`) — filteret køres hver gang popup'en åbnes *og* lukkes.
3. **RoomFormViews status-ComboBox binder `SelectedItem` til `Room.Status`, men items er `ComboBoxItem`-objekter** (`RoomFormView.xaml:41-47`). Klassisk WPF-fejlkilde. Bemærk stavefejlen "Maintanance" (`:44`).
4. **Check In/Check Out-knapperne har ingen status-triggere** (`:1000-1019`, `:1052-1070`) i modsætning til Edit/Confirm/Cancel — de styres kun af `CanExecute`.
5. **`BookingLeftMarginConverter` indeholder modstridende halvfærdig logik** (`CalendarConverters.cs:37-48`); den ydre if er reelt uden effekt, og kommentarerne er en tankestrøm, ikke en specifikation. Samme mønster i `BookingWidthConverter` (`:86-150`).
6. **Søgefeltet binder til `SearchGuest`, men Enter kalder `SortGuestsByName()`** — ligner en navne-/adfærdsforveksling.
7. **To parallelle gæste-oprettelsesflows** — `NewGuestView` med validering og `CanSave`-guard, `NewBookingView` med løse gæstefelter uden guard.
8. **`NewBookingView` mangler Annullér-knap** — modsat `NewGuestView` og `RoomFormView`.
9. **`GuestOverviewView.OpenNewBooking` opdaterer ikke oversigten bagefter**, selvom kommentaren siger det (`.xaml.cs:61-65`).
10. **`OpenNewBooking` sætter ikke `Owner`** (`.xaml.cs:63-64`), i modsætning til de to andre dialogåbninger.
11. **Bookingbjælkens ToolTip viser `GuestID`** (`:345`) — sandsynligvis efterladt debug-binding.
12. **`SortCommand` tager magiske strenge som parameter** på syv steder — enhver omdøbning bryder sorteringen lydløst ved runtime.
13. **`ViewDuration` sammenlignes mod strengene "Week"/"Month"/"Year"** i DataTriggers (`:158,175,191,210,414`).

---

## 5. analyse:tests

### Oversigt over testfilerne

| Testfil | Antal tests | Tester | Kræver DB? | Genbrugsværdi + begrundelse |
|---|---|---|---|---|
| `NFHotel_Tests/Models/BookingTests.cs` | 16 | Model (`Booking`) | Nej i testen — men ja i praksis (se DB-afsnit) | **Høj.** Rene, hurtige tests af `Validate()`, `ValidateEdit()`, `NumberOfNights`, `BookingNumber`. Kan bruges 1:1 som facit. |
| `NFHotel_Tests/Models/GuestTests.cs` | 13 | Model (`Guest`) | Nej i testen — men ja i praksis | **Høj.** Låser hele valideringskontrakten inkl. de præcise fejltekster. 2 af 13 er konstruktør-boilerplate. |
| `NFHotel_Tests/Models/RoomTests.cs` | 17 | Model (`Room`) | Nej i testen — men ja i praksis | **Mellem.** Kerne-valideringen (5-6 tests) er værdifuld; ca. halvdelen er trivielle kopier. |
| `NFHotel_Tests/Repositories/BookingRepoTests.cs` | 12 | Repo | **Ja — live SQL Server** | **Mellem.** Fejlkontrakten og navigation-property-loading er ægte regler, men uadskilleligt bundet til en fysisk DB. |
| `NFHotel_Tests/Repositories/GuestRepoTests.cs` | 13 | Repo | **Ja — live SQL Server** | **Lav.** Næsten udelukkende CRUD-roundtrip. Kun `GetAllByName` og nullable-pas udtrykker reel adfærd. Ingen cleanup-hook. |
| `NFHotel_Tests/Repositories/RoomRepoTests.cs` | 15 | Repo | **Ja — live SQL Server** | **Mellem.** `GetAllByAvailability` og de fire `GetRoomsFromCriteria`-tests beskriver et rigtigt filter-API. Resten er CRUD. |
| `NFHotel_Tests/ViewModels/BookingOverviewViewModelTests.cs` | 22 | ViewModel | **Nej — eneste fil med ægte mocks** (Moq mod `IBookingRepo`/`IRoomRepo`) | **Høj.** Den mest værdifulde fil: 20 af 22 tests koder bookingens tilstandsmaskine. Eksekverbar use-case-specifikation. |
| `NFHotel_Tests/ViewModels/NewBookingViewModelTests.cs` | 20 | ViewModel | **Sandsynligvis ja, indirekte** — VM konstrueres parameterløst uden mocks (`:23`) | **Lav.** 14-16 af 20 er property-boilerplate. Kun `CheckOutDate_BeforeCheckIn_SetsErrorMessage` (`:264`) tester logik. |

**I alt 128 `[TestMethod]`** (README påstår 129).

`MSTestSettings.cs` (31 linjer): `[AssemblyInitialize]` bygger `ConfigurationBuilder` fra `appsettings.json` og sætter den statiske `DatabaseConfig.ConnectionString`. Sætter `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]` (`:7`).

`NFHotel_Tests.csproj` (29 linjer): `net8.0-windows`, `Nullable enable`, `ImplicitUsings enable`. Pakker: `Moq 4.20.72`, `MSTest 4.0.1`. Projektreference til WPF-projektet. `<None Update="appsettings.json">` med `PreserveNewest`.

### DB-afhængighed

**Kort svar: ja — repo-testene rammer en rigtig, delt SQL Server-database, og det er en lokal/on-prem instans, ikke Azure.**

**Hvilken DB rammes**
- Ingen hardkodet connection string i testkoden. Den kommer udelukkende fra `appsettings.json` → `ConnectionStrings:DefaultConnection` (`MSTestSettings.cs:28`).
- README's skabelon er `Server=YOUR_SERVER_NAME;Database=HotelBooking;Trusted_Connection=True;TrustServerCertificate=True` (`README.md:56`), og de foreslåede servernavne er `COMPUTERNAME\SQLEXPRESS`, `localhost\SQLEXPRESS`, `(localdb)\MSSQLLocalDB` (`:46-50`). `Trusted_Connection=True` understøttes ikke af Azure SQL Database. **Intet spor af Azure i testprojektet.**
- Databasenavnet er `HotelBooking` — **samme database som applikationen selv bruger**. Ingen separat testdatabase, ingen transaktion-rollback, ingen in-memory-erstatning.

**Hvordan konfigureres den**
- Ét globalt `[AssemblyInitialize]`-hook (`MSTestSettings.cs:18-29`) skriver til den statiske, globale, mutérbare `DatabaseConfig.ConnectionString`. Eneste konfigurationsflade.
- Repos instantieres parameterløst i `[TestInitialize]` (`BookingRepoTests.cs:27-29`, `GuestRepoTests.cs:22`, `RoomRepoTests.cs:22`) — **ingen konstruktør-injektion**, hvilket er grunden til at de ikke kan mockes.
- ViewModel'en *har* en injektionsflade: `new BookingOverviewViewModel(_mockBookingRepo.Object, _mockRoomRepo.Object)` (`BookingOverviewViewModelTests.cs:50`).

**Hvad sker der hvis den ikke findes**
- Filen indlæses med `optional: false` (`MSTestSettings.cs:24`). **`appsettings.json` findes ikke i repoet** — og heller ikke `appsettingsTemplate.json`, som README selv siger skal committes (`README.md:132`).
- Konsekvens: `[AssemblyInitialize]` kaster `FileNotFoundException`, og et kastende assembly-init fejler **hele assemblyen** — også de 46 rene model-tests. Suiten er i committet tilstand **0 % kørbar out-of-the-box**, trods README's "Expected result: 129 tests passed" (`:77`).
- Findes `appsettings.json` men ikke serveren, fejler de 40 repo-tests med forbindelsesfejl.

**Ryddes der op? Ujævnt, og ikke pålideligt:**
- **`BookingRepoTests`** er den eneste med et rigtigt `[TestCleanup]` (`:56-76`) — men hele blokken er pakket i `try { } catch { }` med `// Ignore cleanup errors` (`:73-74`). Fejlslagen oprydning er usynlig; rækker hober sig lydløst op.
- **`GuestRepoTests` og `RoomRepoTests` har intet `[TestCleanup]`.** Oprydning står inde i hver testmetode (fx `GuestRepoTests.cs:57, 84, 124-125`; `RoomRepoTests.cs:54, 123-124`). Fejler en assert før den linje, køres oprydningen aldrig.
- Fem tests rydder slet ikke op ved design.
- **Parallelitet forværrer det:** `Parallelize(MethodLevel)` (`MSTestSettings.cs:7`) kører metoder samtidigt mod samme fysiske DB. `BookingRepoTests.Setup()` opretter ved hver testmetode et værelse med hardkodet nummer `"999"` og finder det med `GetAll().First(r => r.RoomNumber == "999")` (`:44-53`) — med 12 parallelle metoder kan `First(...)` returnere et andet tests værelse, og `Cleanup` sletter så et fremmed `_testRoomId`. Samme mønster i `RoomRepoTests` (`TEST101`, `TESTAVAIL`, `TESTFLOOR5`). Testene er hverken *Independent* eller *Repeatable*, trods FIRST-påstanden i hver filheader.

**Grænsetilfælde:** `NewBookingViewModelTests` konstruerer den rigtige `NewBookingViewModel()` uden mocks (`:23, 208, 223`). `CheckOutDate_BeforeCheckIn_SetsErrorMessage` (`:264-277`) noterer selv at "UpdateAvailableRooms is called automatically when dates change" (`:271`) — bruger den rutine en repo internt, rammer også disse 20 tests databasen indirekte. **Behandl filen som DB-mistænkt.** (Bekræftet af analyse:viewmodels: `NewBookingViewModel`s ctor kalder `LoadAllAvailableRooms()` → `IRoomRepo.GetAllByAvailability()`.)

### Regler testene beviser

**Booking-domænet (model)**

- **T-01** — En booking skal have en startdato. → `Validate_MissingStartDate_ReturnsError`, `Models/BookingTests.cs:41`
- **T-02** — En booking skal have en slutdato. → `Validate_MissingEndDate_ReturnsError`, `:61`
- **T-03** — Slutdato skal ligge efter startdato. → `Validate_EndDateBeforeStartDate_ReturnsError`, `:81`
- **T-04** — Startdato må ikke ligge i fortiden. → `Validate_StartDateInPast_ReturnsError`, `:101`
- **T-05** — En booking skal have et værelse tilknyttet. → `Validate_MissingRoom_ReturnsError`, `:121`
- **T-06** — En booking skal have en gæst tilknyttet. → `Validate_MissingGuest_ReturnsError`, `:142`
- **T-07** — Check-ud skal ligge efter check-ind. → `Validate_CheckOutBeforeCheckIn_ReturnsError`, `:163`
- **T-08** — Ved redigering gælder samme datoregler via `ValidateEdit(start, end)`. → `ValidateEdit_EndDateBeforeStartDate_ReturnsError` + `ValidateEdit_StartDateInPast_ReturnsError`, `:206`, `:222`
- **T-09** — Antal nætter = differencen i hele dage (17.→18. dec = 1 nat), 0 ved uudfyldte datoer. → `NumberOfNights_TwoDayBooking_ReturnsOne` + `NumberOfNights_DefaultDates_ReturnsZero`, `:242`, `:276`
- **T-10** — Bookingnummer = `FLZ-` + ID nulpolstret til 6 cifre. → `BookingNumber_FormatsCorrectly` (`"FLZ-000123"`), `:293`

**Gæst-domænet (model)**

- **T-11** — Fornavn, efternavn, telefonnummer og land er påkrævede. → `Models/GuestTests.cs:75, 117, 201, 222`
- **T-12** — Whitespace tæller som tomt. → `Validate_WhitespaceFirstName_ReturnsError`, `:96`
- **T-13** — E-mail skal indeholde både `@` og `.`. → `Validate_EmailWithoutAtSign_ReturnsError` + `Validate_EmailWithoutDot_ReturnsError`, `:159`, `:180`
- **T-14** — Pasnummer er valgfrit; `null` giver ingen fejl. → `Validate_PassportNumberOptional_NoError`, `:263`
- **T-15** — Validering samler alle fejl i én liste. → `Validate_MultipleErrors_ReturnsAllErrors` (forventer ≥ 5), `:243`

**Værelse-domænet (model)**

- **T-16** — Værelsesnummer (også mod whitespace) og værelsesstørrelse er påkrævede. → `Models/RoomTests.cs:75, 95, 155`
- **T-17** — Etage skal være > 0. → `Validate_FloorZero_ReturnsError` + `Validate_NegativeFloor_ReturnsError`, `:115`, `:135`
- **T-18** — Kapacitet skal være > 0. → `Validate_CapacityZero_ReturnsError`, `:175`
- **T-19** — Et nyt værelse er `Available` og har tomt værelsesnummer. → `Constructor_Parameterless_CreatesEmptyRoom`, `:39`

**Bookingens tilstandsmaskine (ViewModel — mest genbrugelige gruppe)**

- **T-20** — Kun en `Pending` booking kan bekræftes. → `CanExecuteConfirm_*`, `ViewModels/BookingOverviewViewModelTests.cs:36, 61, 86`
- **T-21** — Check-ind er tilladt fra både `Pending` og `Confirmed` — bekræftelse er ikke en forudsætning. → `:133`, `:159`
- **T-22** — Der kan ikke tjekkes ind før startdatoen. → `CanExecuteCheckIn_BeforeStartDate_ReturnsFalse`, `:185`
- **T-23** — Ikke check-ind på allerede indtjekket eller annulleret booking. → `:211`, `:237`
- **T-24** — Check-ud kræver status `CheckedIn`. → `:266`, `:293`, `:319`
- **T-25** — Annullering tilladt fra `Pending`/`Confirmed`, ikke efter check-ind. → `:350, 375, 400, 426`
- **T-26** — Redigering tilladt fra `Pending`/`Confirmed`, låst efter check-ind/-ud. → `:455, 480, 505, 531`
- **T-27** — Ingen kommando er aktiv uden en valgt booking. → `:112`
- **T-28** — Oversigten skjuler annullerede bookinger. → `FilterBookings_ExcludesCancelledBookings`, `:562`
- **T-29** — Fritekstsøgning filtrerer på gæstens navn. → `:585`
- **T-30** — Ved booking for eksisterende gæst forudfyldes alle gæstefelter. → `Constructor_WithGuest_LoadsGuestData`, `ViewModels/NewBookingViewModelTests.cs:193`
- **T-31** — Check-ud før check-ind giver fejlbesked med det samme. → `:264`

**Repository-kontrakten (kræver DB)**

- **T-32** — `Create`/`Update` med `null` kaster `ArgumentNullException`. → `Repositories/BookingRepoTests.cs:110`, `:305`
- **T-33** — Opdatering/sletning af ikke-eksisterende ID kaster `ArgumentException`. → `:276`, `:331`
- **T-34** — `GetById` på ukendt ID returnerer `null` — booking, gæst og værelse. → `BookingRepoTests.cs:192`, `GuestRepoTests.cs:156`, `RoomRepoTests.cs:157`
- **T-35** — `BookingRepo.GetById` eager-loader `Room` og `Guest`. → `:356`
- **T-36** — Repo'et validerer inputmodellen før skrivning (ugyldigt `Room` afvises ved create og update). → `RoomRepoTests.cs:58`, `:340`
- **T-37** — `GetAllByAvailability()` returnerer kun `Available`. → `:170`
- **T-38** — `GetRoomsFromCriteria` behandler `null` som "intet filter". → `:324`, `:294`
- **T-39** — Hvert filterkriterium er ekskluderende, ikke bare inkluderende. → `:213` (assert `:231`)
- **T-40** — `GetAllByName` søger på tværs af for- og efternavn og matcher delstreng. → `GuestRepoTests.cs:219`
- **T-41** — Pasnummer kan fjernes igen ved opdatering (sættes til `NULL`, ikke tom streng). → `:286`

### Boilerplate / lav værdi

Ca. 45 af de 128 tests er ikke værd at portere:

- **Property get/set-tests** (`NewBookingViewModelTests.cs:31-149`, 8 stk.) — tester C#'s auto-property-mekanik. De verificerer ikke engang `PropertyChanged`, hvilket ellers er pointen i en MVVM-VM.
- **"Er den ikke-null?"-tests**: `SaveBookingCommand_IsNotNull` (`:236`), `NewBookingRoomList_IsInitialized` (`:284`), `ErrorMessage_InitialValue_IsEmpty` (`:166`), `SelectedRoom_InitialValue_IsNull` (`:152`), `Constructor_Parameterless_CreatesEmptyGuest` (`GuestTests.cs:41`).
- **`CheckInDate_Null_NoErrorMessage`** (`:250`) — tomt Act.
- **Duplikerede model-tests**: `RoomSize_AcceptsSingle/Double/Suite` (`RoomTests.cs:278, 297, 316`) er tre identiske kopier; `Status_CanBeSetTo*` (`:248, 261`) tester enum-tildeling.
- **`GuestData_CreatesValidGuest_*`** (`NewBookingViewModelTests.cs:298, 325`) — VM'en indgår ikke i det der testes; det er `GuestTests` ad en omvej.
- **"PreservesData"-roundtrip-tests** (`GuestRepoTests.cs:344, 374`; `RoomRepoTests.cs:454, 401`) — tester ADO.NET-mapping, koster 4-6 DB-rundture hver.
- **`GetAll_ReturnsAll*`** (`BookingRepoTests.cs:135`, `GuestRepoTests.cs:92`, `RoomRepoTests.cs:89`) — assert `Count >= 2` mod delt DB kan reelt ikke fejle.

### Testopsætning

- **Framework:** MSTest, meta-pakke **4.0.1** (`.csproj:12`). Målramme `net8.0-windows` — binder suiten til Windows-only CI, fordi den refererer WPF-projektet direkte.
- **Mocking:** Moq **4.20.72** (`.csproj:11`). **Præcis ét sted mockes reelt:** `BookingOverviewViewModelTests.cs:19-20, 27-28`. Ingen anden testfil importerer Moq. Ingen `Verify(...)` nogen steder — mocks bruges kun som stubs.
- **Isolationsstrategi i tre lag:** rene enheder (46 tests) → mockede afhængigheder (22) → integration mod fysisk DB (40) + 20 i gråzonen.
- **AAA-disciplin: meget høj.** Stort set alle 128 tests har eksplicitte `// Arrange` / `// Act` / `// Assert`. Navngivning følger `Metode_Scenarie_ForventetResultat`. Næsten alle asserts har forklarende besked.
- **Afvigelser:** (a) exception-tests bruger manuel `bool exceptionThrown` + try/catch i stedet for `Assert.ThrowsException<T>` — 8 steder, fx `BookingRepoTests.cs:114-127`; en forkert exception-type fanges ikke som sådan. (b) Oprydning ligger i et fjerde `// Cleanup`-afsnit efter Assert. (c) Flere repo-tests har Act og Assert flettet sammen.
- **Ingen `[DataRow]`/`[DataTestMethod]`** nogen steder — alle varianter er skrevet ud i fuld længde, hvilket forklarer at 3.195 linjer kun rummer 128 tests. Ingen testhjælpere eller builders.

### Uklart/modstridende (tests)

1. **Testtallet passer ikke.** README påstår 129; der er 128. Alle otte per-fil-tal i README (`:120-130`) afviger. README er skrevet ud fra en anden version af koden.
2. **To positive tests ser ud til at være slettet.** `BookingRepoTests.cs:325-330` har overskriften `DELETE TESTS` efterfulgt af et hul; kun `Delete_NonExistentBooking` er tilbage. Samme i `RoomRepoTests.cs:333-339` (`UPDATE TESTS`). **Der findes ingen test der beviser at man kan slette en booking eller opdatere et værelse med succes.**
3. **Pakkeversioner i README stemmer ikke med `.csproj`** (MSTest 4.0.2/Moq 4.20.70 vs. 4.0.1/4.20.72).
4. **`Microsoft.Extensions.Configuration*` er ikke refereret i `.csproj`** — kompilerer kun via transitiv afhængighed gennem projektreferencen. Skjult og skrøbeligt.
5. **`appsettingsTemplate.json` mangler** — setup-instruktionen kan ikke følges som skrevet.
6. **FIRST-påstanden er selvmodsigende** — "< 5 seconds total" mod 40 integrationstests med `Parallelize(MethodLevel)` og hardkodede nøgler.
7. **README siger "Model and ViewModel tests run without database dependencies"** (`:201`) — teknisk sandt for testkoden, praktisk falsk pga. `optional: false`.
8. **README er delvist en indsat chatsamtale** (`:209-222`) og henviser til fire dokumenter der ikke findes. Kan ikke bruges som autoritativ kilde.
9. **`using System.Windows.Media.Media3D;` i `MSTestSettings.cs:5`** er irrelevant og fastholder `net8.0-windows`.
10. **`Nullable enable` mod ikke-initialiserede felter** — CS8618-advarsler overalt.
11. **`DatabaseConfig.ConnectionString` er en statisk mutérbar global** — med parallel eksekvering kan tests ikke pege mod forskellige databaser.
12. **Uafklaret: er `NewBookingViewModel` DB-fri?** Bør verificeres før man antager at de 20 tests kan køre uden SQL Server.
13. **Modstrid mellem T-20 og T-21** — en `Pending` booking kan tjekkes ind uden bekræftelse. Bevidst walk-in-regel eller manglende regel? Skal afklares før tilstandsmaskinen refaktoreres.
14. **`GetAllByName_PartialMatch` er selvsaboterende** — søger på `"Test"` i en delt DB hvor snesevis af tests indsætter gæster ved navn "Test".
