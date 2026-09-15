-- =====================================================================
-- NFHotel — testdata til udvikling
-- =====================================================================
-- Kør:
--   docker exec -i nfhotel-postgres psql -U nfhotel -d nfhotel < db/seed_dev.sql
--
-- Alle datoer er relative til CURRENT_DATE, så check-in og check-ud
-- altid kan afprøves uanset hvornår scriptet køres.
--
-- passport_number sættes til NULL. Feltet krypteres med AES-GCM i
-- applikationslaget (B-09), så en værdi indsat direkte i SQL ville
-- ikke kunne dekrypteres af appen. Opret en gæst via /gaester hvis
-- du vil se krypteringen virke.
--
-- Enum-værdier:
--   BookingStatus       0=Pending 1=Confirmed 2=CheckedIn 3=CheckedOut 4=Cancelled
--   RoomStatus          0=Available 1=OutOfService 2=Maintenance
--   HousekeepingStatus  0=Clean 1=DailyCleaningDue 2=DepartureCleaningDue
--                       3=CleaningInProgress 4=ServiceRequired 5=ServiceInProgress
--   RoomSize            0=Single 1=Double 2=Suite
-- =====================================================================

BEGIN;

TRUNCATE TABLE booking RESTART IDENTITY CASCADE;
TRUNCATE TABLE room    RESTART IDENTITY CASCADE;
TRUNCATE TABLE guest   RESTART IDENTITY CASCADE;

-- ---------------------------------------------------------------------
-- Rum: to etager, alle tre størrelser, alle tre bookbarhedsstatusser,
-- og hele rengøringsforløbet repræsenteret.
-- ---------------------------------------------------------------------
INSERT INTO room (room_number, floor, size, capacity, status, housekeeping_status) VALUES
  ('101', 1, 0, 1, 0, 0),   -- Single,  ledig, ren
  ('102', 1, 1, 2, 0, 1),   -- Double,  ledig, daglig rengøring mangler
  ('103', 1, 1, 2, 0, 2),   -- Double,  ledig, slutrengøring mangler
  ('104', 1, 0, 1, 0, 3),   -- Single,  ledig, rengøring i gang
  ('105', 1, 2, 4, 0, 0),   -- Suite,   ledig, ren
  ('201', 2, 1, 2, 0, 4),   -- Double,  ledig, service anmodet
  ('202', 2, 1, 3, 0, 5),   -- Double,  ledig, service i gang
  ('203', 2, 2, 4, 2, 0),   -- Suite,   vedligehold  → kan ikke bookes
  ('204', 2, 0, 1, 1, 0),   -- Single,  ude af drift → kan ikke bookes
  ('205', 2, 1, 2, 0, 0);   -- Double,  ledig, ren

-- ---------------------------------------------------------------------
-- Gæster
-- ---------------------------------------------------------------------
INSERT INTO guest (first_name, last_name, email, phone_number, country, passport_number) VALUES
  ('Anne',    'Sørensen',  'anne.sorensen@example.dk',   '+45 20 11 22 33', 'Danmark',      NULL),
  ('Bjørn',   'Lund',      'bjorn.lund@example.no',      '+47 90 11 22 33', 'Norge',        NULL),
  ('Clara',   'Nielsen',   'clara.nielsen@example.dk',   '+45 30 44 55 66', 'Danmark',      NULL),
  ('Dieter',  'Vogel',     'dieter.vogel@example.de',    '+49 151 2233445', 'Tyskland',     NULL),
  ('Elena',   'Rossi',     'elena.rossi@example.it',     '+39 340 1122334', 'Italien',      NULL),
  ('Frank',   'Jensen',    'frank.jensen@example.dk',    '+45 40 77 88 99', 'Danmark',      NULL),
  ('Greta',   'Andersson', 'greta.andersson@example.se', '+46 70 111 2233', 'Sverige',      NULL),
  ('Henrik',  'Poulsen',   'henrik.poulsen@example.dk',  '+45 50 12 34 56', 'Danmark',      NULL);

-- ---------------------------------------------------------------------
-- Bookinger — dækker alle fem statusser og de scenarier der er værd at se
-- ---------------------------------------------------------------------
INSERT INTO booking
  (start_date, end_date, check_in_time, check_out_time, check_out_date, status, room_id, guest_id)
VALUES
  -- 1. Pending, ankomst i dag → CHECK IN er mulig med det samme (BR-06)
  (CURRENT_DATE, CURRENT_DATE + 3, NULL, NULL, NULL, 0, 1, 1),

  -- 2. Confirmed, ankomst i dag → både bekræftet OG klar til check-in
  (CURRENT_DATE, CURRENT_DATE + 2, NULL, NULL, NULL, 1, 2, 2),

  -- 3. Pending, ankomst om en uge → CHECK IN skal være SPÆRRET (for tidligt)
  (CURRENT_DATE + 7, CURRENT_DATE + 10, NULL, NULL, NULL, 0, 5, 3),

  -- 4. CheckedIn siden i går → CHECK UD er mulig, CONFIRM/CANCEL spærret (BR-11)
  (CURRENT_DATE - 1, CURRENT_DATE + 2, now() - interval '1 day', NULL, NULL, 2, 6, 4),

  -- 5. CheckedOut normalt — rejste på afrejsedagen
  (CURRENT_DATE - 5, CURRENT_DATE - 2, now() - interval '5 days', now() - interval '2 days',
   CURRENT_DATE - 2, 3, 7, 5),

  -- 6. TIDLIG UDTJEKNING (A-01): booket til om 4 dage, men rejste i går.
  --    effective_end_date bliver i går → rum 10 er ledigt fra i dag.
  (CURRENT_DATE - 3, CURRENT_DATE + 4, now() - interval '3 days', now() - interval '1 day',
   CURRENT_DATE - 1, 3, 10, 6),

  -- 7. Cancelled — eneste status der frigiver rummet helt.
  --    Bemærk at den overlapper booking 8 på rum 3; det er lovligt netop fordi den er annulleret.
  (CURRENT_DATE + 1, CURRENT_DATE + 5, NULL, NULL, NULL, 4, 3, 7),

  -- 8. Pending på samme rum og periode som den annullerede ovenfor
  (CURRENT_DATE + 2, CURRENT_DATE + 6, NULL, NULL, NULL, 0, 3, 8),

  -- 9. Confirmed længere ude i fremtiden
  (CURRENT_DATE + 14, CURRENT_DATE + 18, NULL, NULL, NULL, 1, 1, 1),

  -- 10. Halvåbent interval: starter præcis den dag booking 9 slutter.
  --     Skal være lovligt — afrejse- og ankomstdag samme dag er ikke overlap (BR-39).
  (CURRENT_DATE + 18, CURRENT_DATE + 21, NULL, NULL, NULL, 0, 1, 2),

  -- 11. Historisk booking, sidste måned
  (CURRENT_DATE - 30, CURRENT_DATE - 27, now() - interval '30 days', now() - interval '27 days',
   CURRENT_DATE - 27, 3, 2, 3),

  -- 12. Pending på suite, ankomst i morgen
  (CURRENT_DATE + 1, CURRENT_DATE + 4, NULL, NULL, NULL, 0, 5, 4);

COMMIT;

-- ---------------------------------------------------------------------
-- Kontrol
-- ---------------------------------------------------------------------
SELECT 'rum' AS tabel, count(*) FROM room
UNION ALL SELECT 'gaester', count(*) FROM guest
UNION ALL SELECT 'bookinger', count(*) FROM booking;

-- Effektiv slutdato pr. booking. Kig især på booking 6:
-- end_date ligger 4 dage ude i fremtiden, men effective_end_date er i går.
SELECT booking_id, room_id, status, start_date, end_date, check_out_date, effective_end_date
FROM booking
ORDER BY booking_id;
