using NFHotel.Application.Guests;
using NFHotel.Application.Rooms;
using NFHotel.Domain.Bookings;

namespace NFHotel.Application.Bookings;

/// <summary>
/// Hvilke handlinger der er lovlige på en booking lige nu (BR-22).
/// </summary>
/// <remarks>
/// Den vigtigste enkeltdel i hele DTO-designet. Beregnes i Application ud fra domænets
/// tilstandsregler, så Web kan tegne knapper uden at kende en eneste statusovergang. Det er
/// sådan BR-02, BR-11, BR-14 og BR-22 (ViewModel) og BR-121, BR-122, BR-123 (XAML-triggere)
/// bliver til <b>én</b> implementering i stedet for seks.
/// <para>
/// Navnet er <c>CanReschedule</c> og ikke <c>CanEdit</c>, så DTO og domæne bruger samme ord
/// om samme regel (A-06).
/// </para>
/// </remarks>
/// <param name="CanConfirm">Om bookingen må bekræftes (BR-02, BR-121).</param>
/// <param name="CanCheckIn">Om gæsten må tjekkes ind i dag (BR-06). Walk-in er tilladt (B-01).</param>
/// <param name="CanCheckOut">Om gæsten må tjekkes ud (BR-09).</param>
/// <param name="CanCancel">Om bookingen må annulleres (BR-11, BR-122).</param>
/// <param name="CanReschedule">Om datoer og rum må ændres (BR-14, BR-123).</param>
public sealed record BookingActionsDto(
    bool CanConfirm,
    bool CanCheckIn,
    bool CanCheckOut,
    bool CanCancel,
    bool CanReschedule);

/// <summary>
/// En booking som den vises i oversigten.
/// </summary>
/// <param name="BookingId">Bookingens id.</param>
/// <param name="BookingNumber">Vist bookingnummer, fx <c>FLZ-000123</c> (BR-109).</param>
/// <param name="StartDate">Ankomstdato.</param>
/// <param name="EndDate">Booket afrejsedato. Historik — ændres aldrig af en udtjekning (A-01).</param>
/// <param name="EffectiveEndDate">
/// Den dato bookingen faktisk lægger beslag på rummet til (A-01). Afviger fra
/// <paramref name="EndDate"/> når gæsten er rejst tidligt.
/// </param>
/// <param name="Nights">Antal bookede overnatninger (BR-109, BR-117).</param>
/// <param name="Status">Bookingens livscyklustilstand.</param>
/// <param name="CheckInTime">Faktisk indtjekningstidspunkt, eller <c>null</c>.</param>
/// <param name="CheckOutTime">Faktisk udtjekningstidspunkt, eller <c>null</c>.</param>
/// <param name="RoomId">Rummets id.</param>
/// <param name="RoomNumber">Rummets værelsesnummer.</param>
/// <param name="GuestId">Gæstens id.</param>
/// <param name="GuestFullName">Gæstens fulde navn (BR-25).</param>
/// <param name="GuestCountry">Gæstens land.</param>
/// <param name="GuestEmail">Gæstens e-mail.</param>
/// <param name="Actions">Hvilke handlinger der er lovlige lige nu (BR-22).</param>
public sealed record BookingListItemDto(
    int BookingId,
    string BookingNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly EffectiveEndDate,
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

/// <summary>
/// En booking med alt hvad detaljeskærmen har brug for.
/// </summary>
/// <param name="BookingId">Bookingens id.</param>
/// <param name="BookingNumber">Vist bookingnummer (BR-109).</param>
/// <param name="StartDate">Ankomstdato.</param>
/// <param name="EndDate">Booket afrejsedato (historik, A-01).</param>
/// <param name="EffectiveEndDate">Den dato rummet faktisk er optaget til (A-01).</param>
/// <param name="Nights">Antal bookede overnatninger.</param>
/// <param name="Status">Bookingens livscyklustilstand.</param>
/// <param name="CheckInTime">Faktisk indtjekningstidspunkt, eller <c>null</c>.</param>
/// <param name="CheckOutTime">Faktisk udtjekningstidspunkt, eller <c>null</c>.</param>
/// <param name="CheckOutDate">Den hotel-lokale dato gæsten tjekkede ud, eller <c>null</c> (A-02, A-03).</param>
/// <param name="Room">Rummet.</param>
/// <param name="Guest">Gæsten, uden pasnummer.</param>
/// <param name="Actions">Hvilke handlinger der er lovlige lige nu (BR-22).</param>
public sealed record BookingDetailsDto(
    int BookingId,
    string BookingNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly EffectiveEndDate,
    int Nights,
    BookingStatus Status,
    DateTimeOffset? CheckInTime,
    DateTimeOffset? CheckOutTime,
    DateOnly? CheckOutDate,
    RoomListItemDto Room,
    GuestListItemDto Guest,
    BookingActionsDto Actions);

/// <summary>
/// Repositoryets projektion af en booking med sit rum og sin gæst (A-11).
/// </summary>
/// <remarks>
/// Læsestier henter denne i stedet for entiteten. Det fjerner fælden hvor en mapper kaldes
/// på en booking hentet uden navigationsdata og tavst giver tomme felter — og det er hurtigere.
/// <para>
/// Typen er ikke en DTO til UI: den er råt læsemateriale, som
/// <see cref="BookingMapping"/> gør til <see cref="BookingListItemDto"/> eller
/// <see cref="BookingDetailsDto"/>. Grunden til det ekstra trin er
/// <see cref="BookingActionsDto"/>, der afhænger af "i dag" — og datoen kommer fra uret i
/// servicen, ikke fra databasen (A-05).
/// </para>
/// </remarks>
/// <param name="BookingId">Bookingens id.</param>
/// <param name="StartDate">Ankomstdato.</param>
/// <param name="EndDate">Booket afrejsedato.</param>
/// <param name="EffectiveEndDate">
/// Effektiv slutdato (A-01). Projiceres fra databasens genererede kolonne
/// <c>effective_end_date</c>, som beregner samme udtryk som <c>Booking.EffectiveEndDate</c>.
/// </param>
/// <param name="Status">Bookingens livscyklustilstand.</param>
/// <param name="CheckInTime">Faktisk indtjekningstidspunkt, eller <c>null</c>.</param>
/// <param name="CheckOutTime">Faktisk udtjekningstidspunkt, eller <c>null</c>.</param>
/// <param name="CheckOutDate">Hotel-lokal udtjekningsdato, eller <c>null</c>.</param>
/// <param name="Room">Rummet, projiceret med.</param>
/// <param name="Guest">Gæsten, projiceret med og uden pasnummer.</param>
public sealed record BookingReadModel(
    int BookingId,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly EffectiveEndDate,
    BookingStatus Status,
    DateTimeOffset? CheckInTime,
    DateTimeOffset? CheckOutTime,
    DateOnly? CheckOutDate,
    RoomListItemDto Room,
    GuestListItemDto Guest);

/// <summary>
/// Én bookings beslaglæggelse af et rum, til tilgængelighedsopslaget.
/// </summary>
/// <remarks>
/// Bevidst så smal som muligt: kun det <see cref="BookingRules"/> skal bruge for at afgøre
/// om rummet er optaget. Alternativet — at hente alle bookinger i perioden som entiteter og
/// filtrere i hukommelsen — er præcis den fejl BR-111 beskriver.
/// <para>
/// Bemærk at overlapsafgørelsen <b>ikke</b> træffes her og heller ikke i SQL: servicen
/// bygger en <c>DateRange</c> af felterne og spørger domænet (A-01, A-04). Ellers ville
/// prædikatet findes to steder og kunne blive uenige om <c>&lt;</c> mod <c>&lt;=</c>.
/// </para>
/// </remarks>
/// <param name="BookingId">Bookingens id, så en booking kan udelades ved redigering.</param>
/// <param name="RoomId">Rummet der er lagt beslag på.</param>
/// <param name="StartDate">Første optagne nat.</param>
/// <param name="EffectiveEndDate">Første ledige dato efter beslaglæggelsen (A-01).</param>
/// <param name="Status">Bookingens status, så <c>BookingRules.BlocksRoom</c> kan efterprøves.</param>
public sealed record RoomOccupancyDto(
    int BookingId,
    int RoomId,
    DateOnly StartDate,
    DateOnly EffectiveEndDate,
    BookingStatus Status);
