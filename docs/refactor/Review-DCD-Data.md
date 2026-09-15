# Review — Jens' DCD for datadelen

> 2026-09-09. Gennemgang af udkastet holdt op mod den kode der allerede er bygget
> (`src/NFHotel.Domain/`) og mod ledgeren i `BusinessRules.md`.
>
> Kort version: **prismodellen er stærk og løser et problem vi havde parkeret.** Der er fire
> steder hvor udkastet og den byggede kode er uenige, og de skal afgøres før nogen skriver
> en migration — ikke bagefter.

---

## 1. Det der er stærkt

**`RoomPriceSnapshot` er den rigtige løsning på et problem vi har haft åbent siden Fase 0.**

Ledgerens BR-31 siger at omsætningen altid sættes til 0, fordi der ikke findes en pris nogen
steder i domænet. `SalesService` blev udeladt af samme grund, og `/admin/revenue` er en
placeholder i dag. En per-nat, uforanderlig kopi af prisen som den så ud ved bookingen løser
det ordentligt: omsætning kan afstemmes bagud selv når `RoomPrice` ændres senere, og
weekendtillæg falder naturligt ud af modellen.

Det er ikke bare "en prisklasse på værelset" — det er den version der holder til revision.
Behold den.

**`RoomPrice` med `validFrom`/`validUntil` + `PriceType`** er ligeledes rigtigt tænkt.
Temporal prissætning uden at overskrive historik.

**`CleaningTask` og `MaintenanceTask` med tildeling til `Staff`** giver mobilappen noget at
arbejde med som den nuværende model ikke har — se punkt 4, hvor de to skal forenes.

**`Guest.nationality` og `passport`** er relevante for et hotel i Cambodia, hvor
immigrationsindberetning er et reelt krav. Godt fanget.

---

## 2. Fire konflikter der skal afgøres

### K-1 · `boolean isCancelled` mod `BookingStatus` — vigtigst

Udkastet har `boolean isCancelled` plus `checkInAt`/`checkOutAt`.
Den byggede kode har en enum med fem tilstande: `Pending`, `Confirmed`, `CheckedIn`,
`CheckedOut`, `Cancelled` — og **18 statusovergangsregler** fra ledgeren, håndhævet i
domænet og dækket af 68 tests.

En boolean plus to nullable tidsstempler kan udtrykke *hvad der er sket*, men ikke *hvad der
må ske nu*. "Må denne booking tjekke ind?" bliver til en udledning fra tre felter i stedet
for ét spørgsmål til domænet — og det var præcis den fejl det gamle WPF-system lavede.

**Anbefaling:** behold `BookingStatus`-enummen. `isCancelled` er redundant (`Status ==
Cancelled`). `checkInAt`/`checkOutAt` bevares som tidsstempler, ikke som tilstandskilde.

### K-2 · UUID mod int

Udkastet bruger `UUID` som primærnøgle overalt. Den byggede kode bruger `int` identity, og
`BookingNumber` er afledt som `FLZ-{BookingId:D6}` — det nummer kunden får på sin kvittering
(BR-C-05).

Jens' `int BookingRef` ved siden af `UUID id` er reelt samme idé som vores bookingnummer, så
modellerne er ikke uforenelige. Men valget rammer hver eneste tabel, hver fremmednøgle og
den migration der allerede er kørt.

**Det er billigt at afgøre nu og dyrt om fjorten dage.** Argumenterne: UUID er bedre hvis
data skal kunne flettes fra flere kilder eller genereres klientside; int er lettere at læse i
logs og URL'er og fylder mindre i indekser. Begge dele virker.

### K-3 · To sandheder om rengøring

Den byggede kode har `Room.HousekeepingStatus` med seks tilstande og syv overgange
(BR-N-06) — designet ud fra kravet om at mobilappen skal styre rengøringsforløbet: daglig
rengøring, service, slutrengøring.

Udkastet har `CleaningTask` med sin egen `CleaningStatus`, tildelt en `Staff`.

**Hvis begge findes uden en regel om hvem der bestemmer, vil de før eller siden være
uenige.** Et værelse kan stå som `Clean` mens der ligger en åben `CleaningTask` på det.

To veje, vælg én:
- **(a) Opgaven er sandheden.** `Room.HousekeepingStatus` udledes af åbne opgaver. Renere
  datamodel, men rummets tilstand kræver et opslag hver gang.
- **(b) Rummets status er sandheden.** `CleaningTask` er arbejdsseddel og historik — hvem
  gjorde hvad hvornår — men ændrer ikke selv rummets tilstand uden at gå gennem
  domænemetoden.

**Anbefaling: (b).** Den bevarer de syv overgange og deres guards, og opgaverne bliver det
lag mobilappen tildeler og logger i. Men det er en beslutning, ikke en default.

### K-4 · `Room.variant : String` og de andre fritekstfelter

Vi lavede `RoomSize` om til en enum (`Single`, `Double`, `Suite`) netop fordi det gamle
systems fritekst-`RoomSize` var én af de 22 uoverensstemmelser Fase 0 fandt: ingen
CHECK-constraint, ingen whitelist, og en XAML-dropdown der var uenig med enummen.

Udkastets `String variant` genindfører det. Samme gælder `marketSegment` og `mealPlan`.

**Men der er et modargument værd at høre:** hvis prisen afhænger af varianten, og varianter
skal kunne tilføjes uden en kodeændring, er en **lookup-tabel** bedre end en enum. Det er
formentlig den rigtige løsning her — bare ikke en fri streng.

---

## 3. Tre ting udkastet mangler

**M-1 · Effektiv slutdato.**
Bookingen har `checkOutAt`, men intet fanger at en **tidlig udtjekning frigiver værelset**
for de resterende nætter. Den byggede kode har `EffectiveEndDate = GREATEST(StartDate + 1,
COALESCE(CheckOutDate, EndDate))` og en PostgreSQL exclusion constraint bygget på den.

Det er relevant for Jens' del: enhver ledighedsforespørgsel til prissætning og tilbud skal
bruge den effektive periode, ikke den bookede. Ellers tilbyder systemet ikke et værelse der
faktisk står tomt.

**M-2 · Belægning mod kapacitet.**
`adults`, `children`, `babies` er nye felter, og `Room.capacity` findes allerede. Men der
er ingen regel om at antal gæster ikke må overstige kapaciteten. Skal der være det, er det en
ny forretningsregel der skal i ledgeren som `BR-N-xx` — ikke noget der bare gælder implicit.

**M-3 · Hvad er `Guest.ic`?**
Feltet står uforklaret. Identity card? Hvis ja: det er persondata på linje med pasnummeret,
og D-04 (privacy by design) siger at pasnummer skal krypteres at-rest. Samme bør gælde `ic`.

**Konsekvens Jens skal kende:** krypterede kolonner kan ikke søges eller sorteres normalt.
Vores AES-GCM er randomiseret, så selv et lighedsopslag fejler. Skal man kunne slå en gæst op
på pasnummer, kræver det en anden løsning end den vi har i dag.

---

## 4. Hvad jeg foreslår der sker nu

1. **Jens og jeg afgør K-1 til K-4 sammen** — de tager tyve minutter og låser datamodellen.
2. **Prismodellen kommer ind som den er.** `RoomPrice`, `PriceList`, `PriceType` og
   `RoomPriceSnapshot` er gennemtænkt og løser BR-31. Den skal bare hægtes på den
   eksisterende `Booking` frem for at erstatte den.
3. **`Staff` afklares mod auth-beslutningen.** D-03 definerer fire roller
   (rengøringspersonale, servicetekniker, manager/ejer, gæst). Jens' `Staff.Role` er samme
   begreb. Spørgsmålet er om `Staff` *er* login-entiteten eller en profil ved siden af den —
   præcis samme spørgsmål som Q-61 stiller om `Guest`. Det hører hos Michael og Jakob, ikke
   i datamodellen alene.
4. **Alle nye regler i ledgeren** som `BR-N`-serien, så Fase 3's audit kan skelne "porteret
   fra det gamle system" fra "opfundet nu". Prisregler, belægningsregler og
   rengøringsopgave-regler er alle sammen nye.

---

## 5. Én ting værd at sige højt

Udkastet er tegnet ud fra datadelens behov, og det er sundt — det er sådan man opdager at
`RoomPriceSnapshot` skal findes. Men fire af de fem konflikter ovenfor opstår fordi
datamodellen og domænemodellen er tegnet hver for sig.

De skal ende som **én** model. Ellers får vi to sandheder om hvad en booking er, og det er
det problem hele refaktoreringen begyndte med.

---

# Del 2 — Fejl i selve datamodellen

> Tilføjet 2026-09-09. Denne del ser **kun** på udkastet som datamodel: hvad går galt når
> tingene skal gemmes, hvis vi kører 100 % efter DCD'en som den er tegnet? Konflikterne med
> den eksisterende kode i del 1 er holdt udenfor.
>
> 18 fund, sorteret efter hvornår de gør ondt.

---

## A · Går i stykker med det samme

**F-1 · `status = ToDo` findes ikke i nogen af de to enums.**
`CleaningTask` har `CleaningStatus status = ToDo`, men `CleaningStatus` er
`NOT_SCHEDULED, SCHEDULED, IN_PROGRESS, COMPLETED, REJECTED`.
`MaintenanceTask` har `MaintenanceStatus status = ToDo`, men enummen er
`PENDING, IN_PROGRESS, COMPLETED, CANCELLED, FAILED`.

Defaultværdien er ikke en gyldig værdi i typen. Begge steder. Det kompilerer ikke, og
kolonnen kan ikke få den default. Sandsynligvis en rest fra en tidligere version af enummen.

**F-2 · `RoomPriceSnapshot` har ingen primærnøgle.**
Alle andre klasser har `UUID id`. Snapshottet har `bookingId`, `roomPriceId` og `date` — tre
felter, ingen identitet. Som tegnet er det en tabel man ikke kan pege på eller opdatere en
enkelt række i.

Den naturlige nøgle er formentlig `(bookingId, date)`, og det er sandsynligvis det rigtige
valg — men det skal stå der, og så skal den også håndhæves (se F-11).

**F-3 · `RoomStatus` bruges, men er aldrig defineret.**
`Room.status : RoomStatus` refererer en enum der ikke findes i diagrammet, i modsætning til
`CleaningStatus`, `MaintenanceStatus` og `PriceType`, som alle er tegnet ud. Hvilke værdier
kan et værelse have?

---

## B · Multipliciteter der gør virkelige tilfælde ugemmelige

**F-4 · `Booking "1" — "1" Room  ' occupies` — et værelse kan kun bookes én gang. Nogensinde.**
Det er den alvorligste. Relationen siger 1-til-1 i begge ender. En hotelbooking er
mange-til-én: ét værelse har mange bookinger over tid.

Med den model kan hotellet gemme præcis lige så mange bookinger som det har værelser.
Skal være `Room "1" — "0..*" Booking`.

**F-5 · `Guest "1" — "1..*" Booking  ' makes` — en gæst kan ikke oprettes uden en booking.**
`1..*` betyder mindst én. Men gæster oprettes selvstændigt: en walk-in registreres i
receptionen før bookingen findes, og admin har en gæsteoversigt med oprettelse.

Skal være `0..*`.

**F-6 · Enhver opgave skal have en medarbejder tildelt.**
`CleaningTask "0..*" — "1" Staff` og tilsvarende for `MaintenanceTask`. Multipliciteten 1
gør tildeling obligatorisk.

Det modsiger `CleaningStatus.NOT_SCHEDULED` og `MaintenanceStatus.PENDING`: en opgave der
ikke er planlagt endnu har pr. definition ingen tildelt. Man kan ikke gemme "der skal gøres
rent på 204, vi ved ikke af hvem endnu".

Skal være `0..1`.

**F-7 · `CleaningTask.cleanStartAt` er ikke nullable.**
`LocalDateTime cleanStartAt` (uden `?`) mod `cleanEndAt ?`. Men en `NOT_SCHEDULED`- eller
`SCHEDULED`-opgave har ikke noget starttidspunkt endnu.

Enten skal feltet være nullable, eller også skal det betyde "planlagt starttidspunkt" —
og så mangler der et felt til det faktiske. Som det står, tvinges man til at gemme et
opdigtet tidsstempel.

---

## C · Modellen tillader data der modsiger sig selv

**F-8 · Intet forhindrer dobbeltbooking.**
Der er ingen constraint der forhindrer to bookinger på samme værelse i overlappende
perioder. Det er den vigtigste integritetsregel i hele systemet, og den kan ikke løses i
applikationskoden alene — to samtidige forespørgsler vil begge se et ledigt værelse.

Det hører i databasen. PostgreSQL kan gøre det med `btree_gist` og en exclusion constraint
på `(room_id, daterange)`.

**F-9 · To aktive priser på samme værelse er lovligt.**
`RoomPrice.validUntil ? ' null = currently active` plus `Room "1" — "0..*" RoomPrice`
betyder at intet forhindrer to rækker med `validUntil = null` på samme værelse.

Hvilken pris gælder så? Modellen kan ikke svare. Der mangler enten en unik constraint
(én aktiv pr. værelse pr. `priceType`) eller en exclusion constraint på perioden.

**F-10 · Når flere `PriceType` gælder samtidig, er der ingen regel for hvem der vinder.**
Noten siger eksplicit "Overlaps allowed via validFrom/validUntil". Men en lørdag i
højsæsonen med en aktiv kampagne matcher `BASE`, `WEEKEND`, `SEASONAL_HIGH` og `PROMO` på én
gang.

Der er ingen prioritet, ingen rækkefølge, ingen additiv/eksklusiv-markering. `description`-
eksemplet `"Base + Weekend Surcharge - $50"` antyder at de lægges sammen — men det er en
beregningsregel der kun findes som fritekst i en beskrivelse, ikke som data. Det kan ikke
genberegnes eller revideres.

**F-11 · Intet garanterer at der er én snapshot pr. nat.**
Snapshottet er per-nat, men uden nøgle på `(bookingId, date)` kan man gemme:
- to rækker for samme dato med forskellig pris
- en 5-nætters booking med 3 snapshots
- et snapshot med en dato uden for bookingens periode

Alle tre er tavse datafejl der først opdages når regnskabet ikke stemmer. Kræver unik nøgle
plus en check på at `date` ligger i `[startAt, endAt)`.

**F-12 · `PriceList.isDefault : Boolean` kan ikke udtrykke "præcis én".**
Kommentaren siger "One default per room/variant". En boolean kan ikke håndhæve det — intet
forhindrer nul defaults eller fem.

Kræver et partielt unikt indeks (`WHERE is_default`) eller en anden model, fx en peger fra
værelset til dets standardliste.

**F-13 · Tre veje mellem `Room` og `RoomPrice` — de kan modsige hinanden.**
Diagrammet har samtidig:
- `Room "1" — "0..*" RoomPrice` (has active pricing history)
- `Room "1" — "0..*" PriceList` (belongs to list)
- `RoomPrice "1" — "1" PriceList` (part of)

Det er en cyklus. Intet forhindrer en `RoomPrice` hvis `room` er 101, men hvis `PriceList`
hører til værelse 205. Redundante relationer der ikke er bundet sammen bliver altid
uenige før eller siden.

Dertil: `PriceList` med navnet `"2024 Summer Rates"` lyder som et **globalt** begreb, men
`Room "1" — "0..*" PriceList` gør hver liste til ét værelses ejendom. Så skal der oprettes
én "2024 Summer Rates" pr. værelse.

**F-14 · `MaintenanceTask` har både `note` og `updatedNote`.**
To fritekstfelter hvor det ene er en rettelse af det andet. Det kan rumme præcis én
opdatering — den anden gang nogen retter noten, er den første væk.

Enten er det en rigtig historik (egen tabel med tidsstempel og forfatter), eller også er det
ét felt. Som det står, er det et halvt journalsystem.

---

## D · Uspecificeret, men skal være det inden migrationen skrives

**F-15 · Ingen præcision på beløbene.**
`BigDecimal` er det rigtige valg — aldrig float til penge. Men skala og præcision skal pinnes
i skemaet (`numeric(10,2)` eller tilsvarende). Uden det får man
databasens default, og afrundinger opfører sig forskelligt fra miljø til miljø.

**F-16 · `Currency` gemmes pr. række uden at være bundet sammen.**
`RoomPrice.currency` og `RoomPriceSnapshot.currency` er begge `= "USD"`. Er systemet reelt
enkeltvaluta, er feltet støj. Er det ikke, mangler der en constraint på at `pricePerNight`,
`discountAmount` og `couponDiscount` i samme række har samme valuta — og at snapshottets
valuta matcher den `RoomPrice` den er kopieret fra.

**F-17 · `Booking.BookingRef : int` — ingen unikhed, ingen generator.**
Er det det menneskevendte referencenummer, skal det være unikt. Der står ikke hvordan det
tildeles: sekvens, tæller, eller afledt af noget. To bookinger med samme ref er lovligt som
modellen står.

**F-18 · Ingen vej til at slette persondata.**
`Guest` bærer `email`, `phoneNumber`, `nationality`, `passport` og `ic`. Beslutning D-05
siger at persondata skal kunne slettes på gæstens anmodning, mens bookingdata har sin egen
opbevaringsperiode.

Med `Guest "1" — "1..*" Booking` og ingen anonymiseringsmekanisme har man kun to valg når en
gæst beder om sletning: kaskadere bookingerne væk (regnskabet forsvinder) eller efterlade
forældreløse rækker. Der mangler et begreb for en anonymiseret gæst.

Dertil: `passport` og `ic` er persondata der skal krypteres at-rest. Krypterede kolonner kan
ikke søges eller sorteres — skal receptionen kunne slå en gæst op på pasnummer, er det en
selvstændig opgave der skal løses, ikke en detalje.

---

## Opsummering

| Kategori | Antal | Konsekvens |
|---|---|---|
| Går i stykker med det samme | 3 | Kompilerer ikke / kan ikke gemmes |
| Ugemmelige virkelige tilfælde | 4 | Kan ikke registrere walk-ins, uplanlagte opgaver, eller mere end én booking pr. værelse |
| Data der modsiger sig selv | 7 | Dobbeltbookinger, tvetydige priser, regnskab der ikke stemmer |
| Skal specificeres | 4 | Afrundingsfejl, dublerede referencer, GDPR uden udvej |

**F-4 er den der ville vælte demoen** — 1:1 mellem værelse og booking betyder at hotellet
kan gemme ti bookinger i alt.

**F-8, F-9 og F-11 er de farlige**, fordi de ikke fejler højlydt. Systemet kører videre og
gemmer data der er forkert, og det opdages først når nogen tæller efter.

Intet af det betyder at modellen er dårlig. Prismodellen er gennemtænkt, og opgavemodellen
løser noget vi manglede. Det er den slags fejl man finder ved at gennemgå et udkast — hvilket
er derfor det er et udkast.
