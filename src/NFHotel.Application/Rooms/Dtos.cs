using NFHotel.Domain.Rooms;

namespace NFHotel.Application.Rooms;

/// <summary>
/// Hvilke overgange der er lovlige på et rum lige nu (A-07).
/// </summary>
/// <remarks>
/// Rummets pendant til <c>BookingActionsDto</c>. Felterne kommer fra domænets
/// <c>Room.Can*</c>-properties, som er de samme udtryk overgangsmetoderne bruger som guard.
/// Web må derfor ikke udlede lovligheden af <c>Status</c> og <c>HousekeepingStatus</c> selv —
/// det ville kopiere tilstandsmaskinen ud i præsentationslaget (A-07).
/// <para>
/// Et felt der er <c>false</c> betyder "denne handling flytter ikke rummet". En overgang til
/// den tilstand rummet allerede har er stadig et lovligt kald (A-08), men får <c>false</c>,
/// fordi felterne findes for at tegne knapper — se kommentaren ved <c>Room.Can*</c>.
/// </para>
/// </remarks>
/// <param name="CanTakeOutOfService">Om rummet må tages ud af drift.</param>
/// <param name="CanSendToMaintenance">Om rummet må spærres for vedligehold.</param>
/// <param name="CanReturnToService">Om rummet må sættes i drift igen.</param>
/// <param name="CanMarkDailyCleaningDue">Om daglig rengøring må markeres som manglende.</param>
/// <param name="CanMarkDepartureCleaningDue">Om slutrengøring må markeres som manglende.</param>
/// <param name="CanStartCleaning">Om rengøringen må startes.</param>
/// <param name="CanCompleteCleaning">Om rengøringen må afsluttes.</param>
/// <param name="CanReportServiceNeeded">Om et servicebehov må meldes.</param>
/// <param name="CanStartService">Om servicen må startes.</param>
/// <param name="CanCompleteService">
/// Om servicen må afsluttes. Dækker begge udfald — "rent" og "skal rengøres" — fordi guarden
/// er den samme; UI'ets to knapper deler derfor dette ene felt.
/// </param>
public sealed record RoomActionsDto(
    bool CanTakeOutOfService,
    bool CanSendToMaintenance,
    bool CanReturnToService,
    bool CanMarkDailyCleaningDue,
    bool CanMarkDepartureCleaningDue,
    bool CanStartCleaning,
    bool CanCompleteCleaning,
    bool CanReportServiceNeeded,
    bool CanStartService,
    bool CanCompleteService);

/// <summary>
/// Et rum som det vises i en liste eller en tabelrække.
/// </summary>
/// <remarks>
/// Ingen formatering: datoer er datoer, status er enum, ikke farve eller tekst. Formatering
/// hører i Web — ellers flytter det gamle systems problem bare ét lag ned.
/// <para>
/// <see cref="Actions"/> er en beregnet property og ikke en konstruktørparameter: rækken
/// bygges som SQL-projektion i repositoryet (A-11), og databasen kan ikke fylde et
/// <see cref="RoomActionsDto"/> i den. Det er også listen der har brug for den — rummets
/// handlingsknapper sidder på det valgte listeelement.
/// </para>
/// </remarks>
/// <param name="RoomId">Rummets id.</param>
/// <param name="RoomNumber">Værelsesnummer, fx "101" eller "12B".</param>
/// <param name="Floor">Etage.</param>
/// <param name="Size">Værelsestype.</param>
/// <param name="Capacity">Antal personer rummet kan rumme.</param>
/// <param name="Status">Bookbarhed (B-04).</param>
/// <param name="HousekeepingStatus">Rengørings- og servicestand (B-04, A-06).</param>
public sealed record RoomListItemDto(
    int RoomId,
    string RoomNumber,
    int Floor,
    RoomSize Size,
    int Capacity,
    RoomStatus Status,
    HousekeepingStatus HousekeepingStatus)
{
    /// <summary>
    /// Hvilke overgange der er lovlige på rummet lige nu (A-07). Samme kilde som
    /// <see cref="RoomDetailsDto.Actions"/>.
    /// </summary>
    public RoomActionsDto Actions => RoomMapping.ToActions(Status, HousekeepingStatus);
}

/// <summary>
/// Et rum med alt hvad detaljeskærmen har brug for.
/// </summary>
/// <param name="RoomId">Rummets id.</param>
/// <param name="RoomNumber">Værelsesnummer.</param>
/// <param name="Floor">Etage.</param>
/// <param name="Size">Værelsestype.</param>
/// <param name="Capacity">Antal personer rummet kan rumme.</param>
/// <param name="Status">Bookbarhed (B-04).</param>
/// <param name="HousekeepingStatus">Rengørings- og servicestand (B-04).</param>
/// <param name="IsBookable">
/// Om rummet overhovedet må udlejes (BR-110). Beregnet af domænets
/// <see cref="Room.IsBookable"/> — Web må ikke udlede den af <paramref name="Status"/> selv.
/// </param>
/// <param name="HasBookings">
/// Om der findes bookinger på rummet. Bærer sletteguarden (BR-78, BR-112) op i UI'et, så
/// advarslen kan vises inden knappen trykkes i stedet for som en fejl bagefter.
/// </param>
public sealed record RoomDetailsDto(
    int RoomId,
    string RoomNumber,
    int Floor,
    RoomSize Size,
    int Capacity,
    RoomStatus Status,
    HousekeepingStatus HousekeepingStatus,
    bool IsBookable,
    bool HasBookings)
{
    /// <summary>
    /// Hvilke overgange der er lovlige lige nu (A-07), så Web kan tegne knapper uden at kende
    /// en eneste overgang.
    /// </summary>
    /// <remarks>
    /// Beregnet property og ikke en konstruktørparameter, fordi detaljevisningen hentes som
    /// SQL-projektion i repositoryet (A-11): databasen kan ikke fylde et
    /// <see cref="RoomActionsDto"/> i rækken. Udtrykkene ligger derfor ét sted i Application —
    /// <see cref="RoomMapping.ToActions(RoomStatus, HousekeepingStatus)"/> — som både denne
    /// property og entitetsstien bruger.
    /// </remarks>
    public RoomActionsDto Actions => RoomMapping.ToActions(Status, HousekeepingStatus);
}

/// <summary>
/// Et bookbart rum i tilgængelighedsopslaget (BR-34, BR-36, BR-37).
/// </summary>
/// <remarks>
/// Bevidst uden status: listen indeholder kun rum der <i>er</i> ledige, så en statuskolonne
/// ville kun kunne have én værdi. Typen bor i <c>Rooms</c> og ikke i <c>Bookings</c>, fordi
/// det er et rum — selv om det er bookingens use case der efterspørger det.
/// </remarks>
/// <param name="RoomId">Rummets id.</param>
/// <param name="RoomNumber">Værelsesnummer.</param>
/// <param name="Floor">Etage.</param>
/// <param name="Size">Værelsestype.</param>
/// <param name="Capacity">Antal personer rummet kan rumme.</param>
public sealed record AvailableRoomDto(
    int RoomId,
    string RoomNumber,
    int Floor,
    RoomSize Size,
    int Capacity);

/// <summary>
/// Valgmulighederne i rumfilteret (BR-75).
/// </summary>
/// <remarks>
/// Kun etager kommer fra data. Størrelse, status og rengøringsstand kommer fra enums, hvilket
/// automatisk lukker BR-126, hvor det gamle systems XAML-dropdown og enum var uenige.
/// </remarks>
/// <param name="Floors">Etager der faktisk findes rum på, stigende.</param>
/// <param name="Sizes">Alle definerede værelsestyper.</param>
/// <param name="Statuses">Alle definerede bookbarhedsstatusser.</param>
/// <param name="HousekeepingStatuses">Alle definerede rengørings- og servicestande.</param>
public sealed record RoomFilterOptionsDto(
    IReadOnlyList<int> Floors,
    IReadOnlyList<RoomSize> Sizes,
    IReadOnlyList<RoomStatus> Statuses,
    IReadOnlyList<HousekeepingStatus> HousekeepingStatuses);
