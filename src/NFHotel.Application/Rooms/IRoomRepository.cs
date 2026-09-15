using NFHotel.Domain.Rooms;

namespace NFHotel.Application.Rooms;

/// <summary>
/// Dataadgang for rum. Implementeres i Infrastructure.
/// </summary>
/// <remarks>
/// Fire regler for hele repository-laget:
/// <list type="number">
/// <item><description>
/// <b>Repositoryet beslutter ikke.</b> Filtre er parametre, ikke hårdkodede valg — det
/// gamle <c>GetAllByAvailability()</c> afgjorde selv hvad "tilgængelig" betød (BR-110).
/// </description></item>
/// <item><description>
/// <b>Nullability er ærlig.</b> <see cref="GetByIdAsync"/> returnerer <c>Room?</c>, og
/// servicen oversætter <c>null</c> til en fejlkode (BR-115).
/// </description></item>
/// <item><description>
/// <b>Ingen <c>SaveChanges</c> her.</b> <see cref="Add"/>, <see cref="Update"/> og
/// <see cref="Remove"/> markerer kun og er derfor synkrone — de laver ingen IO.
/// </description></item>
/// <item><description>
/// <b>Læsning returnerer projektioner (A-11).</b> Entiteter hentes kun på kommandostier.
/// </description></item>
/// </list>
/// </remarks>
public interface IRoomRepository
{
    // --------------------------------------------------------
    // LÆSNING — projektioner (A-11)
    // --------------------------------------------------------

    /// <summary>
    /// Henter et rums detaljer som projektion.
    /// </summary>
    /// <param name="roomId">Rummets id.</param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Detaljerne, eller <c>null</c> hvis rummet ikke findes (BR-115).</returns>
    Task<RoomDetailsDto?> GetDetailsAsync(int roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Henter alle rum som listerækker.
    /// </summary>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Alle rum, sorteret på værelsesnummer.</returns>
    Task<IReadOnlyList<RoomListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Henter de rum der matcher filteret (BR-74, BR-76).
    /// </summary>
    /// <param name="filter">Filteret. Et tomt filter betyder alle rum.</param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>De matchende rum. Filtreringen sker i databasen, ikke i hukommelsen (BR-111).</returns>
    Task<IReadOnlyList<RoomListItemDto>> GetByFilterAsync(
        RoomFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Henter rum med den angivne bookbarhed som tilgængelighedsrækker.
    /// </summary>
    /// <param name="status">Statussen kalderen ønsker. Kalderen — ikke repositoryet — vælger (BR-110).</param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Rummene, sorteret på værelsesnummer.</returns>
    Task<IReadOnlyList<AvailableRoomDto>> GetByStatusAsync(
        RoomStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Henter de etager der faktisk findes rum på (BR-75).
    /// </summary>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Etagenumre uden dubletter, stigende.</returns>
    Task<IReadOnlyList<int>> GetDistinctFloorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Afgør om værelsesnummeret allerede er i brug (BR-N-01).
    /// </summary>
    /// <param name="roomNumber">Værelsesnummeret der undersøges.</param>
    /// <param name="excludeRoomId">
    /// Rummet der redigeres, så det ikke kolliderer med sig selv. <c>null</c> ved oprettelse.
    /// </param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Sand hvis et andet rum har nummeret.</returns>
    Task<bool> RoomNumberExistsAsync(
        string roomNumber,
        int? excludeRoomId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Afgør om der findes bookinger på rummet (BR-78, BR-112).
    /// </summary>
    /// <param name="roomId">Rummets id.</param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Sand hvis mindst én booking peger på rummet.</returns>
    /// <remarks>
    /// Erstatter det gamle systems <c>catch (SqlException 547)</c>. Fremmednøglen bevares som
    /// sidste værn, men reglen står ikke længere i en catch-blok med brugervendt engelsk tekst.
    /// </remarks>
    Task<bool> HasAnyBookingsAsync(int roomId, CancellationToken cancellationToken = default);

    // --------------------------------------------------------
    // KOMMANDOSTIER — entiteter
    // --------------------------------------------------------

    /// <summary>
    /// Henter rummet som entitet, så domænemetoderne kan kaldes på det.
    /// </summary>
    /// <param name="roomId">Rummets id.</param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Rummet, eller <c>null</c> hvis det ikke findes (BR-115).</returns>
    Task<Room?> GetByIdAsync(int roomId, CancellationToken cancellationToken = default);

    /// <summary>Markerer et nyt rum til indsættelse. Persisterer ikke.</summary>
    /// <param name="room">Rummet.</param>
    void Add(Room room);

    /// <summary>Markerer et ændret rum til opdatering. Persisterer ikke.</summary>
    /// <param name="room">Rummet.</param>
    void Update(Room room);

    /// <summary>Markerer et rum til sletning. Persisterer ikke.</summary>
    /// <param name="room">Rummet. Entiteten og ikke id'et, fordi servicen alligevel har hentet den.</param>
    void Remove(Room room);
}
