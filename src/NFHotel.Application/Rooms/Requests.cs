using NFHotel.Domain.Rooms;

namespace NFHotel.Application.Rooms;

/// <summary>
/// Filter til rumsøgningen (BR-74, BR-76).
/// </summary>
/// <remarks>
/// Alle felter er valgfrie: <c>null</c> betyder "filtrér ikke på dette". "Ryd filter" er
/// derfor et tomt <see cref="RoomFilter"/> — der er ingen særskilt use case for det.
/// Filteret er et <i>objekt</i> og ikke løse parametre, fordi antallet af kriterier
/// allerede var på vej fra tre til fem.
/// </remarks>
/// <param name="Floor">Etage, eller <c>null</c> for alle.</param>
/// <param name="Size">Værelsestype, eller <c>null</c> for alle.</param>
/// <param name="Status">Bookbarhed, eller <c>null</c> for alle.</param>
/// <param name="HousekeepingStatus">Rengørings- og servicestand, eller <c>null</c> for alle.</param>
public sealed record RoomFilter(
    int? Floor = null,
    RoomSize? Size = null,
    RoomStatus? Status = null,
    HousekeepingStatus? HousekeepingStatus = null);

/// <summary>
/// Opret et nyt rum (BR-81, BR-97 til BR-100, BR-113).
/// </summary>
/// <remarks>
/// Ingen status: et nyoprettet rum er altid bookbart og rent (BR-81). Kaldet vælger det ikke.
/// </remarks>
/// <param name="RoomNumber">Værelsesnummer, fx "101" eller "12B".</param>
/// <param name="Floor">Etage. Skal være større end 0.</param>
/// <param name="Size">Værelsestype.</param>
/// <param name="Capacity">Antal personer rummet kan rumme. Skal være større end 0.</param>
public sealed record CreateRoomRequest(
    string RoomNumber,
    int Floor,
    RoomSize Size,
    int Capacity);

/// <summary>
/// Ret et rums stamdata (BR-82, BR-84, BR-97 til BR-100, BR-113).
/// </summary>
/// <remarks>
/// <b>Indeholder bevidst hverken <c>Status</c> eller <c>HousekeepingStatus</c> (A-08).</b>
/// Et statusskift har sin egen use case på <see cref="IRoomService"/>, så en retry fra
/// mobilappen ikke kan komme til at rulle en rengøringsstand tilbage ved at sende en
/// forældet værdi med. Samtidig dør BR-82's "RoomId == 0 betyder opret": oprettelse og
/// opdatering er to metoder.
/// </remarks>
/// <param name="RoomId">Rummets id. Skal være større end 0.</param>
/// <param name="RoomNumber">Værelsesnummer.</param>
/// <param name="Floor">Etage.</param>
/// <param name="Size">Værelsestype.</param>
/// <param name="Capacity">Antal personer rummet kan rumme.</param>
public sealed record UpdateRoomRequest(
    int RoomId,
    string RoomNumber,
    int Floor,
    RoomSize Size,
    int Capacity);
