-- =====================================================================
-- Verifikation af exclusion constrainten (A-01, B-03)
-- =====================================================================
-- Kør EFTER seed_dev.sql:
--   docker exec -i nfhotel-postgres psql -U nfhotel -d nfhotel < db/constraint_check.sql
--
-- Dette er den vigtigste enkeltmekanisme i hele designet: den gør
-- dobbeltbooking fysisk umulig, også hvis to brugere trykker samtidig.
-- Det gamle WPF-system havde intet overlapstjek ved oprettelse overhovedet.
--
-- Fire tests. De tre første SKAL fejle, den fjerde SKAL lykkes.
-- =====================================================================

\set ON_ERROR_STOP off
\echo ''
\echo '=== TEST 1: overlap paa samme rum — FORVENTET: afvist ==='
-- Rum 1 er booket CURRENT_DATE..+3 (booking 1). Denne overlapper midt i.
INSERT INTO booking (start_date, end_date, status, room_id, guest_id)
VALUES (CURRENT_DATE + 1, CURRENT_DATE + 2, 0, 1, 3);

\echo ''
\echo '=== TEST 2: overlap paa nyt rum uden konflikt — FORVENTET: accepteret ==='
-- Rum 9 (204) har ingen bookinger.
INSERT INTO booking (start_date, end_date, status, room_id, guest_id)
VALUES (CURRENT_DATE + 1, CURRENT_DATE + 2, 0, 9, 3);

\echo ''
\echo '=== TEST 3: ankomst samme dag som en anden gaests afrejse — FORVENTET: accepteret ==='
-- Booking 1 paa rum 1 slutter CURRENT_DATE + 3. Halvaabent interval [) betyder
-- at en ny booking maa starte praecis der (BR-39).
INSERT INTO booking (start_date, end_date, status, room_id, guest_id)
VALUES (CURRENT_DATE + 3, CURRENT_DATE + 5, 0, 1, 4);

\echo ''
\echo '=== TEST 4: genudlejning efter TIDLIG UDTJEKNING — FORVENTET: accepteret ==='
-- Dette er A-01, hele pointen med effective_end_date.
-- Booking 6 paa rum 10 er booket til CURRENT_DATE + 4, men gaesten tjekkede ud i gaar.
-- Uden effective_end_date ville denne blive afvist, selv om rummet staar tomt.
INSERT INTO booking (start_date, end_date, status, room_id, guest_id)
VALUES (CURRENT_DATE, CURRENT_DATE + 3, 0, 10, 5);

\echo ''
\echo '=== TEST 5: overlap med en ANNULLERET booking — FORVENTET: accepteret ==='
-- Booking 7 paa rum 3 er Cancelled og blokerer derfor ikke.
INSERT INTO booking (start_date, end_date, status, room_id, guest_id)
VALUES (CURRENT_DATE + 1, CURRENT_DATE + 2, 0, 3, 6);

\echo ''
\echo '=== OPRYDNING: fjerner de testrækker der blev accepteret ==='
DELETE FROM booking WHERE guest_id IN (3,4,5,6) AND check_in_time IS NULL AND booking_id > 12;

\echo ''
\echo '=== RESULTAT ==='
\echo 'Test 1 skal have givet:  ERROR ... ex_booking_room_period'
\echo 'Test 2 til 5 skal alle have givet: INSERT 0 1'
\echo ''
\echo 'Fejler test 1 IKKE, er constrainten ikke aktiv — tjek at migrationen er koert.'
\echo 'Fejler test 4, er effective_end_date forkert — det er A-01 der er braekket.'
