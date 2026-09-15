using NFHotel.Application.Guests;

namespace NFHotel.Application.Bookings;

/// <summary>
/// Sorteringsretning.
/// </summary>
public enum SortDirection
{
    /// <summary>Stigende.</summary>
    Ascending = 0,

    /// <summary>Faldende.</summary>
    Descending = 1
}

/// <summary>
/// Felt der kan sorteres på i bookingoversigten (BR-32).
/// </summary>
/// <remarks>
/// En enum og ikke en streng: en tastefejl i et kolonnenavn skal være en compilerfejl, ikke
/// en tavst usorteret liste.
/// </remarks>
public enum BookingSortField
{
    /// <summary>Bookingnummer — svarer til id-rækkefølgen (BR-109).</summary>
    BookingNumber = 0,

    /// <summary>Gæstens fulde navn (BR-25).</summary>
    GuestName = 1,

    /// <summary>Værelsesnummer.</summary>
    RoomNumber = 2,

    /// <summary>Ankomstdato.</summary>
    StartDate = 3,

    /// <summary>Booket afrejsedato.</summary>
    EndDate = 4,

    /// <summary>Bookingens status.</summary>
    Status = 5,

    /// <summary>Antal overnatninger (BR-117).</summary>
    Nights = 6
}

/// <summary>
/// Opret en booking (BR-01, BR-41 til BR-49, BR-51, BR-101 til BR-106).
/// </summary>
/// <remarks>
/// Bærer BR-47: <b>præcis én</b> af <see cref="GuestId"/> og <see cref="NewGuest"/> skal
/// være udfyldt. At begge eller ingen er sat er en valideringsfejl fra servicen — ikke en
/// <c>NullReferenceException</c> nede i en ViewModel.
/// </remarks>
/// <param name="CheckInDate">Ankomstdato, inklusiv.</param>
/// <param name="CheckOutDate">Afrejsedato, eksklusiv. Skal være efter ankomstdatoen (BR-103).</param>
/// <param name="RoomId">Rummets id (BR-45, BR-105).</param>
/// <param name="GuestId">En eksisterende gæsts id, eller <c>null</c> hvis gæsten oprettes nu.</param>
/// <param name="NewGuest">Felterne til en ny gæst, eller <c>null</c> hvis en eksisterende er valgt.</param>
public sealed record CreateBookingRequest(
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    int RoomId,
    int? GuestId,
    GuestFields? NewGuest);

/// <summary>
/// Flyt en booking til nye datoer og/eller et nyt rum (BR-14, BR-17 til BR-20, BR-108).
/// </summary>
/// <remarks>
/// Hedder bevidst ikke <c>UpdateBookingRequest</c>: use casen er "flyt bookingen", ikke
/// "sæt vilkårlige felter". Status, check-in og check-ud har deres egne use cases.
/// </remarks>
/// <param name="BookingId">Bookingens id.</param>
/// <param name="NewStartDate">Ny ankomstdato. Skal være i dag eller senere (BR-108).</param>
/// <param name="NewEndDate">Ny afrejsedato, eksklusiv.</param>
/// <param name="RoomId">Rummets id — samme som før, eller et nyt rum.</param>
public sealed record RescheduleBookingRequest(
    int BookingId,
    DateOnly NewStartDate,
    DateOnly NewEndDate,
    int RoomId);

/// <summary>
/// Forespørgsel til bookingoversigten (BR-23, BR-24, BR-25, BR-32).
/// </summary>
/// <remarks>
/// Periodefilteret er <b>altid</b> aktivt. I det gamle system filtrerede uge- og
/// månedsvisningen slet ikke, så hele tabellen blev hentet og sorteret i hukommelsen (BR-111).
/// </remarks>
/// <param name="PeriodStart">Første dato i vinduet, inklusiv.</param>
/// <param name="PeriodEnd">Sidste dato i vinduet, eksklusiv.</param>
/// <param name="SearchText">Fritekst på gæstenavn eller værelsesnummer (BR-23, BR-25). <c>null</c> for alle.</param>
/// <param name="SortBy">Feltet der sorteres på (BR-32).</param>
/// <param name="Direction">Sorteringsretning.</param>
/// <param name="IncludeCancelled">Om annullerede bookinger skal med (BR-13's soft-delete).</param>
public sealed record BookingOverviewQuery(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string? SearchText = null,
    BookingSortField SortBy = BookingSortField.StartDate,
    SortDirection Direction = SortDirection.Ascending,
    bool IncludeCancelled = false);
