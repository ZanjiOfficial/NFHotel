# RBAC — adgangskontrolmatrix

> Adgangskontrolmatrixen beskriver, hvilke roller der må tilgå forskellige funktioner og data i NFHotel.
> Matrixen fungerer som grundlag for den senere implementering af autentifikation og autorisation.

---

## Formål

Formålet med matrixen er at definere adgangsrettighederne for de forskellige roller i NFHotel, før de implementeres i systemet.

Matrixen skal gøre det tydeligt, hvilke funktioner og data de forskellige roller har behov for adgang til, og samtidig understøtte princippet om **least privilege** — en bruger skal kun have de rettigheder, der er nødvendige for brugerens opgaver.

Adgangskontrollen skal ikke kun håndhæves i brugergrænsefladen, men også i applikationslaget.

---

## Roller

| Rolle | Beskrivelse |
|---|---|
| **Guest** | Hotellets gæst. Login og autorisation for Guest implementeres i en senere fase. |
| **Receptionist** | Medarbejder der håndterer gæster og bookinger. |
| **Manager** | Medarbejder med udvidede administrative rettigheder. |
| **Housekeeping** | Medarbejder der håndterer rengøring og housekeeping-opgaver. |

---


## Adgangskontrolmatrix

| Funktion / data | Guest | Receptionist | Manager | Housekeeping |
|---|:---:|:---:|:---:|:---:|
| Opret booking | o | o | o | x |
| Ændre egen booking | o | o | o | x |
| Ændre andre gæsters booking | x | o | o | x |
| Se egne gæsteoplysninger | o | o | o | x |
| Se andre gæsters oplysninger | x | o | o | x |
| Se egne passportdata | ? | o | o | x |
| Se andre gæsters passportdata | x | o | o | x |
| Ændre priser/rabatter | x | x | o | x |
| Se housekeeping-information | x | o | o | o |
| Ændre housekeeping-status | x | x | o | o |

**Symboler:**

- `o` = adgang
- `x` = ingen adgang
- `?` = skal afklares

---

## Åbne spørgsmål

Følgende adgangsregler skal afklares med teamet/PO, før de implementeres:

- Skal en Guest kunne se egne passportdata efter upload?
- Hvilke bookingoplysninger har Housekeeping konkret behov for?
- Skal Housekeeping have en begrænset visning af bookingoplysninger frem for adgang til hele bookingen?
- Mangler der funktioner eller data i adgangskontrolmatrixen?

---

## Implementeringsnote

Matrixen beskriver den ønskede adgangskontrol og er ikke i sig selv en implementering.

Når adgangsreglerne er gennemgået og godkendt, kan dette bruges som grundlag for implementeringen af roller og autorisation i NFHotel.

Adgang til egne ressourcer kræver mere end en kontrol af brugerens rolle. Hvis en Guest eksempelvis må se sin egen booking, skal systemet også kontrollere, at den konkrete booking tilhører den autentificerede bruger.
