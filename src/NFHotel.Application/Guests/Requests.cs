namespace NFHotel.Application.Guests;

/// <summary>
/// De seks redigérbare gæstefelter (BR-52 til BR-57, BR-91 til BR-96).
/// </summary>
/// <remarks>
/// Egen type, fordi felterne indtastes to steder: i gæsteformularen og i "opret booking for
/// en ny gæst" (BR-47). Det er den eneste tilladte krydsreference mellem feature-mapper, og
/// den går mod en request — aldrig mod en anden features service.
/// <para>
/// Felterne er <c>string</c> og ikke <c>string?</c>, fordi de er obligatoriske. At de
/// alligevel kan være tomme fanges af <c>GuestRules.Validate</c>, ikke af typesystemet —
/// en tom streng fra en formular er et brugerudfald, ikke en programmørfejl.
/// </para>
/// </remarks>
/// <param name="FirstName">Fornavn.</param>
/// <param name="LastName">Efternavn.</param>
/// <param name="Email">E-mail. Skal indeholde både '@' og '.' (BR-55, BR-93).</param>
/// <param name="PhoneNumber">Telefonnummer. Intet formatkrav (BR-94).</param>
/// <param name="Country">Land.</param>
/// <param name="PassportNumber">Pasnummer. Valgfrit og uvalideret ud over længden (BR-57).</param>
public sealed record GuestFields(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Country,
    string? PassportNumber);

/// <summary>
/// Opret en gæst (BR-52 til BR-57, BR-60, BR-91 til BR-96).
/// </summary>
/// <param name="Fields">Gæstens felter.</param>
public sealed record CreateGuestRequest(GuestFields Fields);

/// <summary>
/// Ret en gæst (BR-62, BR-66, BR-72, BR-91 til BR-96).
/// </summary>
/// <remarks>
/// <see cref="GuestId"/> er påkrævet og ligger uden for <see cref="Fields"/>, fordi id'et
/// ikke er redigérbart (BR-72). Det lukker samtidig fejlen hvor en redigeret gæst mistede
/// sit id undervejs og blev gemt på id 0. BR-66's "arbejd på en kopi" bliver overflødigt:
/// requesten <i>er</i> kopien.
/// </remarks>
/// <param name="GuestId">Gæstens id. Skal være større end 0.</param>
/// <param name="Fields">De nye feltværdier.</param>
public sealed record UpdateGuestRequest(int GuestId, GuestFields Fields);
