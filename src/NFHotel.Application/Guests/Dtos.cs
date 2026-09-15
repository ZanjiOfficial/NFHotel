namespace NFHotel.Application.Guests;

/// <summary>
/// En gæst som hun vises i en liste eller et søgeresultat.
/// </summary>
/// <remarks>
/// Bærer <see cref="HasPassportNumber"/> og <b>ikke</b> selve nummeret. Pasnummeret er
/// krypteret at-rest (B-09), så hver visning af feltet er en dekryptering — og ingen af de
/// 126 porterede regler viser pasnummer i en liste. Privacy by design uden at koste
/// funktionalitet (D-04).
/// </remarks>
/// <param name="GuestId">Gæstens id.</param>
/// <param name="FirstName">Fornavn.</param>
/// <param name="LastName">Efternavn.</param>
/// <param name="FullName">"Fornavn Efternavn". Kommer fra domænet, ikke fra en visningsformatering.</param>
/// <param name="Email">E-mail.</param>
/// <param name="PhoneNumber">Telefonnummer.</param>
/// <param name="Country">Land.</param>
/// <param name="HasPassportNumber">Om der er registreret et pasnummer. Selve værdien udelades bevidst.</param>
public sealed record GuestListItemDto(
    int GuestId,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string PhoneNumber,
    string Country,
    bool HasPassportNumber);

/// <summary>
/// En gæst med alle felter, til redigeringsformularen.
/// </summary>
/// <remarks>
/// Eneste DTO der bærer pasnummeret i klartekst, og kun fordi formularen skal kunne rette
/// det. Brug <see cref="GuestListItemDto"/> alle andre steder.
/// </remarks>
/// <param name="GuestId">Gæstens id.</param>
/// <param name="FirstName">Fornavn.</param>
/// <param name="LastName">Efternavn.</param>
/// <param name="FullName">"Fornavn Efternavn".</param>
/// <param name="Email">E-mail.</param>
/// <param name="PhoneNumber">Telefonnummer.</param>
/// <param name="Country">Land.</param>
/// <param name="PassportNumber">Pasnummer, eller <c>null</c> hvis der ikke er registreret et (BR-57).</param>
public sealed record GuestDetailsDto(
    int GuestId,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string PhoneNumber,
    string Country,
    string? PassportNumber);
