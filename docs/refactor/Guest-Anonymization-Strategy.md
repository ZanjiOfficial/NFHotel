# D-05 — anonymiseringsstrategi for gæstedata

> Arbejdet som konkretisering af **D-05 (Q-41)**.
> Bookingdata og persondata har separate slette og retentionregler. Dette dokument beskriver håndteringen af persondata, når en gæst ikke kan slettes direkte på grund af eksisterende bookinger.

---

## Formål

En `Guest` kan i den nuværende model ikke slettes, hvis gæsten har eksisterende bookinger. Bookinger refererer til gæsten via `GuestId` og relationen er beskyttet med `DeleteBehavior.Restrict`.

D-05 kræver samtidig, at sletning af bookingdata og persondata behandles som to separate spor.

Formålet med strategien er derfor at definere, hvordan en gæsts persondata kan fjernes eller anonymiseres uden, at bryde eksisterende bookingrelationer.

Retentionperioderne er endnu ikke fastlagt og angives derfor fortsat som **X**, jf. D-05.


---

## Nuværende situation

`GuestService.DeleteAsync()` understøtter allerede sletning af en `Guest`.

Hvis gæsten ikke har nogen bookinger, fjernes gæsten via `IGuestRepository.Remove()`.

Hvis gæsten har en eller flere bookinger, afvises sletningen med `ErrorCodes.Guest.HasBookings`. Dette skyldes, at eksisterende bookinger fortsat refererer til gæsten via `GuestId`.

Relationen er også beskyttet i databasen med `DeleteBehavior.Restrict`, så en gæst med tilknyttede bookinger ikke kan slettes og efterlade ugyldige bookingrelationer.

**Konsekvens:** En gæst med eksisterende bookinger kan i øjeblikket ikke få sine persondata slettet gennem den eksisterende slettefunktion.

`Guest` indeholder følgende gæsteoplysninger:

| Felt | Nuværende krav |
|---|---|
| `FirstName` | Obligatorisk |
| `LastName` | Obligatorisk |
| `Email` | Obligatorisk |
| `PhoneNumber` | Obligatorisk |
| `Country` | Obligatorisk |
| `PassportNumber` | Valgfrit |

De obligatoriske felter håndhæves af `GuestRules`. Den eksisterende `UpdateDetails()` kan derfor ikke bruges til blot at tømme felterne som en anonymiseringsmekanisme.


---

## Foreslået anonymiseringsstrategi

Hvis en `Guest` ikke har eksisterende bookinger, kan gæsten fortsat slettes fysisk gennem den eksisterende `DeleteAsync()`.

Hvis en `Guest` har eksisterende bookinger, foreslås det at bevare `Guest`-rækken og relationen til eksisterende bookinger, mens gæstens identificerende oplysninger anonymiseres.


`GuestId` bevares som teknisk reference, så eksisterende relationer mellem `Guest` og `Booking` ikke brydes.

Dette løsning gør det muligt at bevare bookingrelationerne uden samtidig at være afhængig af de oprindelige identificerende gæsteoplysninger.

De konkrete værdier og felter, der skal anvendes ved anonymisering, skal selvfølgelig afklares og godkendes, før strategien implementeres.


---

## Felter ved anonymisering

Følgende håndtering af gæsteoplysninger foreslås som udgangspunkt:

| Felt | Foreslået håndtering | Begrundelse |
|---|---|---|
| `GuestId` | Bevares | Bookingrelationerne skal fortsat kunne referere til gæsten |
| `FirstName` | Anonymiseres | Kan identificere gæsten |
| `LastName` | Anonymiseres | Kan identificere gæsten |
| `Email` | Anonymiseres | Kan identificere og kontakte gæsten |
| `PhoneNumber` | Anonymiseres | Kan identificere og kontakte gæsten |
| `Country` | Skal afklares | Det skal vurderes, om oplysningen fortsat er nødvendig efter anonymisering |
| `PassportNumber` | Fjernes | Det oprindelige pasnummer skal ikke bevares som en del af en anonymiseret gæst |

De konkrete erstatningsværdier er endnu ikke besluttet. De skal blandt andet tage højde for de eksisterende krav i `GuestRules`, hvor flere af felterne ikke må være tomme.

---

## Teknisk forslag
Den eksisterende `UpdateDetails()` er beregnet til almindelig redigering af en gæst og følger valideringen i `GuestRules`.

Ved ikke om den eksisterende `UpdateDetails()` er egnet til anonymisering, da den er beregnet til almindelig redigering af en gæst og følger valideringen i `GuestRules`.

Der skal derfor afklares, hvordan anonymisering skal håndteres teknisk.

Følgende dele af systemet kan forventes at blive berørt:

| Område | Forventet ændring |
|---|---|
| `Guest` | Skal kunne udføre en særskilt anonymiseringsoperation |
| `GuestRules` | Almindelig oprettelse og redigering skal fortsat håndhæve de eksisterende regler |
| `GuestService` | Skal kunne vælge mellem fysisk sletning og anonymisering afhængigt af eksisterende bookinger |
| `IGuestRepository` | Den eksisterende relation til bookinger skal fortsat kunne kontrolleres |
| Tests | Der skal tilføjes tests af både fysisk sletning og anonymisering |

Den konkrete implementering fastlægges først, når anonymiseringsstrategien er godkendt.

---

## Åbne spørgsmål

Følgende punkter skal helst afklares med teamet, før anonymiseringsstrategien kan implementeres:

- Skal den foreslåede strategi med at bevare `Guest`-rækken og anonymisere gæsteoplysninger godkendes?
- Hvilke konkrete erstatningsværdier skal bruges for `FirstName`, `LastName`, `Email` og `PhoneNumber`?
- Skal `Country` bevares efter anonymisering, eller skal feltet også anonymiseres?
- Skal `PassportNumber` sættes til `null` ved anonymisering?
- Hvordan skal systemet markere, at en `Guest` er anonymiseret?
- Skal anonymisering kun ske, når en gæst beder om at få sine personoplysninger slettet, eller skal det også ske automatisk efter en bestemt periode?
- Hvor længe skal bookingdata og gæstens persondata opbevares? Retentionperioderne står stadig som **X** i D-05 og skal fastlægges.
