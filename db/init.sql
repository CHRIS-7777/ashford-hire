SET NOCOUNT ON;
GO
CREATE DATABASE AshfordHire;
GO
USE AshfordHire;
GO

CREATE TABLE Users (
    Id                INT IDENTITY(1,1) PRIMARY KEY,
    Username          NVARCHAR(50)  NOT NULL UNIQUE,
    Email             NVARCHAR(120) NOT NULL,
    FullName          NVARCHAR(100) NOT NULL,
    Phone             NVARCHAR(30)  NULL,
    Company           NVARCHAR(100) NULL,
    PasswordHash      NVARCHAR(64)  NOT NULL,
    Role              NVARCHAR(20)  NOT NULL DEFAULT 'customer',
    AccountType       NVARCHAR(20)  NOT NULL DEFAULT 'retail',
    SecurityQuestion  NVARCHAR(120) NULL,
    SecurityAnswer    NVARCHAR(120) NULL,
    RecoveryEmail     NVARCHAR(120) NULL,
    IsActive          BIT NOT NULL DEFAULT 1,
    CreatedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    FailedLogins      INT NOT NULL DEFAULT 0,
    LockedUntil       DATETIME2 NULL,
    WebhookUrl        NVARCHAR(400) NULL,
    MarketingOptIn    BIT NOT NULL DEFAULT 0,
    ShareWithPartners BIT NOT NULL DEFAULT 0
);

CREATE TABLE Depots (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    Code     NVARCHAR(10)  NOT NULL,
    Name     NVARCHAR(80)  NOT NULL,
    Region   NVARCHAR(60)  NOT NULL,
    Town     NVARCHAR(60)  NOT NULL,
    Postcode NVARCHAR(12)  NOT NULL,
    Phone    NVARCHAR(30)  NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE Equipment (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Sku         NVARCHAR(20)  NOT NULL UNIQUE,
    Slug        NVARCHAR(80)  NOT NULL UNIQUE,
    Name        NVARCHAR(100) NOT NULL,
    Category    NVARCHAR(50)  NOT NULL,
    DayRate     DECIMAL(10,2) NOT NULL,
    WeekRate    DECIMAL(10,2) NOT NULL,
    Deposit     DECIMAL(10,2) NOT NULL,
    Summary     NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NOT NULL,
    Specs       NVARCHAR(600) NOT NULL,
    ManualFile  NVARCHAR(120) NOT NULL,
    IsFeatured  BIT NOT NULL DEFAULT 0,
    FleetSize   INT NOT NULL DEFAULT 1
);

CREATE TABLE Hires (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    Ref              NVARCHAR(20)  NOT NULL,
    UserId           INT NOT NULL REFERENCES Users(Id),
    EquipmentId      INT NOT NULL REFERENCES Equipment(Id),
    StartDate        DATE NOT NULL,
    Days             INT NOT NULL,
    DeliveryPostcode NVARCHAR(12)  NOT NULL,
    SiteNotes        NVARCHAR(1000) NULL,
    Subtotal         DECIMAL(10,2) NOT NULL,
    DiscountPct      DECIMAL(5,2)  NOT NULL DEFAULT 0,
    Total            DECIMAL(10,2) NOT NULL,
    Status           NVARCHAR(20)  NOT NULL,
    CardLast4        NVARCHAR(4)   NOT NULL,
    CreatedAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Quotes (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Ref         NVARCHAR(20) NOT NULL,
    UserId      INT NOT NULL REFERENCES Users(Id),
    EquipmentId INT NOT NULL REFERENCES Equipment(Id),
    Days        INT NOT NULL,
    StartDate   DATE NOT NULL,
    Total       DECIMAL(10,2) NOT NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE OpsSecrets (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    Name      NVARCHAR(80)  NOT NULL,
    Value     NVARCHAR(200) NOT NULL,
    Notes     NVARCHAR(200) NULL,
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

-- ---------------------------------------------------------------- users
CREATE TABLE #seed_users (
    Username NVARCHAR(50), Pw VARCHAR(100), Email NVARCHAR(120), FullName NVARCHAR(100), Phone NVARCHAR(30),
    Company NVARCHAR(100), Role NVARCHAR(20), AccountType NVARCHAR(20), Q NVARCHAR(120), A NVARCHAR(120), CreatedDaysAgo INT
);
INSERT INTO #seed_users VALUES
 ('admin',      'trustno1',    'sarah.whitlock@ashfordhire.co.uk', 'Sarah Whitlock', '0113 496 0701', 'Ashford Tool & Plant Hire', 'admin',      'trade',  'What was the name of your first pet?', 'Biscuit', 1400),
 ('dispatch.qa','Yard!2024',   'qa.dispatch@ashfordhire.co.uk',    'QA Dispatch',    '0113 496 0702', 'Ashford Tool & Plant Hire', 'dispatcher', 'retail', 'What town were you born in?', 'Leeds', 400),
 ('k.osei',     'Forklift#88', 'kwame.osei@ashfordhire.co.uk',     'Kwame Osei',     '0113 496 0715', 'Ashford Tool & Plant Hire', 'dispatcher', 'retail', 'Which football team do you support?', 'Leeds United', 900),
 ('jordan',     'Summer2024',  'jordan.hale@halelandscapes.co.uk', 'Jordan Hale',    '07700 900412',  'Hale Landscapes',           'customer',   'retail', 'What was the make of your first van?', 'Transit', 120),
 ('megan.p',    'liverpool1',  'megan@priceandsons.co.uk',         'Megan Price',    '07700 900233',  'Price & Sons Builders',     'customer',   'trade',  'What was your mother''s maiden name?', 'Holloway', 600),
 ('tariq',      'Groundw0rks!','tariq@northerngroundworks.co.uk',  'Tariq Mahmood',  '07700 900871',  'Northern Groundworks Ltd',  'customer',   'trade',  'Which football team do you support?', 'Bradford City', 480),
 ('lucy.b',     'sunshine1',   'lucy.barker@outlook.example',      'Lucy Barker',    '07700 900119',  NULL,                        'customer',   'retail', 'What was the name of your first pet?', 'Pepper', 90),
 ('dev.patel',  'Patel2023!',  'dev@patelconstruction.co.uk',      'Dev Patel',      '07700 900554',  'Patel Construction',        'customer',   'trade',  'What town were you born in?', 'Leicester', 300),
 ('hannah.w',   'H4nnah-Gardens', 'hannah.wright@gmail.example',   'Hannah Wright',  '07700 900642',  NULL,                        'customer',   'retail', 'What was the name of your first pet?', 'Milo', 45),
 ('craig.m',    'Scaff0ld99',  'craig@mcbridescaffolding.co.uk',   'Craig McBride',  '07700 900378',  'McBride Scaffolding',       'customer',   'trade',  'What town were you born in?', 'Halifax', 210);

INSERT INTO Users (Username, Email, FullName, Phone, Company, PasswordHash, Role, AccountType, SecurityQuestion, SecurityAnswer, RecoveryEmail, IsActive, CreatedAt)
SELECT Username, Email, FullName, Phone, Company,
       LOWER(CONVERT(NVARCHAR(40), HASHBYTES('SHA1', Pw), 2)),
       Role, AccountType, Q, A, Email, 1, DATEADD(day, -CreatedDaysAgo, SYSUTCDATETIME())
FROM #seed_users;
DROP TABLE #seed_users;

UPDATE Users SET WebhookUrl = 'https://hooks.northerngroundworks.co.uk/ashford' WHERE Username = 'tariq';
UPDATE Users SET MarketingOptIn = 1 WHERE Username IN ('jordan', 'lucy.b', 'hannah.w');
GO

-- ---------------------------------------------------------------- depots
INSERT INTO Depots (Code, Name, Region, Town, Postcode, Phone, IsActive) VALUES
 ('LDS', 'Leeds Stourton (head office)', 'West Yorkshire',     'Leeds',      'LS10 1RW', '0113 496 0770', 1),
 ('BFD', 'Bradford Bowling Back Lane',   'West Yorkshire',     'Bradford',   'BD4 8SL',  '01274 496 210', 1),
 ('WKF', 'Wakefield Calder Park',        'West Yorkshire',     'Wakefield',  'WF2 7AW',  '01924 496 330', 1),
 ('YRK', 'York Clifton Moor',            'North Yorkshire',    'York',       'YO30 4XL', '01904 496 115', 1),
 ('HGT', 'Harrogate Hornbeam Park',      'North Yorkshire',    'Harrogate',  'HG2 8RE',  '01423 496 402', 1),
 ('SHF', 'Sheffield Attercliffe',        'South Yorkshire',    'Sheffield',  'S9 2RX',   '0114 496 0880', 1),
 ('DON', 'Doncaster Wheatley Hall Road', 'South Yorkshire',    'Doncaster',  'DN2 4LP',  '01302 496 517', 1),
 ('MAN', 'Manchester Trafford Park',     'Greater Manchester', 'Manchester', 'M17 1PG',  '0161 496 0925', 1),
 ('HUL', 'Hull Sutton Fields (closed)',  'East Yorkshire',     'Hull',       'HU7 0XL',  '01482 496 300', 0);
GO

-- ---------------------------------------------------------------- equipment
INSERT INTO Equipment (Sku, Slug, Name, Category, DayRate, WeekRate, Deposit, Summary, Description, Specs, ManualFile, IsFeatured, FleetSize) VALUES
 ('EXC-018', 'mini-excavator-1-8t', 'Mini Excavator 1.8t', 'Earthmoving', 165.00, 495.00, 250.00,
  'Zero-tail-swing mini digger for footings, drainage and landscaping.',
  'Our most popular machine. Expandable tracks squeeze through a standard garden gate, and the zero-tail-swing body is ideal on tight urban sites. Supplied with 300mm, 600mm and 1200mm grading buckets and a quick hitch.',
  'Operating weight: 1,800 kg|Max dig depth: 2.4 m|Track width: 980-1,320 mm|Engine: Kubota 3-cylinder diesel|Fuel: Red diesel, approx. 2 L/h', 'EXC-018-operator-guide.pdf', 1, 22),
 ('EXC-030', 'midi-excavator-3t', 'Midi Excavator 3t', 'Earthmoving', 210.00, 630.00, 350.00,
  'Bigger reach and lifting capacity for groundworks and utilities.',
  'A compact 3 tonne excavator with offset boom, blade and auxiliary hydraulics for breakers. Supplied with three buckets.',
  'Operating weight: 3,050 kg|Max dig depth: 2.9 m|Bucket breakout force: 26 kN|Engine: Yanmar 3TNV diesel', 'EXC-030-operator-guide.pdf', 0, 12),
 ('EXC-080', 'tracked-excavator-8t', 'Tracked Excavator 8t', 'Earthmoving', 320.00, 960.00, 600.00,
  'Production excavator for bulk dig, site clearance and drainage runs.',
  'Short-radius 8 tonne excavator with dozer blade, quick hitch and piped for breaker. Low-loader delivery required.',
  'Operating weight: 8,200 kg|Max dig depth: 4.6 m|Engine: Stage V diesel, 55 kW|Transport: Low-loader', 'EXC-080-operator-guide.pdf', 0, 6),
 ('DMP-010', 'high-tip-dumper-1t', 'High-Tip Dumper 1t', 'Earthmoving', 85.00, 255.00, 150.00,
  'Tracked high-tip dumper that unloads straight into a skip.',
  'Narrow tracked dumper with a 1.4 m tip height, ideal for moving spoil through gardens and tipping into skips.',
  'Payload: 1,000 kg|Tip height: 1.4 m|Width: 790 mm|Engine: Honda petrol', 'DMP-010-operator-guide.pdf', 1, 15),
 ('ACC-SC8', 'electric-scissor-lift-8m', 'Electric Scissor Lift 8m', 'Access Platforms', 140.00, 420.00, 200.00,
  'Quiet, zero-emission scissor lift for indoor fit-out and M&E work.',
  'Narrow 0.81 m chassis passes through standard doorways. Non-marking tyres and roll-out deck extension.',
  'Working height: 8 m|Platform capacity: 230 kg|Width: 0.81 m|Power: 24 V battery', 'ACC-SC8-operator-guide.pdf', 1, 18),
 ('ACC-BM12', 'cherry-picker-12m', 'Cherry Picker 12m', 'Access Platforms', 230.00, 690.00, 400.00,
  'Bi-energy articulated boom with up-and-over reach.',
  'Bi-energy articulated boom lift for working at height outdoors or inside. IPAF PAL card holders only.',
  'Working height: 12 m|Outreach: 6.2 m|Platform capacity: 200 kg|Power: Battery / diesel', 'ACC-BM12-operator-guide.pdf', 0, 8),
 ('ACC-TW5', 'aluminium-tower-5-2m', 'Aluminium Tower 5.2m', 'Access Platforms', 55.00, 165.00, 80.00,
  'Lightweight mobile access tower with stabilisers.',
  'Double-width tower supplied with stabilisers, toe boards and guard rails. PASMA-trained erectors recommended.',
  'Platform height: 5.2 m|Platform size: 2.5 x 1.45 m|Safe working load: 275 kg', 'ACC-TW5-operator-guide.pdf', 0, 30),
 ('PWR-G20', 'generator-20kva', 'Generator 20kVA', 'Power & Lighting', 95.00, 285.00, 200.00,
  'Super-silent diesel generator for site cabins and tools.',
  'Canopied 20 kVA generator with 3-phase and single-phase outlets and 24-hour fuel tank.',
  'Output: 20 kVA|Outlets: 400 V 3-phase, 230 V, 110 V|Noise: 65 dBA @ 7 m|Run time: 24 h', 'PWR-G20-operator-guide.pdf', 1, 14),
 ('PWR-LT4', 'led-lighting-tower', 'LED Lighting Tower', 'Power & Lighting', 70.00, 210.00, 150.00,
  'Hybrid LED tower lighting up to 4,000 square metres.',
  'Hybrid battery/diesel LED tower with a 9 m mast. Runs silently on battery for up to 10 hours.',
  'Mast height: 9 m|Lamps: 4 x 300 W LED|Coverage: 4,000 m2|Run time: 10 h battery', 'PWR-LT4-operator-guide.pdf', 0, 10),
 ('CMP-PLT', 'plate-compactor', 'Plate Compactor', 'Compaction', 38.00, 114.00, 60.00,
  'Forward plate compactor for sub-bases and block paving.',
  'Petrol forward plate with water tank and paving pad for laying block paving.',
  'Plate width: 500 mm|Centrifugal force: 15 kN|Engine: Honda GX160', 'CMP-PLT-operator-guide.pdf', 0, 40),
 ('CMP-RLR', 'twin-drum-roller-800mm', 'Twin Drum Roller 800mm', 'Compaction', 120.00, 360.00, 200.00,
  'Ride-on vibrating roller for tarmac and sub-base.',
  'Articulated 800 mm twin drum roller with water spray system.',
  'Drum width: 800 mm|Operating weight: 1,500 kg|Engine: Kubota diesel', 'CMP-RLR-operator-guide.pdf', 0, 7),
 ('CUT-SAW', 'petrol-cut-off-saw-350mm', 'Petrol Cut-off Saw 350mm', 'Concrete & Cutting', 32.00, 96.00, 50.00,
  'Handheld disc cutter for kerbs, slabs and block.',
  'Supplied with a diamond blade and water suppression kit. Blade wear is charged separately.',
  'Blade: 350 mm diamond|Cutting depth: 125 mm|Engine: 2-stroke petrol', 'CUT-SAW-operator-guide.pdf', 0, 35),
 ('CUT-MIX', 'cement-mixer-110v', 'Cement Mixer 110V', 'Concrete & Cutting', 24.00, 72.00, 40.00,
  'Site mixer for mortar, render and small pours.',
  '110 V tip-up mixer with stand. Transformer available on request.',
  'Drum: 134 L|Power: 110 V|Output: approx. 90 L per mix', 'CUT-MIX-operator-guide.pdf', 0, 45),
 ('LFT-TH7', 'telehandler-7m', 'Telehandler 7m', 'Lifting & Handling', 260.00, 780.00, 500.00,
  'Compact telescopic handler for loading out and placing materials.',
  'Compact 7 m telehandler with pallet forks. CPCS or NPORS ticket required.',
  'Lift height: 7 m|Max capacity: 3,000 kg|Engine: Stage V diesel', 'LFT-TH7-operator-guide.pdf', 1, 9),
 ('LFT-PAL', 'pallet-truck-2-5t', 'Pallet Truck 2.5t', 'Lifting & Handling', 18.00, 54.00, 30.00,
  'Hand pallet truck for moving palletised loads.',
  'Standard 2.5 tonne hand pallet truck with nylon wheels.',
  'Capacity: 2,500 kg|Fork length: 1,150 mm', 'LFT-PAL-operator-guide.pdf', 0, 60);
GO

-- ---------------------------------------------------------------- hires
CREATE TABLE #seed_hires (Ref NVARCHAR(20), Username NVARCHAR(50), Sku NVARCHAR(20), StartOff INT, Days INT, Pc NVARCHAR(12), Notes NVARCHAR(1000), Card4 NVARCHAR(4), CreatedOff INT);
INSERT INTO #seed_hires VALUES
 ('AH-40112', 'megan.p',   'EXC-018',  -16, 5,  'LS6 2QT',  'Gate code 4471. Ask for Dave in the site cabin.', '5454', -18),
 ('AH-40135', 'tariq',     'EXC-080',  -14, 10, 'BD3 9LN',  'Low-loader access from Leeds Old Road only.', '0041', -17),
 ('AH-40158', 'jordan',    'DMP-010',  -12, 3,  'LS17 6AJ', 'Side gate is 90cm wide. Please phone 30 min before arrival.', '4242', -14),
 ('AH-40171', 'dev.patel', 'LFT-TH7',  -11, 7,  'S3 8DN',   'Banksman on site 07:30-16:00.', '1881', -13),
 ('AH-40190', 'lucy.b',    'CUT-MIX',  -10, 2,  'YO24 1AB', 'Leave on the driveway if no one is in.', '1234', -12),
 ('AH-40213', 'megan.p',   'ACC-SC8',  -9,  5,  'LS1 4AP',  'Deliver to loading bay B, call Megan on arrival.', '5454', -11),
 ('AH-40229', 'craig.m',   'ACC-TW5',  -8,  14, 'HX1 1QG',  'Scaffold yard, use rear entrance.', '7710', -10),
 ('AH-40247', 'hannah.w',  'CMP-PLT',  -7,  2,  'HG1 2PW',  NULL, '9902', -9),
 ('AH-40266', 'tariq',     'EXC-030',  -6,  10, 'BD5 0HD',  'Site contact: Imran 07700 900872.', '0041', -8),
 ('AH-40281', 'jordan',    'EXC-018',  -4,  7,  'LS16 7PQ', 'Customer garden, access via back lane. Neighbour has a dog.', '4242', -6),
 ('AH-40302', 'dev.patel', 'PWR-G20',  -3,  14, 'S9 1XU',   'Connect to the welfare cabin. Fuel supplied by us.', '1881', -5),
 ('AH-40318', 'megan.p',   'PWR-LT4',  -2,  7,  'LS9 8EB',  'Night works - deliver before 3pm please.', '5454', -4),
 ('AH-40337', 'craig.m',   'LFT-PAL',  -1,  5,  'HX3 6AA',  NULL, '7710', -3),
 ('AH-40355', 'tariq',     'CMP-RLR',  0,   5,  'BD4 7TP',  'Tarmac crew starts 7am. Drop by the entrance.', '0041', -2),
 ('AH-40372', 'lucy.b',    'CUT-SAW',  0,   1,  'YO31 8EX', NULL, '1234', -2),
 ('AH-40391', 'dev.patel', 'ACC-BM12', 1,   5,  'S1 2HE',   'City centre - permit for Arundel Gate loading bay.', '1881', -1),
 ('AH-40408', 'jordan',    'CMP-PLT',  2,   2,  'LS17 6AJ', 'Same site as last time.', '4242', -1),
 ('AH-40426', 'megan.p',   'EXC-030',  3,   10, 'LS12 5LJ', 'Machine to the rear of plot 14.', '5454', 0),
 ('AH-40445', 'hannah.w',  'DMP-010',  4,   2,  'HG2 7SA',  NULL, '9902', 0),
 ('AH-40463', 'craig.m',   'ACC-SC8',  5,   3,  'M17 1WA',  'Warehouse unit 7, ask security for a pass.', '7710', 0),
 ('AH-40480', 'tariq',     'LFT-TH7',  7,   14, 'BD1 5RD',  'Large site - call Imran on arrival.', '0041', 0);

INSERT INTO Hires (Ref, UserId, EquipmentId, StartDate, Days, DeliveryPostcode, SiteNotes, Subtotal, DiscountPct, Total, Status, CardLast4, CreatedAt)
SELECT s.Ref, u.Id, e.Id,
       DATEADD(day, s.StartOff, CAST(SYSUTCDATETIME() AS date)), s.Days, s.Pc, s.Notes,
       p.Subtotal,
       CASE WHEN u.AccountType = 'trade' THEN 15 ELSE 0 END,
       CASE WHEN u.AccountType = 'trade' THEN ROUND(p.Subtotal * 0.85, 2) ELSE p.Subtotal END,
       CASE WHEN s.StartOff + s.Days - 1 < 0 THEN 'Completed'
            WHEN s.StartOff <= 0 THEN 'On hire'
            ELSE 'Confirmed' END,
       s.Card4,
       DATEADD(minute, 540 + (ABS(CHECKSUM(s.Ref)) % 420), DATEADD(day, s.CreatedOff, CAST(CAST(SYSUTCDATETIME() AS date) AS datetime2)))
FROM #seed_hires s
JOIN Users u ON u.Username = s.Username
JOIN Equipment e ON e.Sku = s.Sku
CROSS APPLY (SELECT (s.Days / 7) * e.WeekRate +
                    CASE WHEN (s.Days % 7) * e.DayRate < e.WeekRate THEN (s.Days % 7) * e.DayRate ELSE e.WeekRate END AS Subtotal) p
ORDER BY s.CreatedOff, s.Ref;
DROP TABLE #seed_hires;
GO

-- ---------------------------------------------------------------- quotes
INSERT INTO Quotes (Ref, UserId, EquipmentId, Days, StartDate, Total, CreatedAt)
SELECT 'Q-31877', u.Id, e.Id, 7, DATEADD(day, 9, CAST(SYSUTCDATETIME() AS date)), e.WeekRate, DATEADD(day, -2, SYSUTCDATETIME())
FROM Users u, Equipment e WHERE u.Username = 'jordan' AND e.Sku = 'EXC-030';
INSERT INTO Quotes (Ref, UserId, EquipmentId, Days, StartDate, Total, CreatedAt)
SELECT 'Q-31902', u.Id, e.Id, 14, DATEADD(day, 12, CAST(SYSUTCDATETIME() AS date)), e.WeekRate * 2, DATEADD(day, -1, SYSUTCDATETIME())
FROM Users u, Equipment e WHERE u.Username = 'megan.p' AND e.Sku = 'LFT-TH7';
GO

-- ---------------------------------------------------------------- ops
INSERT INTO OpsSecrets (Name, Value, Notes) VALUES
 ('portal_verification', 'FLAG{union_all_the_way_to_the_ops_table}', 'Quarterly audit marker'),
 ('traknet_backup_key',  'tnk_live_bk_7f3a9c21e5d84b6f',            'Backup telematics key, rotate after audit'),
 ('payments_merchant_id','ASHFORD-MID-0042871',                      'Card acquirer merchant ID'),
 ('smtp_relay_password', 'Relay-2024-Spring!',                       'mail.ashfordhire.co.uk relay user portal@');
GO

-- ---------------------------------------------------------------- logins
USE master;
GO
CREATE LOGIN ashford_app WITH PASSWORD = '$(APP_DB_PASSWORD)', CHECK_POLICY = OFF, DEFAULT_DATABASE = AshfordHire;
CREATE LOGIN ashford_ro  WITH PASSWORD = '$(RO_DB_PASSWORD)',  CHECK_POLICY = OFF, DEFAULT_DATABASE = AshfordHire;
GO
USE AshfordHire;
GO
CREATE USER ashford_app FOR LOGIN ashford_app;
ALTER ROLE db_datareader ADD MEMBER ashford_app;
ALTER ROLE db_datawriter ADD MEMBER ashford_app;
CREATE USER ashford_ro FOR LOGIN ashford_ro;
ALTER ROLE db_datareader ADD MEMBER ashford_ro;
GO
PRINT 'AshfordHire seeded';
GO
