# Architecture-Diagrams.md — Fase 1

> Bilag til `Architecture.md`. Tegnet af `design:diagrams` ud fra domæne- og lagdesignet.
> Diagrammerne følger den **reviderede** overlapsregel (A-01) og afgørelserne A-06 og A-07.
>
> **Gældende overlapsregel:** en booking blokerer rummet fra `StartDate` til sin effektive slutdato, medmindre den er `Cancelled`.
> `EffectiveEndDate = GREATEST(StartDate + 1 dag, COALESCE(CheckOutDate, EndDate))`. `EndDate` ændres aldrig.

---

## 1. Domænemodel

```mermaid
classDiagram
    direction TB

    class Booking {
        +int BookingId
        +DateOnly StartDate
        +DateOnly EndDate
        +DateOnly CheckOutDate
        +DateTimeOffset CheckInTime
        +DateTimeOffset CheckOutTime
        +BookingStatus Status
        +int RoomId
        +int GuestId
        +DateRange Period
        +DateOnly EffectiveEndDate
        +DateRange EffectivePeriod
        +int NumberOfNights
        +string BookingNumber
        +bool CanConfirm
        +bool CanCheckOut
        +bool CanCancel
        +bool CanReschedule
        +Create(period, roomId, guestId, today) Booking
        +CanCheckIn(today) bool
        +Confirm() void
        +CheckIn(occurredAt, today) void
        +CheckOut(occurredAt, checkOutDate) void
        +Cancel() void
        +Reschedule(newPeriod, newRoomId, today) void
    }

    class Room {
        +int RoomId
        +string RoomNumber
        +int Floor
        +RoomSize Size
        +int Capacity
        +RoomStatus Status
        +HousekeepingStatus HousekeepingStatus
        +bool IsBookable
        +Create(roomNumber, floor, size, capacity) Room
        +UpdateDetails(roomNumber, floor, size, capacity) void
        +TakeOutOfService() void
        +SendToMaintenance() void
        +ReturnToService() void
        +MarkDailyCleaningDue() void
        +MarkDepartureCleaningDue() void
        +StartCleaning() void
        +CompleteCleaning() void
        +ReportServiceNeeded() void
        +StartService() void
        +CompleteService(requiresCleaning) void
    }

    class Guest {
        +int GuestId
        +string FirstName
        +string LastName
        +string Email
        +string PhoneNumber
        +string Country
        +string PassportNumber
        +string FullName
        +Create(firstName, lastName, email, phoneNumber, country, passportNumber) Guest
        +UpdateDetails(firstName, lastName, email, phoneNumber, country, passportNumber) void
    }

    class DateRange {
        +DateOnly Start
        +DateOnly End
        +int Nights
        +Overlaps(other) bool
        +Contains(date) bool
        +StartsOnOrAfter(today) bool
    }

    class BookingStatus {
        <<enumeration>>
        Pending
        Confirmed
        CheckedIn
        CheckedOut
        Cancelled
    }

    class RoomStatus {
        <<enumeration>>
        Available
        OutOfService
        Maintenance
    }

    class HousekeepingStatus {
        <<enumeration>>
        Clean
        DailyCleaningDue
        DepartureCleaningDue
        CleaningInProgress
        ServiceRequired
        ServiceInProgress
    }

    class RoomSize {
        <<enumeration>>
        Single
        Double
        Suite
    }

    class BookingRules {
        +Overlaps(a, b) bool
        +BlocksRoom(status) bool
        +EffectiveEndDate(booking) DateOnly
        +Conflicts(existing, roomId, desiredPeriod) bool
    }

    class GuestRules {
        +Validate(firstName, lastName, email, phoneNumber, country) errorCodes
        +IsValidEmail(email) bool
    }

    class RoomRules {
        +IsValidRoomNumber(roomNumber) bool
        +IsValidFloor(floor) bool
        +IsValidCapacity(capacity) bool
        +Validate(roomNumber, floor, size, capacity) errorCodes
    }

    Booking "0..*" --> "1" Room : RoomId
    Booking "0..*" --> "1" Guest : GuestId
    Booking ..> DateRange
    Booking ..> BookingStatus
    Room ..> RoomStatus
    Room ..> HousekeepingStatus
    Room ..> RoomSize
    BookingRules ..> Booking
    GuestRules ..> Guest
    RoomRules ..> Room
```

Tre aggregatrødder uden navigerbar sammenhæng ud over fremmednøglerne. `DateRange` persisteres ikke — den er regelsted og parametertype. `CheckInTime`, `CheckOutTime`, `CheckOutDate` og `PassportNumber` er nullable; mermaid gengiver ikke `?`. Validerings­metoderne returnerer fejlkoder, ikke tekst (A-09).

---

## 2. Tilstandsdiagram — BookingStatus

```mermaid
stateDiagram-v2
    direction LR

    [*] --> Pending : Create<br/>guard startdato ikke i fortiden<br/>BR-01 BR-44 BR-45 BR-48 BR-104 BR-105 BR-106

    Pending --> Confirmed : Confirm<br/>guard status er Pending<br/>BR-02 BR-03 BR-05 BR-121

    Pending --> CheckedIn : CheckIn<br/>guard CheckInTime tom<br/>og StartDate senest i dag<br/>BR-06 BR-07 BR-08
    Confirmed --> CheckedIn : CheckIn<br/>guard CheckInTime tom<br/>og StartDate senest i dag<br/>BR-06 BR-07 BR-08

    CheckedIn --> CheckedOut : CheckOut<br/>guard CheckOutTime tom<br/>saetter CheckOutDate<br/>frigiver resterende naetter<br/>BR-09 BR-10 BR-107

    Pending --> Cancelled : Cancel - soft delete<br/>BR-11 BR-13 BR-122
    Confirmed --> Cancelled : Cancel - soft delete<br/>BR-11 BR-13 BR-122

    Pending --> Pending : Reschedule<br/>ny periode og nyt rum<br/>BR-14 BR-17 BR-18 BR-20 BR-108 BR-123
    Confirmed --> Confirmed : Reschedule<br/>ny periode og nyt rum<br/>BR-14 BR-17 BR-18 BR-20 BR-108 BR-123

    CheckedOut --> [*]
    Cancelled --> [*]

    note right of CheckedOut
        Slutstatus
        Blokerer stadig rummet men kun frem til
        effektiv slutdato lig udtjekningsdatoen
        EndDate bevares uaendret som historik
    end note

    note right of Cancelled
        Slutstatus
        Eneste status der frigiver rummet helt
    end note
```

`Confirmed` er ikke en forudsætning for check-in (B-01) — derfor to `CheckIn`-kanter. `Reschedule` er en selvovergang. Den reviderede regel rammer kun `CheckOut`: overgangen ændrer ikke *om* bookingen blokerer, men flytter den effektive slutdato ned til udtjekningsdatoen.

---

## 3. Tilstandsdiagram — HousekeepingStatus

```mermaid
stateDiagram-v2
    direction LR

    [*] --> Clean : Room.Create<br/>BR-81

    Clean --> DailyCleaningDue : MarkDailyCleaningDue
    ServiceRequired --> DailyCleaningDue : MarkDailyCleaningDue
    DailyCleaningDue --> DailyCleaningDue : idempotent ved retry

    Clean --> DepartureCleaningDue : MarkDepartureCleaningDue<br/>Application efter CheckOut
    DailyCleaningDue --> DepartureCleaningDue : MarkDepartureCleaningDue
    ServiceRequired --> DepartureCleaningDue : MarkDepartureCleaningDue
    DepartureCleaningDue --> DepartureCleaningDue : idempotent ved retry

    DailyCleaningDue --> CleaningInProgress : StartCleaning<br/>rengoeringspersonale
    DepartureCleaningDue --> CleaningInProgress : StartCleaning<br/>rengoeringspersonale
    CleaningInProgress --> Clean : CompleteCleaning<br/>rengoeringspersonale

    Clean --> ServiceRequired : ReportServiceNeeded
    DailyCleaningDue --> ServiceRequired : ReportServiceNeeded
    DepartureCleaningDue --> ServiceRequired : ReportServiceNeeded
    CleaningInProgress --> ServiceRequired : ReportServiceNeeded

    ServiceRequired --> ServiceInProgress : StartService<br/>servicetekniker
    ServiceInProgress --> Clean : CompleteService requiresCleaning falsk
    ServiceInProgress --> DepartureCleaningDue : CompleteService requiresCleaning sand

    note right of Clean
        Ingen af disse tilstande paavirker bookbarhed
        Room.IsBookable laeser kun RoomStatus
        Spaerring kraever SendToMaintenance
        B-04 og BR-126
    end note
```

Tre spor: daglig rengøring, slutrengøring efter afrejse, og service — de to første deler `CleaningInProgress`. `MarkDepartureCleaningDue` udløses af Application efter `Booking.CheckOut`, ikke af `Booking` selv (to aggregater).

**Rollerne er dokumentation, ikke håndhævelse i Fase 1 (A-12).** Autorisation er udskudt; enhver kan i praksis kalde enhver overgang indtil Identity bygges. Intet inspektionstrin — tilføjes som `Inspected` når mobil-guilden beder om det.

---

## 4. ER-diagram

```mermaid
erDiagram
    room ||--o{ booking : "room_id"
    guest ||--o{ booking : "guest_id"

    booking {
        integer booking_id PK "identity"
        date start_date "NOT NULL"
        date end_date "NOT NULL - bookede periode - historik"
        date check_out_date "NULL - hotellokal dato saettes af servicen"
        date effective_end_date "GENERATED STORED - styrer overlap"
        timestamptz check_in_time "NULL"
        timestamptz check_out_time "NULL"
        integer status "NOT NULL - BookingStatus 0 til 4"
        integer room_id FK "NOT NULL - ON DELETE RESTRICT"
        integer guest_id FK "NOT NULL - ON DELETE RESTRICT"
    }

    room {
        integer room_id PK "identity"
        varchar room_number "NOT NULL - 20 tegn - unikt - BR-N-01"
        integer floor "NOT NULL - check over nul"
        integer size "NOT NULL - RoomSize 0 til 2"
        integer capacity "NOT NULL - check over nul"
        integer status "NOT NULL - RoomStatus 0 til 2 - default 0"
        integer housekeeping_status "NOT NULL - HousekeepingStatus 0 til 5 - default 0"
    }

    guest {
        integer guest_id PK "identity"
        varchar first_name "NOT NULL - 100 tegn"
        varchar last_name "NOT NULL - 100 tegn"
        varchar email "NOT NULL - 100 tegn"
        varchar phone_number "NOT NULL - 50 tegn"
        varchar country "NOT NULL - 100 tegn"
        text passport_number "NULL - AES GCM krypteret - aldrig indekseret"
    }
```

Alle tre tabeller bærer desuden systemkolonnen `xmin` som optimistisk samtidighedstoken. Exclusion constraint, check constraints og indekser står i `Architecture.md` afsnit 5.

---

## 5. Lag- og pakkediagram

```mermaid
flowchart TB
    subgraph LWeb [Ydre lag - praesentation]
        Web["NFHotel.Web<br/>Blazor Server<br/>Razor og Program.cs - composition root"]
        Api["NFHotel.Api<br/>PLANLAGT - bygges ikke i Fase 1"]
    end

    subgraph LInfra [Infrastrukturlag]
        Infra["NFHotel.Infrastructure<br/>HotelDbContext og Configurations<br/>Repositories UnitOfWork SystemClock<br/>AesGcmStringEncryptor<br/>EF Core og Npgsql bor KUN her"]
    end

    subgraph LApp [Applikationslag]
        App["NFHotel.Application<br/>BookingService RoomService GuestService<br/>Repository-interfaces IUnitOfWork IClock<br/>DTOer Requests Result ErrorCodes"]
    end

    subgraph LDom [Domaenelag - kerne]
        Dom["NFHotel.Domain<br/>Booking Room Guest DateRange<br/>BookingRules GuestRules RoomRules<br/>BookingStatus RoomStatus HousekeepingStatus RoomSize<br/>NUL projektreferencer og NUL NuGet-pakker"]
    end

    Web --> App
    Web --> Infra
    Infra --> App
    App --> Dom

    Api -.-> App
    Api -.-> Infra

    classDef planned stroke-dasharray: 6 4,fill:#ffffff;
    class Api planned;
```

Beviset ligger i kantlisten: fire byggede kanter, hver fra et ydre lag mod kernen. `Domain` har **udgrad nul**. `Application` har præcis én udgående kant. `Infrastructure` peger på `Application`, aldrig omvendt — derfor bor repository-*interfaces* i Application og *implementeringerne* i Infrastructure.

Reglen der ikke kan tegnes — at `Application` ikke må referere EF Core eller Npgsql — håndhæves af arkitekturtesten.

---

## 6. Use case-diagram

```mermaid
flowchart LR
    Personale(["Personale<br/>eneste aktoer i Fase 1"])
    Kunde(["Kunde - UDSKUDT"])
    Mobil(["Rengoering og service via mobil - UDSKUDT"])

    subgraph UCB [IBookingService - 9]
        B1["GetByIdAsync"]
        B2["GetOverviewAsync<br/>BR-23 BR-24 BR-25 BR-32"]
        B3["GetAvailableRoomsAsync<br/>BR-34 BR-36 BR-39 BR-40 BR-110"]
        B4["CreateAsync<br/>BR-01 BR-41 til BR-49"]
        B5["ConfirmAsync<br/>BR-02 BR-03 BR-05"]
        B6["CheckInAsync<br/>BR-06 BR-07 BR-08"]
        B7["CheckOutAsync<br/>BR-09 BR-10 - saetter CheckOutDate"]
        B8["CancelAsync<br/>BR-11 BR-13"]
        B9["RescheduleAsync<br/>BR-14 BR-17 til BR-20"]
    end

    subgraph UCR [IRoomService - 15]
        R1["GetByIdAsync"]
        R2["GetAllAsync"]
        R3["SearchAsync<br/>BR-74 BR-76"]
        R4["GetFilterOptionsAsync<br/>BR-75"]
        R5["CreateAsync<br/>BR-97 til BR-100 BR-113"]
        R6["UpdateAsync<br/>BR-82 - uden status"]
        R7["DeleteAsync<br/>BR-78 BR-112"]
        R8["TakeOutOfServiceAsync"]
        R9["SendToMaintenanceAsync"]
        R10["ReturnToServiceAsync"]
        R11["MarkDailyCleaningDueAsync"]
        R12["MarkDepartureCleaningDueAsync"]
        R13["StartCleaningAsync"]
        R14["CompleteCleaningAsync"]
        R15["ReportServiceNeededAsync<br/>StartServiceAsync CompleteServiceAsync"]
    end

    subgraph UCG [IGuestService - 6]
        G1["GetByIdAsync<br/>BR-35"]
        G2["SearchAsync<br/>BR-68 BR-69 BR-70"]
        G3["CreateAsync<br/>BR-52 til BR-57 BR-91 til BR-96"]
        G4["UpdateAsync<br/>BR-62 BR-66 BR-72"]
        G5["DeleteAsync<br/>BR-N-02 kraever godkendelse"]
        G6["ValidateFields<br/>BR-58 BR-59 - synkron"]
    end

    subgraph UCX [Udskudt - tegnes men bygges ikke]
        X1["Selvbetjent booking og betaling<br/>ingen pris i domaenet"]
        X2["ISalesService<br/>UDELADT - BR-31 giver altid nul"]
        X3["Mobil-API for rengoering og service<br/>fladen er R11 til R15"]
    end

    Personale --> UCB
    Personale --> UCR
    Personale --> UCG
    Kunde -.-> X1
    Personale -.-> X2
    Mobil -.-> X3
    X3 -.-> UCR

    classDef deferred stroke-dasharray: 6 4,fill:#ffffff;
    class Kunde,Mobil,X1,X2,X3,UCX deferred;
```

Ét use case = én service-metode: **30 byggede use cases**. `IRoomService` voksede fra 9 til 15, fordi rengøringsovergangene fik hver sin metode i stedet for én generisk mål-status (A-07).

De syv rengøringsmetoder bygges nu som backoffice-funktioner. De er samtidig præcis den flade mobilappen senere skal kalde — derfor den stiplede pil fra `X3`.
