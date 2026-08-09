CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE TABLE "AuditLogs" (
        "Id" uuid NOT NULL,
        "UserId" character varying(450) NOT NULL,
        "Role" character varying(50),
        "EventType" character varying(50) NOT NULL,
        "TimestampUtc" timestamp with time zone NOT NULL,
        "SessionId" character varying(100),
        "IpAddress" character varying(45),
        "UserAgent" character varying(500),
        CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE TABLE "RefreshTokens" (
        "Id" uuid NOT NULL,
        "UserId" character varying(450) NOT NULL,
        "SessionId" character varying(100) NOT NULL,
        "TokenHash" character varying(450) NOT NULL,
        "IssuedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "RevokedAt" timestamp with time zone,
        "RevocationReason" character varying(100),
        "IssuedFromIpAddress" character varying(45),
        "IssuedFromUserAgent" character varying(500),
        "LastUsedIpAddress" character varying(45),
        "LastUsedAt" timestamp with time zone,
        "TokenFamily" character varying(450) NOT NULL,
        CONSTRAINT "PK_RefreshTokens" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE TABLE "Roles" (
        "Id" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Description" character varying(250),
        "CreatedDate" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_Roles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE TABLE "ServiceRequests" (
        "Id" uuid NOT NULL,
        "CustomerId" character varying(450) NOT NULL,
        "Title" text NOT NULL,
        "Description" text NOT NULL,
        "Category" text NOT NULL,
        "Location" text NOT NULL,
        "Latitude" double precision NOT NULL,
        "Longitude" double precision NOT NULL,
        "Status" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ServiceRequests" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE TABLE "Users" (
        "Id" uuid NOT NULL,
        "FirstName" character varying(100) NOT NULL,
        "LastName" character varying(100) NOT NULL,
        "Email" character varying(150),
        "NormalizedEmail" character varying(150),
        "PhoneNumber" character varying(20),
        "NormalizedPhoneNumber" character varying(20),
        "SecondaryPhoneNumber" character varying(20),
        "PasswordHash" character varying(500) NOT NULL,
        "IsEmailVerified" boolean NOT NULL DEFAULT FALSE,
        "IsPhoneVerified" boolean NOT NULL DEFAULT FALSE,
        "TwoFactorEnabled" boolean NOT NULL DEFAULT FALSE,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "AccessFailedCount" integer NOT NULL,
        "LockoutEndUtc" timestamp with time zone,
        "DateOfBirth" timestamp with time zone NOT NULL,
        "UserType" smallint NOT NULL DEFAULT 1,
        "GovIdFilePath" character varying(500),
        "IsKycSubmitted" boolean NOT NULL,
        "IsKycApproved" boolean NOT NULL,
        "KycApprovedByUserId" character varying(450),
        "KycApprovedOn" timestamp with time zone,
        "CreatedDate" timestamp with time zone NOT NULL DEFAULT (now()),
        "UpdatedDate" timestamp with time zone,
        CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE TABLE "Bids" (
        "Id" uuid NOT NULL,
        "ServiceRequestId" uuid NOT NULL,
        "ServiceProviderId" character varying(450) NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "ProposedDateTime" timestamp with time zone NOT NULL,
        "Message" text,
        "Status" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Bids" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Bids_ServiceRequests_ServiceRequestId" FOREIGN KEY ("ServiceRequestId") REFERENCES "ServiceRequests" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE TABLE "UserRoles" (
        "UserId" uuid NOT NULL,
        "RoleId" uuid NOT NULL,
        CONSTRAINT "PK_UserRoles" PRIMARY KEY ("UserId", "RoleId"),
        CONSTRAINT "FK_UserRoles_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id"),
        CONSTRAINT "FK_UserRoles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    INSERT INTO "Roles" ("Id", "CreatedDate", "Description", "Name")
    VALUES ('11111111-1111-1111-1111-111111111111', TIMESTAMPTZ '2026-08-06T00:00:00Z', 'Customer account role', 'User');
    INSERT INTO "Roles" ("Id", "CreatedDate", "Description", "Name")
    VALUES ('22222222-2222-2222-2222-222222222222', TIMESTAMPTZ '2026-08-06T00:00:00Z', 'Service provider account role', 'ServiceProvider');
    INSERT INTO "Roles" ("Id", "CreatedDate", "Description", "Name")
    VALUES ('33333333-3333-3333-3333-333333333333', TIMESTAMPTZ '2026-08-06T00:00:00Z', 'Dual customer and service provider account role', 'Both');
    INSERT INTO "Roles" ("Id", "CreatedDate", "Description", "Name")
    VALUES ('44444444-4444-4444-4444-444444444444', TIMESTAMPTZ '2026-08-06T00:00:00Z', 'Administrator account role', 'Admin');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE INDEX "IX_AuditLogs_SessionId" ON "AuditLogs" ("SessionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE INDEX "IX_AuditLogs_TimestampUtc" ON "AuditLogs" ("TimestampUtc" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE INDEX "IX_AuditLogs_UserId" ON "AuditLogs" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE INDEX "IX_Bids_ServiceProviderId" ON "Bids" ("ServiceProviderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE INDEX "IX_Bids_ServiceRequestId" ON "Bids" ("ServiceRequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE INDEX "IX_RefreshTokens_ExpiresAt" ON "RefreshTokens" ("ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE INDEX "IX_RefreshTokens_TokenFamily" ON "RefreshTokens" ("TokenFamily");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE UNIQUE INDEX "IX_RefreshTokens_TokenHash" ON "RefreshTokens" ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE INDEX "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE UNIQUE INDEX "UX_Roles_Name" ON "Roles" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE INDEX "IX_ServiceRequests_CustomerId" ON "ServiceRequests" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE INDEX "IX_UserRoles_RoleId" ON "UserRoles" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE INDEX "IX_Users_UserType" ON "Users" ("UserType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE UNIQUE INDEX "UX_Users_NormalizedEmail" ON "Users" ("NormalizedEmail");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    CREATE UNIQUE INDEX "UX_Users_NormalizedPhoneNumber" ON "Users" ("NormalizedPhoneNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260806183341_InitialPostgresCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260806183341_InitialPostgresCreate', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807071403_AddServiceProviderProfiles') THEN
    CREATE TABLE "ServiceProviderProfiles" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "DisplayName" character varying(150) NOT NULL,
        "BusinessName" character varying(150),
        "Bio" character varying(1000),
        "Skills" character varying(1000) NOT NULL,
        "PrimaryCategory" character varying(100) NOT NULL,
        "ServiceAreaCity" character varying(100) NOT NULL,
        "ServiceAreaState" character varying(100) NOT NULL,
        "ServiceAreaZone" character varying(100),
        "HourlyRate" numeric(18,2) NOT NULL,
        "IsAvailable" boolean NOT NULL,
        "Status" smallint NOT NULL,
        "IdentityVerificationSubmitted" boolean NOT NULL,
        "AddressVerificationSubmitted" boolean NOT NULL,
        "BackgroundCheckConsent" boolean NOT NULL,
        "SubmittedAt" timestamp with time zone,
        "ReviewedAt" timestamp with time zone,
        "ReviewedByUserId" character varying(450),
        "ReviewNotes" character varying(1000),
        "RejectionReason" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ServiceProviderProfiles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ServiceProviderProfiles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807071403_AddServiceProviderProfiles') THEN
    CREATE INDEX "IX_ServiceProviderProfiles_ServiceArea" ON "ServiceProviderProfiles" ("ServiceAreaState", "ServiceAreaCity");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807071403_AddServiceProviderProfiles') THEN
    CREATE INDEX "IX_ServiceProviderProfiles_Status" ON "ServiceProviderProfiles" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807071403_AddServiceProviderProfiles') THEN
    CREATE UNIQUE INDEX "UX_ServiceProviderProfiles_UserId" ON "ServiceProviderProfiles" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807071403_AddServiceProviderProfiles') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260807071403_AddServiceProviderProfiles', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    ALTER TABLE "ServiceRequests" ADD "PreferredStartAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    ALTER TABLE "ServiceRequests" ADD "Requirements" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    ALTER TABLE "ServiceRequests" ADD "ServiceCategoryId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    ALTER TABLE "ServiceRequests" ADD "ServiceZoneId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    ALTER TABLE "ServiceRequests" ADD "Urgency" smallint NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    CREATE TABLE "ServiceCategories" (
        "Id" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Slug" character varying(100) NOT NULL,
        "Description" character varying(500),
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "SortOrder" integer NOT NULL DEFAULT 0,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ServiceCategories" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    CREATE TABLE "ServiceZones" (
        "Id" uuid NOT NULL,
        "Country" character varying(100) NOT NULL,
        "State" character varying(100) NOT NULL,
        "City" character varying(100) NOT NULL,
        "ZoneName" character varying(100),
        "DisplayName" character varying(200) NOT NULL,
        "PinCodeRegion" character varying(50),
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "SortOrder" integer NOT NULL DEFAULT 0,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ServiceZones" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    INSERT INTO "ServiceCategories" ("Id", "CreatedAt", "Description", "IsActive", "Name", "Slug", "SortOrder", "UpdatedAt")
    VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1', TIMESTAMPTZ '2026-08-07T00:00:00Z', 'Leaks, taps, pipes, fittings, and water-flow issues', TRUE, 'Plumbing', 'plumbing', 10, NULL);
    INSERT INTO "ServiceCategories" ("Id", "CreatedAt", "Description", "IsActive", "Name", "Slug", "SortOrder", "UpdatedAt")
    VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2', TIMESTAMPTZ '2026-08-07T00:00:00Z', 'Wiring, fixtures, switchboards, fans, and basic electrical repairs', TRUE, 'Electrical', 'electrical', 20, NULL);
    INSERT INTO "ServiceCategories" ("Id", "CreatedAt", "Description", "IsActive", "Name", "Slug", "SortOrder", "UpdatedAt")
    VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3', TIMESTAMPTZ '2026-08-07T00:00:00Z', 'Home and small-office cleaning services', TRUE, 'Cleaning', 'cleaning', 30, NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    INSERT INTO "ServiceZones" ("Id", "City", "Country", "CreatedAt", "DisplayName", "IsActive", "PinCodeRegion", "SortOrder", "State", "UpdatedAt", "ZoneName")
    VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1', 'Kolkata', 'India', TIMESTAMPTZ '2026-08-07T00:00:00Z', 'Central Kolkata, Kolkata, West Bengal', TRUE, '7000xx', 10, 'West Bengal', NULL, 'Central Kolkata');
    INSERT INTO "ServiceZones" ("Id", "City", "Country", "CreatedAt", "DisplayName", "IsActive", "PinCodeRegion", "SortOrder", "State", "UpdatedAt", "ZoneName")
    VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2', 'Kolkata', 'India', TIMESTAMPTZ '2026-08-07T00:00:00Z', 'South Kolkata, Kolkata, West Bengal', TRUE, '7000xx', 20, 'West Bengal', NULL, 'South Kolkata');
    INSERT INTO "ServiceZones" ("Id", "City", "Country", "CreatedAt", "DisplayName", "IsActive", "PinCodeRegion", "SortOrder", "State", "UpdatedAt", "ZoneName")
    VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3', 'Kolkata', 'India', TIMESTAMPTZ '2026-08-07T00:00:00Z', 'North Kolkata, Kolkata, West Bengal', TRUE, '7000xx', 30, 'West Bengal', NULL, 'North Kolkata');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    CREATE INDEX "IX_ServiceRequests_ServiceCategoryId" ON "ServiceRequests" ("ServiceCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    CREATE INDEX "IX_ServiceRequests_ServiceZoneId" ON "ServiceRequests" ("ServiceZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    CREATE INDEX "IX_ServiceCategories_ActiveSort" ON "ServiceCategories" ("IsActive", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    CREATE UNIQUE INDEX "UX_ServiceCategories_Slug" ON "ServiceCategories" ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    CREATE INDEX "IX_ServiceZones_ActiveSort" ON "ServiceZones" ("IsActive", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    CREATE INDEX "IX_ServiceZones_Area" ON "ServiceZones" ("State", "City", "ZoneName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    ALTER TABLE "ServiceRequests" ADD CONSTRAINT "FK_ServiceRequests_ServiceCategories_ServiceCategoryId" FOREIGN KEY ("ServiceCategoryId") REFERENCES "ServiceCategories" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    ALTER TABLE "ServiceRequests" ADD CONSTRAINT "FK_ServiceRequests_ServiceZones_ServiceZoneId" FOREIGN KEY ("ServiceZoneId") REFERENCES "ServiceZones" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807073604_AddServiceCatalogAndStructuredRequests') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260807073604_AddServiceCatalogAndStructuredRequests', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807074732_AddProviderCoverageAndBidComparison') THEN
    ALTER TABLE "ServiceProviderProfiles" ADD "ServiceCategoryId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807074732_AddProviderCoverageAndBidComparison') THEN
    ALTER TABLE "ServiceProviderProfiles" ADD "ServiceZoneId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807074732_AddProviderCoverageAndBidComparison') THEN
    ALTER TABLE "Bids" ADD "EstimatedDurationMinutes" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807074732_AddProviderCoverageAndBidComparison') THEN
    CREATE INDEX "IX_ServiceProviderProfiles_ServiceCategoryId" ON "ServiceProviderProfiles" ("ServiceCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807074732_AddProviderCoverageAndBidComparison') THEN
    CREATE INDEX "IX_ServiceProviderProfiles_ServiceZoneId" ON "ServiceProviderProfiles" ("ServiceZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807074732_AddProviderCoverageAndBidComparison') THEN
    ALTER TABLE "ServiceProviderProfiles" ADD CONSTRAINT "FK_ServiceProviderProfiles_ServiceCategories_ServiceCategoryId" FOREIGN KEY ("ServiceCategoryId") REFERENCES "ServiceCategories" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807074732_AddProviderCoverageAndBidComparison') THEN
    ALTER TABLE "ServiceProviderProfiles" ADD CONSTRAINT "FK_ServiceProviderProfiles_ServiceZones_ServiceZoneId" FOREIGN KEY ("ServiceZoneId") REFERENCES "ServiceZones" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807074732_AddProviderCoverageAndBidComparison') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260807074732_AddProviderCoverageAndBidComparison', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807080237_AddServiceOrderLifecycle') THEN
    CREATE TABLE "ServiceOrders" (
        "Id" uuid NOT NULL,
        "ServiceRequestId" uuid NOT NULL,
        "AcceptedBidId" uuid NOT NULL,
        "CustomerId" character varying(450) NOT NULL,
        "ProviderId" character varying(450) NOT NULL,
        "AgreedAmount" numeric(18,2) NOT NULL,
        "ScheduledStartAt" timestamp with time zone NOT NULL,
        "EstimatedDurationMinutes" integer,
        "Status" smallint NOT NULL DEFAULT 1,
        "StartedAt" timestamp with time zone,
        "ProviderCompletedAt" timestamp with time zone,
        "CompletedAt" timestamp with time zone,
        "CancelledAt" timestamp with time zone,
        "CancelledByUserId" character varying(450),
        "CancellationReason" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ServiceOrders" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ServiceOrders_Bids_AcceptedBidId" FOREIGN KEY ("AcceptedBidId") REFERENCES "Bids" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ServiceOrders_ServiceRequests_ServiceRequestId" FOREIGN KEY ("ServiceRequestId") REFERENCES "ServiceRequests" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807080237_AddServiceOrderLifecycle') THEN
    CREATE INDEX "IX_ServiceOrders_CustomerId" ON "ServiceOrders" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807080237_AddServiceOrderLifecycle') THEN
    CREATE INDEX "IX_ServiceOrders_ProviderId" ON "ServiceOrders" ("ProviderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807080237_AddServiceOrderLifecycle') THEN
    CREATE INDEX "IX_ServiceOrders_Status" ON "ServiceOrders" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807080237_AddServiceOrderLifecycle') THEN
    CREATE UNIQUE INDEX "UX_ServiceOrders_AcceptedBidId" ON "ServiceOrders" ("AcceptedBidId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807080237_AddServiceOrderLifecycle') THEN
    CREATE UNIQUE INDEX "UX_ServiceOrders_ServiceRequestId" ON "ServiceOrders" ("ServiceRequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807080237_AddServiceOrderLifecycle') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260807080237_AddServiceOrderLifecycle', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807081553_AddServiceOrderCommunicationAndNotifications') THEN
    CREATE TABLE "ServiceOrderMessages" (
        "Id" uuid NOT NULL,
        "ServiceOrderId" uuid NOT NULL,
        "SenderUserId" character varying(450) NOT NULL,
        "RecipientUserId" character varying(450) NOT NULL,
        "Body" character varying(2000) NOT NULL,
        "ReadAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ServiceOrderMessages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ServiceOrderMessages_ServiceOrders_ServiceOrderId" FOREIGN KEY ("ServiceOrderId") REFERENCES "ServiceOrders" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807081553_AddServiceOrderCommunicationAndNotifications') THEN
    CREATE TABLE "UserNotifications" (
        "Id" uuid NOT NULL,
        "UserId" character varying(450) NOT NULL,
        "Type" smallint NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Message" character varying(1000) NOT NULL,
        "ServiceOrderId" uuid,
        "ServiceRequestId" uuid,
        "BidId" uuid,
        "ServiceOrderMessageId" uuid,
        "IsRead" boolean NOT NULL DEFAULT FALSE,
        "ReadAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_UserNotifications" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807081553_AddServiceOrderCommunicationAndNotifications') THEN
    CREATE INDEX "IX_ServiceOrderMessages_OrderCreated" ON "ServiceOrderMessages" ("ServiceOrderId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807081553_AddServiceOrderCommunicationAndNotifications') THEN
    CREATE INDEX "IX_ServiceOrderMessages_RecipientUserId" ON "ServiceOrderMessages" ("RecipientUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807081553_AddServiceOrderCommunicationAndNotifications') THEN
    CREATE INDEX "IX_UserNotifications_ServiceOrderId" ON "UserNotifications" ("ServiceOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807081553_AddServiceOrderCommunicationAndNotifications') THEN
    CREATE INDEX "IX_UserNotifications_UserReadCreated" ON "UserNotifications" ("UserId", "IsRead", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807081553_AddServiceOrderCommunicationAndNotifications') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260807081553_AddServiceOrderCommunicationAndNotifications', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807083716_AddPaymentsAndReviews') THEN
    ALTER TABLE "ServiceProviderProfiles" ADD "AverageRating" numeric(3,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807083716_AddPaymentsAndReviews') THEN
    ALTER TABLE "ServiceProviderProfiles" ADD "ReviewCount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807083716_AddPaymentsAndReviews') THEN
    CREATE TABLE "ServiceOrderPayments" (
        "Id" uuid NOT NULL,
        "ServiceOrderId" uuid NOT NULL,
        "CustomerId" character varying(450) NOT NULL,
        "ProviderId" character varying(450) NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "Method" smallint NOT NULL,
        "Status" smallint NOT NULL,
        "ReferenceNumber" character varying(200),
        "Notes" character varying(1000),
        "RecordedByUserId" character varying(450) NOT NULL,
        "RecordedAt" timestamp with time zone NOT NULL,
        "ReleasedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ServiceOrderPayments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ServiceOrderPayments_ServiceOrders_ServiceOrderId" FOREIGN KEY ("ServiceOrderId") REFERENCES "ServiceOrders" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807083716_AddPaymentsAndReviews') THEN
    CREATE TABLE "ServiceOrderReviews" (
        "Id" uuid NOT NULL,
        "ServiceOrderId" uuid NOT NULL,
        "CustomerId" character varying(450) NOT NULL,
        "ProviderId" character varying(450) NOT NULL,
        "Rating" integer NOT NULL,
        "Feedback" character varying(2000),
        "IsHidden" boolean NOT NULL DEFAULT FALSE,
        "ModerationNotes" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ServiceOrderReviews" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ServiceOrderReviews_ServiceOrders_ServiceOrderId" FOREIGN KEY ("ServiceOrderId") REFERENCES "ServiceOrders" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807083716_AddPaymentsAndReviews') THEN
    CREATE INDEX "IX_ServiceOrderPayments_ProviderStatus" ON "ServiceOrderPayments" ("ProviderId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807083716_AddPaymentsAndReviews') THEN
    CREATE UNIQUE INDEX "UX_ServiceOrderPayments_ServiceOrderId" ON "ServiceOrderPayments" ("ServiceOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807083716_AddPaymentsAndReviews') THEN
    CREATE INDEX "IX_ServiceOrderReviews_ProviderVisibleCreated" ON "ServiceOrderReviews" ("ProviderId", "IsHidden", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807083716_AddPaymentsAndReviews') THEN
    CREATE UNIQUE INDEX "UX_ServiceOrderReviews_ServiceOrderId" ON "ServiceOrderReviews" ("ServiceOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807083716_AddPaymentsAndReviews') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260807083716_AddPaymentsAndReviews', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807090523_AddServiceOrderAuditAndReviewModeration') THEN
    CREATE TABLE "ServiceOrderAuditEvents" (
        "Id" uuid NOT NULL,
        "ServiceOrderId" uuid NOT NULL,
        "ServiceRequestId" uuid NOT NULL,
        "ActorUserId" character varying(450) NOT NULL,
        "ActorRole" character varying(50) NOT NULL,
        "EventType" character varying(100) NOT NULL,
        "FromStatus" character varying(100),
        "ToStatus" character varying(100),
        "Details" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ServiceOrderAuditEvents" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ServiceOrderAuditEvents_ServiceOrders_ServiceOrderId" FOREIGN KEY ("ServiceOrderId") REFERENCES "ServiceOrders" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807090523_AddServiceOrderAuditAndReviewModeration') THEN
    CREATE INDEX "IX_ServiceOrderAuditEvents_ActorUserId" ON "ServiceOrderAuditEvents" ("ActorUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807090523_AddServiceOrderAuditAndReviewModeration') THEN
    CREATE INDEX "IX_ServiceOrderAuditEvents_OrderCreated" ON "ServiceOrderAuditEvents" ("ServiceOrderId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807090523_AddServiceOrderAuditAndReviewModeration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260807090523_AddServiceOrderAuditAndReviewModeration', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807093524_AddFixedPriceServicePackages') THEN
    ALTER TABLE "ServiceOrders" ALTER COLUMN "AcceptedBidId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807093524_AddFixedPriceServicePackages') THEN
    ALTER TABLE "ServiceOrders" ADD "ServicePackageId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807093524_AddFixedPriceServicePackages') THEN
    CREATE TABLE "ServicePackages" (
        "Id" uuid NOT NULL,
        "ProviderId" character varying(450) NOT NULL,
        "ServiceCategoryId" uuid NOT NULL,
        "ServiceZoneId" uuid,
        "Title" character varying(150) NOT NULL,
        "Description" character varying(1000) NOT NULL,
        "Price" numeric(18,2) NOT NULL,
        "EstimatedDurationMinutes" integer,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ServicePackages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ServicePackages_ServiceCategories_ServiceCategoryId" FOREIGN KEY ("ServiceCategoryId") REFERENCES "ServiceCategories" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ServicePackages_ServiceZones_ServiceZoneId" FOREIGN KEY ("ServiceZoneId") REFERENCES "ServiceZones" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807093524_AddFixedPriceServicePackages') THEN
    CREATE INDEX "IX_ServiceOrders_ServicePackageId" ON "ServiceOrders" ("ServicePackageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807093524_AddFixedPriceServicePackages') THEN
    CREATE INDEX "IX_ServicePackages_ActiveCategoryZone" ON "ServicePackages" ("IsActive", "ServiceCategoryId", "ServiceZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807093524_AddFixedPriceServicePackages') THEN
    CREATE INDEX "IX_ServicePackages_ProviderId" ON "ServicePackages" ("ProviderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807093524_AddFixedPriceServicePackages') THEN
    CREATE INDEX "IX_ServicePackages_ServiceCategoryId" ON "ServicePackages" ("ServiceCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807093524_AddFixedPriceServicePackages') THEN
    CREATE INDEX "IX_ServicePackages_ServiceZoneId" ON "ServicePackages" ("ServiceZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807093524_AddFixedPriceServicePackages') THEN
    ALTER TABLE "ServiceOrders" ADD CONSTRAINT "FK_ServiceOrders_ServicePackages_ServicePackageId" FOREIGN KEY ("ServicePackageId") REFERENCES "ServicePackages" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807093524_AddFixedPriceServicePackages') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260807093524_AddFixedPriceServicePackages', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    CREATE TABLE "ProductCategories" (
        "Id" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Slug" character varying(100) NOT NULL,
        "Description" character varying(500),
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "SortOrder" integer NOT NULL DEFAULT 0,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ProductCategories" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    CREATE TABLE "SellerProfiles" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "StoreName" character varying(150) NOT NULL,
        "BusinessName" character varying(150),
        "Description" character varying(1000),
        "Gstin" character varying(30),
        "PickupAddress" character varying(500) NOT NULL,
        "City" character varying(100) NOT NULL,
        "State" character varying(100) NOT NULL,
        "ServiceZoneId" uuid,
        "IdentityVerificationSubmitted" boolean NOT NULL,
        "AddressVerificationSubmitted" boolean NOT NULL,
        "BusinessVerificationSubmitted" boolean NOT NULL,
        "Status" smallint NOT NULL,
        "SubmittedAt" timestamp with time zone,
        "ReviewedAt" timestamp with time zone,
        "ReviewedByUserId" character varying(450),
        "ReviewNotes" character varying(1000),
        "RejectionReason" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_SellerProfiles" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_SellerProfiles_UserId" UNIQUE ("UserId"),
        CONSTRAINT "FK_SellerProfiles_ServiceZones_ServiceZoneId" FOREIGN KEY ("ServiceZoneId") REFERENCES "ServiceZones" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_SellerProfiles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    CREATE TABLE "ProductListings" (
        "Id" uuid NOT NULL,
        "SellerId" uuid NOT NULL,
        "ProductCategoryId" uuid NOT NULL,
        "ServiceZoneId" uuid,
        "Title" character varying(150) NOT NULL,
        "Description" character varying(1000) NOT NULL,
        "Price" numeric(18,2) NOT NULL,
        "StockQuantity" integer NOT NULL,
        "ImageUrl" character varying(500),
        "Status" smallint NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ProductListings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ProductListings_ProductCategories_ProductCategoryId" FOREIGN KEY ("ProductCategoryId") REFERENCES "ProductCategories" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ProductListings_SellerProfiles_SellerId" FOREIGN KEY ("SellerId") REFERENCES "SellerProfiles" ("UserId") ON DELETE CASCADE,
        CONSTRAINT "FK_ProductListings_ServiceZones_ServiceZoneId" FOREIGN KEY ("ServiceZoneId") REFERENCES "ServiceZones" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    INSERT INTO "ProductCategories" ("Id", "CreatedAt", "Description", "IsActive", "Name", "Slug", "SortOrder", "UpdatedAt")
    VALUES ('cccccccc-cccc-cccc-cccc-ccccccccccc1', TIMESTAMPTZ '2026-08-07T00:00:00Z', 'Everyday household products for local buyers', TRUE, 'Home Essentials', 'home-essentials', 10, NULL);
    INSERT INTO "ProductCategories" ("Id", "CreatedAt", "Description", "IsActive", "Name", "Slug", "SortOrder", "UpdatedAt")
    VALUES ('cccccccc-cccc-cccc-cccc-ccccccccccc2', TIMESTAMPTZ '2026-08-07T00:00:00Z', 'Tools, fittings, and basic hardware supplies', TRUE, 'Tools And Hardware', 'tools-and-hardware', 20, NULL);
    INSERT INTO "ProductCategories" ("Id", "CreatedAt", "Description", "IsActive", "Name", "Slug", "SortOrder", "UpdatedAt")
    VALUES ('cccccccc-cccc-cccc-cccc-ccccccccccc3', TIMESTAMPTZ '2026-08-07T00:00:00Z', 'Small electronics and accessories', TRUE, 'Electronics', 'electronics', 30, NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    CREATE INDEX "IX_ProductCategories_ActiveSort" ON "ProductCategories" ("IsActive", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    CREATE UNIQUE INDEX "UX_ProductCategories_Slug" ON "ProductCategories" ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    CREATE INDEX "IX_ProductListings_ProductCategoryId" ON "ProductListings" ("ProductCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    CREATE INDEX "IX_ProductListings_SellerId" ON "ProductListings" ("SellerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    CREATE INDEX "IX_ProductListings_ServiceZoneId" ON "ProductListings" ("ServiceZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    CREATE INDEX "IX_ProductListings_StatusCategoryZone" ON "ProductListings" ("Status", "ProductCategoryId", "ServiceZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    CREATE INDEX "IX_SellerProfiles_Area" ON "SellerProfiles" ("State", "City");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    CREATE INDEX "IX_SellerProfiles_ServiceZoneId" ON "SellerProfiles" ("ServiceZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    CREATE INDEX "IX_SellerProfiles_Status" ON "SellerProfiles" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    CREATE UNIQUE INDEX "UX_SellerProfiles_UserId" ON "SellerProfiles" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807102530_AddSellerOnboardingAndProductMarketplace') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260807102530_AddSellerOnboardingAndProductMarketplace', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807105120_AddUsedProductInspectionGuidance') THEN
    ALTER TABLE "ProductListings" ADD "Condition" smallint NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807105120_AddUsedProductInspectionGuidance') THEN
    ALTER TABLE "ProductListings" ADD "ConditionNotes" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807105120_AddUsedProductInspectionGuidance') THEN
    ALTER TABLE "ProductListings" ADD "HasOriginalBill" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807105120_AddUsedProductInspectionGuidance') THEN
    ALTER TABLE "ProductListings" ADD "HasWarranty" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807105120_AddUsedProductInspectionGuidance') THEN
    ALTER TABLE "ProductListings" ADD "InspectionChecklist" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807105120_AddUsedProductInspectionGuidance') THEN
    ALTER TABLE "ProductListings" ADD "PurchaseYear" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807105120_AddUsedProductInspectionGuidance') THEN
    CREATE TABLE "ProductInspectionPrompts" (
        "Id" uuid NOT NULL,
        "ProductCategoryId" uuid NOT NULL,
        "Prompt" character varying(300) NOT NULL,
        "SortOrder" integer NOT NULL DEFAULT 0,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ProductInspectionPrompts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ProductInspectionPrompts_ProductCategories_ProductCategoryId" FOREIGN KEY ("ProductCategoryId") REFERENCES "ProductCategories" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807105120_AddUsedProductInspectionGuidance') THEN
    INSERT INTO "ProductInspectionPrompts" ("Id", "CreatedAt", "IsActive", "ProductCategoryId", "Prompt", "SortOrder", "UpdatedAt")
    VALUES ('dddddddd-dddd-dddd-dddd-ddddddddddd1', TIMESTAMPTZ '2026-08-07T00:00:00Z', TRUE, 'cccccccc-cccc-cccc-cccc-ccccccccccc1', 'Check visible wear, stains, cracks, missing parts, and whether the product has been cleaned before pickup.', 10, NULL);
    INSERT INTO "ProductInspectionPrompts" ("Id", "CreatedAt", "IsActive", "ProductCategoryId", "Prompt", "SortOrder", "UpdatedAt")
    VALUES ('dddddddd-dddd-dddd-dddd-ddddddddddd2', TIMESTAMPTZ '2026-08-07T00:00:00Z', TRUE, 'cccccccc-cccc-cccc-cccc-ccccccccccc2', 'Check rust, grip condition, moving parts, safety guards, serial/model labels, and included accessories.', 10, NULL);
    INSERT INTO "ProductInspectionPrompts" ("Id", "CreatedAt", "IsActive", "ProductCategoryId", "Prompt", "SortOrder", "UpdatedAt")
    VALUES ('dddddddd-dddd-dddd-dddd-ddddddddddd3', TIMESTAMPTZ '2026-08-07T00:00:00Z', TRUE, 'cccccccc-cccc-cccc-cccc-ccccccccccc3', 'Power on the device, check battery/charging, ports, display, buttons, invoice/warranty status, and reset/lock status.', 10, NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807105120_AddUsedProductInspectionGuidance') THEN
    CREATE INDEX "IX_ProductListings_StatusConditionCategoryZone" ON "ProductListings" ("Status", "Condition", "ProductCategoryId", "ServiceZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807105120_AddUsedProductInspectionGuidance') THEN
    CREATE INDEX "IX_ProductInspectionPrompts_CategoryActiveSort" ON "ProductInspectionPrompts" ("ProductCategoryId", "IsActive", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807105120_AddUsedProductInspectionGuidance') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260807105120_AddUsedProductInspectionGuidance', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807115847_AddProductDeliveryOrders') THEN
    ALTER TABLE "UserNotifications" ADD "ProductDeliveryOrderId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807115847_AddProductDeliveryOrders') THEN
    CREATE TABLE "ProductDeliveryOrders" (
        "Id" uuid NOT NULL,
        "ProductListingId" uuid NOT NULL,
        "SellerId" uuid NOT NULL,
        "BuyerId" character varying(450) NOT NULL,
        "Quantity" integer NOT NULL,
        "UnitPrice" numeric(18,2) NOT NULL,
        "TotalPrice" numeric(18,2) NOT NULL,
        "Status" smallint NOT NULL DEFAULT 1,
        "DeliveryRecipientName" character varying(150) NOT NULL,
        "DeliveryPhoneNumber" character varying(20) NOT NULL,
        "DeliveryAddress" character varying(500) NOT NULL,
        "DeliveryCity" character varying(100) NOT NULL,
        "DeliveryState" character varying(100) NOT NULL,
        "ServiceZoneId" uuid,
        "BuyerNotes" character varying(1000),
        "CancellationReason" character varying(1000),
        "CancelledByUserId" character varying(450),
        "ConfirmedAt" timestamp with time zone,
        "ReadyForPickupAt" timestamp with time zone,
        "OutForDeliveryAt" timestamp with time zone,
        "DeliveredAt" timestamp with time zone,
        "CancelledAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ProductDeliveryOrders" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ProductDeliveryOrders_ProductListings_ProductListingId" FOREIGN KEY ("ProductListingId") REFERENCES "ProductListings" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ProductDeliveryOrders_SellerProfiles_SellerId" FOREIGN KEY ("SellerId") REFERENCES "SellerProfiles" ("UserId") ON DELETE RESTRICT,
        CONSTRAINT "FK_ProductDeliveryOrders_ServiceZones_ServiceZoneId" FOREIGN KEY ("ServiceZoneId") REFERENCES "ServiceZones" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807115847_AddProductDeliveryOrders') THEN
    CREATE INDEX "IX_UserNotifications_ProductDeliveryOrderId" ON "UserNotifications" ("ProductDeliveryOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807115847_AddProductDeliveryOrders') THEN
    CREATE INDEX "IX_ProductDeliveryOrders_BuyerStatusCreated" ON "ProductDeliveryOrders" ("BuyerId", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807115847_AddProductDeliveryOrders') THEN
    CREATE INDEX "IX_ProductDeliveryOrders_ProductListingId" ON "ProductDeliveryOrders" ("ProductListingId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807115847_AddProductDeliveryOrders') THEN
    CREATE INDEX "IX_ProductDeliveryOrders_SellerStatusCreated" ON "ProductDeliveryOrders" ("SellerId", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807115847_AddProductDeliveryOrders') THEN
    CREATE INDEX "IX_ProductDeliveryOrders_ServiceZoneId" ON "ProductDeliveryOrders" ("ServiceZoneId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807115847_AddProductDeliveryOrders') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260807115847_AddProductDeliveryOrders', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    ALTER TABLE "UserNotifications" ADD "ProviderPayoutId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    ALTER TABLE "UserNotifications" ADD "ServiceOrderDisputeId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    ALTER TABLE "ServiceOrderPayments" ADD "PlatformFeeAmount" numeric(18,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    ALTER TABLE "ServiceOrderPayments" ADD "PlatformPaymentIntentId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    ALTER TABLE "ServiceOrderPayments" ADD "ProviderPayoutAmount" numeric(18,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE TABLE "PlatformPaymentIntents" (
        "Id" uuid NOT NULL,
        "ServiceOrderId" uuid NOT NULL,
        "CustomerId" character varying(450) NOT NULL,
        "ProviderId" character varying(450) NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "PlatformFeeAmount" numeric(18,2) NOT NULL,
        "ProviderPayoutAmount" numeric(18,2) NOT NULL,
        "Status" smallint NOT NULL,
        "GatewayReference" character varying(100) NOT NULL,
        "GatewayPaymentId" character varying(200),
        "VerificationNotes" character varying(1000),
        "FailureReason" character varying(1000),
        "VerifiedByUserId" character varying(450),
        "ExpiresAt" timestamp with time zone NOT NULL,
        "VerifiedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_PlatformPaymentIntents" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PlatformPaymentIntents_ServiceOrders_ServiceOrderId" FOREIGN KEY ("ServiceOrderId") REFERENCES "ServiceOrders" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE TABLE "ProviderPayouts" (
        "Id" uuid NOT NULL,
        "ServiceOrderPaymentId" uuid NOT NULL,
        "ServiceOrderId" uuid NOT NULL,
        "ProviderId" character varying(450) NOT NULL,
        "GrossAmount" numeric(18,2) NOT NULL,
        "PlatformFeeAmount" numeric(18,2) NOT NULL,
        "PayoutAmount" numeric(18,2) NOT NULL,
        "Status" smallint NOT NULL,
        "PayoutReference" character varying(200),
        "Notes" character varying(1000),
        "MarkedPaidByUserId" character varying(450),
        "PaidAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ProviderPayouts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ProviderPayouts_ServiceOrderPayments_ServiceOrderPaymentId" FOREIGN KEY ("ServiceOrderPaymentId") REFERENCES "ServiceOrderPayments" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ProviderPayouts_ServiceOrders_ServiceOrderId" FOREIGN KEY ("ServiceOrderId") REFERENCES "ServiceOrders" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE TABLE "ServiceOrderDisputes" (
        "Id" uuid NOT NULL,
        "ServiceOrderId" uuid NOT NULL,
        "ServiceOrderPaymentId" uuid,
        "RaisedByUserId" character varying(450) NOT NULL,
        "AgainstUserId" character varying(450) NOT NULL,
        "Reason" character varying(1000) NOT NULL,
        "Status" smallint NOT NULL,
        "ResolutionNotes" character varying(1000),
        "ResolvedByUserId" character varying(450),
        "ResolvedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ServiceOrderDisputes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ServiceOrderDisputes_ServiceOrderPayments_ServiceOrderPayme~" FOREIGN KEY ("ServiceOrderPaymentId") REFERENCES "ServiceOrderPayments" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_ServiceOrderDisputes_ServiceOrders_ServiceOrderId" FOREIGN KEY ("ServiceOrderId") REFERENCES "ServiceOrders" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE INDEX "IX_UserNotifications_ProviderPayoutId" ON "UserNotifications" ("ProviderPayoutId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE INDEX "IX_UserNotifications_ServiceOrderDisputeId" ON "UserNotifications" ("ServiceOrderDisputeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE UNIQUE INDEX "UX_ServiceOrderPayments_PlatformPaymentIntentId" ON "ServiceOrderPayments" ("PlatformPaymentIntentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE INDEX "IX_PlatformPaymentIntents_CustomerStatusCreated" ON "PlatformPaymentIntents" ("CustomerId", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE INDEX "IX_PlatformPaymentIntents_OrderStatusCreated" ON "PlatformPaymentIntents" ("ServiceOrderId", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE UNIQUE INDEX "UX_PlatformPaymentIntents_GatewayReference" ON "PlatformPaymentIntents" ("GatewayReference");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE INDEX "IX_ProviderPayouts_ProviderStatusCreated" ON "ProviderPayouts" ("ProviderId", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE INDEX "IX_ProviderPayouts_ServiceOrderId" ON "ProviderPayouts" ("ServiceOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE INDEX "IX_ProviderPayouts_StatusCreated" ON "ProviderPayouts" ("Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE UNIQUE INDEX "UX_ProviderPayouts_ServiceOrderPaymentId" ON "ProviderPayouts" ("ServiceOrderPaymentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE INDEX "IX_ServiceOrderDisputes_OrderStatusCreated" ON "ServiceOrderDisputes" ("ServiceOrderId", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE INDEX "IX_ServiceOrderDisputes_ServiceOrderPaymentId" ON "ServiceOrderDisputes" ("ServiceOrderPaymentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    CREATE INDEX "IX_ServiceOrderDisputes_StatusCreated" ON "ServiceOrderDisputes" ("Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    ALTER TABLE "ServiceOrderPayments" ADD CONSTRAINT "FK_ServiceOrderPayments_PlatformPaymentIntents_PlatformPayment~" FOREIGN KEY ("PlatformPaymentIntentId") REFERENCES "PlatformPaymentIntents" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260807121044_AddMarketplaceEconomics') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260807121044_AddMarketplaceEconomics', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809184129_AddContactRequestsAndProfileDirectory') THEN
    ALTER TABLE "UserNotifications" ADD "ContactRequestId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809184129_AddContactRequestsAndProfileDirectory') THEN
    CREATE TABLE "ContactRequests" (
        "Id" uuid NOT NULL,
        "RequesterUserId" character varying(450) NOT NULL,
        "TargetUserId" character varying(450) NOT NULL,
        "TargetProfileType" smallint NOT NULL,
        "Kind" smallint NOT NULL,
        "Status" smallint NOT NULL DEFAULT 1,
        "Message" character varying(1000),
        "PreferredCallbackAt" timestamp with time zone,
        "ReviewedByUserId" character varying(450),
        "ReviewedAt" timestamp with time zone,
        "ReviewNotes" character varying(1000),
        "RejectionReason" character varying(1000),
        "CompletedByUserId" character varying(450),
        "CompletedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ContactRequests" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809184129_AddContactRequestsAndProfileDirectory') THEN
    CREATE INDEX "IX_UserNotifications_ContactRequestId" ON "UserNotifications" ("ContactRequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809184129_AddContactRequestsAndProfileDirectory') THEN
    CREATE INDEX "IX_ContactRequests_KindStatus" ON "ContactRequests" ("Kind", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809184129_AddContactRequestsAndProfileDirectory') THEN
    CREATE INDEX "IX_ContactRequests_RequesterStatusCreated" ON "ContactRequests" ("RequesterUserId", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809184129_AddContactRequestsAndProfileDirectory') THEN
    CREATE INDEX "IX_ContactRequests_TargetStatusCreated" ON "ContactRequests" ("TargetUserId", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809184129_AddContactRequestsAndProfileDirectory') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260809184129_AddContactRequestsAndProfileDirectory', '9.0.0');
    END IF;
END $EF$;
COMMIT;

