using NFHotel.Domain.Guests;

namespace NFHotel.Application.Guests;

/// <summary>
/// Dataadgang for gæster. Implementeres i Infrastructure.
/// </summary>
/// <remarks>
/// Læsning returnerer projektioner (A-11); entiteter hentes kun på kommandostier.
/// Skrivemetoderne markerer og persisterer ikke — det gør <c>IUnitOfWork</c>.
/// <para>
/// Bemærk hvad der bevidst <b>ikke</b> findes: ingen søgning og ingen sortering på
/// pasnummer. Feltet er krypteret med en randomiseret ciffer (B-09), så selv et
/// lighedsopslag ville ikke virke — og ingen af de 126 regler beder om det.
/// </para>
/// </remarks>
public interface IGuestRepository
{
    // --------------------------------------------------------
    // LÆSNING — projektioner (A-11)
    // --------------------------------------------------------

    /// <summary>
    /// Henter en gæsts detaljer som projektion.
    /// </summary>
    /// <param name="guestId">Gæstens id.</param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Detaljerne, eller <c>null</c> hvis gæsten ikke findes (BR-115).</returns>
    Task<GuestDetailsDto?> GetDetailsAsync(int guestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Søger gæster (BR-68, BR-N-05).
    /// </summary>
    /// <param name="searchTerms">
    /// Søgeordene. <b>HVERT</b> ord skal findes (case-insensitivt) i enten fornavn,
    /// efternavn, land eller e-mail — altså AND mellem ord, OR mellem felter. Et tomt sæt
    /// returnerer alle gæster (BR-70).
    /// </param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>De matchende gæster som listerækker.</returns>
    /// <remarks>
    /// Semantikken er konsolideret fra to uenige søgninger i det gamle system: repoet søgte
    /// kun på for- og efternavn (BR-116), mens ViewModel'en delte teksten i ord og krævede
    /// at alle ord fandtes i navn, land eller e-mail (BR-68). BR-68 vandt (BR-N-05).
    /// Tokeniseringen er reglen og sker i servicen; matchningen er dataadgang og sker her.
    /// Kontrakten skal pinnes af en test i Infrastructure.Tests, ellers driver de fra hinanden.
    /// </remarks>
    Task<IReadOnlyList<GuestListItemDto>> SearchAsync(
        IReadOnlyCollection<string> searchTerms,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Afgør om gæsten findes (BR-114).
    /// </summary>
    /// <param name="guestId">Gæstens id.</param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Sand hvis gæsten findes. Henter ikke hele grafen.</returns>
    Task<bool> ExistsAsync(int guestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Afgør om der findes bookinger på gæsten.
    /// </summary>
    /// <param name="guestId">Gæstens id.</param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Sand hvis mindst én booking peger på gæsten.</returns>
    /// <remarks>Guard for sletning (BR-N-02) og spor for D-05's persondatasletning.</remarks>
    Task<bool> HasAnyBookingsAsync(int guestId, CancellationToken cancellationToken = default);

    // --------------------------------------------------------
    // KOMMANDOSTIER — entiteter
    // --------------------------------------------------------

    /// <summary>
    /// Henter gæsten som entitet, så domænemetoderne kan kaldes på den.
    /// </summary>
    /// <param name="guestId">Gæstens id.</param>
    /// <param name="cancellationToken">Annulleringstoken.</param>
    /// <returns>Gæsten, eller <c>null</c> hvis hun ikke findes (BR-115).</returns>
    Task<Guest?> GetByIdAsync(int guestId, CancellationToken cancellationToken = default);

    /// <summary>Markerer en ny gæst til indsættelse. Persisterer ikke.</summary>
    /// <param name="guest">Gæsten. Id'et sættes på entiteten når der gemmes.</param>
    void Add(Guest guest);

    /// <summary>Markerer en ændret gæst til opdatering. Persisterer ikke.</summary>
    /// <param name="guest">Gæsten.</param>
    void Update(Guest guest);

    /// <summary>Markerer en gæst til sletning (BR-N-02). Persisterer ikke.</summary>
    /// <param name="guest">Gæsten.</param>
    void Remove(Guest guest);
}
