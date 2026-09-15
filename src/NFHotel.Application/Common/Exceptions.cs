namespace NFHotel.Application.Common;

/// <summary>
/// Databasens sidste værn mod dobbeltbooking slog til (B-03).
/// </summary>
/// <remarks>
/// Overlap afvises to steder, og de gør ikke det samme: <c>BookingService</c> spørger
/// domænet (den normale vej, pæn fejlkode, testbar uden database), mens PostgreSQLs
/// exclusion constraint fanger <i>kapløbet</i> mellem to samtidige oprettelser der begge
/// så et ledigt rum.
/// <para>
/// Infrastructure oversætter <c>SQLSTATE 23P01</c> til denne type, fordi Application ikke
/// må kende <c>PostgresException</c>. Servicen fanger den og returnerer
/// <see cref="ErrorCodes.Booking.RoomNotAvailable"/> — samme fejlkode som servicetjekket,
/// så brugeren ser det samme uanset hvilket værn der stoppede hende.
/// </para>
/// </remarks>
public sealed class BookingOverlapConflictException : Exception
{
    /// <summary>Opretter fejlen.</summary>
    /// <param name="message">Teknisk besked til loggen. Vises aldrig til brugeren.</param>
    public BookingOverlapConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Opretter fejlen med en underliggende databasefejl.</summary>
    /// <param name="message">Teknisk besked til loggen.</param>
    /// <param name="innerException">Den underliggende fejl fra dataadgangslaget.</param>
    public BookingOverlapConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// En unik-constraint blev brudt — fx to rum med samme rumnummer (BR-N-01).
/// </summary>
/// <remarks>
/// Oversat fra <c>SQLSTATE 23505</c> i Infrastructure. Servicen tjekker eksplicit inden
/// den skriver; denne fejl er kapløbsværnet bag det tjek.
/// </remarks>
public sealed class UniqueConstraintViolationException : Exception
{
    /// <summary>Opretter fejlen.</summary>
    /// <param name="message">Teknisk besked til loggen.</param>
    /// <param name="constraintName">Navnet på den brudte constraint. Bruges som diskriminator.</param>
    public UniqueConstraintViolationException(string message, string constraintName)
        : base(message)
    {
        ConstraintName = constraintName;
    }

    /// <summary>Opretter fejlen med en underliggende databasefejl.</summary>
    /// <param name="message">Teknisk besked til loggen.</param>
    /// <param name="constraintName">Navnet på den brudte constraint.</param>
    /// <param name="innerException">Den underliggende fejl fra dataadgangslaget.</param>
    public UniqueConstraintViolationException(string message, string constraintName, Exception innerException)
        : base(message, innerException)
    {
        ConstraintName = constraintName;
    }

    /// <summary>Navnet på den constraint der blev brudt, fx <c>ux_room_room_number</c>.</summary>
    public string ConstraintName { get; }
}

/// <summary>
/// Rækken blev ændret af en anden mellem læsning og skrivning (optimistisk samtidighed).
/// </summary>
/// <remarks>
/// Oversat fra dataadgangslagets samtidighedsfejl. Servicen returnerer en fejlkode, så
/// brugeren kan hente igen og prøve forfra — det gamle system ignorerede
/// <c>rowsAffected</c> og fejlede lydløst.
/// </remarks>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>Opretter fejlen.</summary>
    /// <param name="message">Teknisk besked til loggen.</param>
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Opretter fejlen med en underliggende databasefejl.</summary>
    /// <param name="message">Teknisk besked til loggen.</param>
    /// <param name="innerException">Den underliggende fejl fra dataadgangslaget.</param>
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
