using NFHotel.Domain.Bookings;

namespace NFHotel.Application.Bookings;

/// <summary>
/// Dataadgang for bookinger. Implementeres i Infrastructure.
/// </summary>
/// <remarks>
/// Repositoryet <b>beslutter ikke</b>. Vil servicen vide hvilke bookinger der spærrer et
/// rum, sender den selv statussættet med — hentet fra <see cref="BookingRules.BlocksRoom"/>.
/// Reglen står dermed ét sted i stedet for i SQL.
/// <para>
/// Læsning returnerer projektioner (A-11); entiteter hentes kun på kommandostier, hvor
/// domænemetoderne skal kaldes.
/// </para>
/// </remarks>
public interface IBookingRepository
{
    // --------------------------------------------------------
    // LÆSNING — projektioner (A-11)
    // --------------------------------------------------------

    /// <summary>
    /// Henter én bookings læsemodel med rum og gæst.
    /// </summary>
    /// <param name="bookingId">Bookingens id.</param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Læsemodellen, eller <c>null</c> hvis bookingen ikke findes (BR-115).</returns>
    Task<BookingReadModel?> GetReadModelAsync(int bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Henter bookingoversigten for et datovindue (BR-23, BR-24, BR-25).
    /// </summary>
    /// <param name="periodStart">Vinduets første dato, inklusiv.</param>
    /// <param name="periodEnd">Vinduets sidste dato, eksklusiv.</param>
    /// <param name="includedStatuses">
    /// De statusser der skal med. Kalderen vælger — repositoryet hardkoder ikke
    /// "uden annullerede".
    /// </param>
    /// <param name="searchText">
    /// Fritekst på gæstenavn eller værelsesnummer, eller <c>null</c> for alle. Filtreres i
    /// databasen, ikke i hukommelsen (BR-111).
    /// </param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>De bookinger hvis effektive periode rører vinduet.</returns>
    Task<IReadOnlyList<BookingReadModel>> GetOverviewAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyCollection<BookingStatus> includedStatuses,
        string? searchText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Henter beslaglæggelserne i et datovindue, til tilgængelighedsopslaget (BR-34, BR-39).
    /// </summary>
    /// <param name="periodStart">Vinduets første dato, inklusiv.</param>
    /// <param name="periodEnd">Vinduets sidste dato, eksklusiv.</param>
    /// <param name="blockingStatuses">
    /// De statusser der spærrer et rum. Kommer fra <see cref="BookingRules.BlocksRoom"/>.
    /// </param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Grovfiltrerede beslaglæggelser. Den endelige overlapsafgørelse træffes af domænet.</returns>
    /// <remarks>
    /// Returnerer projektioner og ikke entiteter, fordi kaldet kun bruges som negation i
    /// tilgængelighedsopslaget. At hente hele tabellen og filtrere i hukommelsen er præcis
    /// BR-111's fejl.
    /// </remarks>
    Task<IReadOnlyList<RoomOccupancyDto>> GetOccupancyAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyCollection<BookingStatus> blockingStatuses,
        CancellationToken cancellationToken = default);

    // --------------------------------------------------------
    // KOMMANDOSTIER — entiteter
    // --------------------------------------------------------

    /// <summary>
    /// Henter bookingen som entitet, så domænemetoderne kan kaldes på den.
    /// </summary>
    /// <param name="bookingId">Bookingens id.</param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Bookingen, eller <c>null</c> hvis den ikke findes (BR-115).</returns>
    Task<Booking?> GetByIdAsync(int bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Henter de bookinger der <i>kan</i> være i konflikt med en ønsket periode på et rum (BR-19, B-03).
    /// </summary>
    /// <param name="roomId">Rummet den ønskede periode gælder.</param>
    /// <param name="periodStart">Ønsket ankomstdato.</param>
    /// <param name="periodEnd">Ønsket afrejsedato, eksklusiv.</param>
    /// <param name="blockingStatuses">
    /// De statusser der spærrer. Kommer fra <see cref="BookingRules.BlocksRoom"/> — ikke fra
    /// et hardkodet <c>WHERE</c>.
    /// </param>
    /// <param name="excludeBookingId">
    /// Bookingen der redigeres, så den ikke blokerer sig selv. <c>null</c> ved oprettelse.
    /// </param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>
    /// <b>Kandidater</b>, ikke konflikter. Grovfiltreringen på rum, statussæt og datovindue
    /// sker i SQL; selve afgørelsen træffes af <see cref="BookingRules.Conflicts"/>, så
    /// repository og domæne aldrig kan blive uenige om <c>&lt;</c> mod <c>&lt;=</c> (A-04).
    /// </returns>
    Task<IReadOnlyList<Booking>> GetOverlapCandidatesAsync(
        int roomId,
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyCollection<BookingStatus> blockingStatuses,
        int? excludeBookingId,
        CancellationToken cancellationToken = default);

    /// <summary>Markerer en ny booking til indsættelse. Persisterer ikke.</summary>
    /// <param name="booking">Bookingen.</param>
    /// <remarks>
    /// Hedder <c>Add</c> og ikke <c>Create</c>: metoden persisterer ikke, den markerer.
    /// Det gamle navn løj om hvornår rækken fandtes.
    /// </remarks>
    void Add(Booking booking);

    /// <summary>Markerer en ændret booking til opdatering. Persisterer ikke.</summary>
    /// <param name="booking">Bookingen.</param>
    void Update(Booking booking);
}
