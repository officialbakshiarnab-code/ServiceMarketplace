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
COMMIT;

