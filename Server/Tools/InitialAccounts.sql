DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'accounts') THEN
        CREATE SCHEMA accounts;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS accounts."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM accounts."__EFMigrationsHistory" WHERE "MigrationId" = '20260907045416_InitialAccounts') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'accounts') THEN
            CREATE SCHEMA accounts;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM accounts."__EFMigrationsHistory" WHERE "MigrationId" = '20260907045416_InitialAccounts') THEN
    CREATE TABLE accounts.users (
        user_id uuid NOT NULL,
        display_name text NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT pk_users PRIMARY KEY (user_id),
        CONSTRAINT ck_users_display_name CHECK (length(btrim(display_name, chr(9)||chr(10)||chr(11)||chr(12)||chr(13)||chr(32)||chr(133)||chr(160)||chr(5760)||chr(8192)||chr(8193)||chr(8194)||chr(8195)||chr(8196)||chr(8197)||chr(8198)||chr(8199)||chr(8200)||chr(8201)||chr(8202)||chr(8232)||chr(8233)||chr(8239)||chr(8287)||chr(12288))) > 0),
        CONSTRAINT ck_users_nonempty_id CHECK (user_id <> '00000000-0000-0000-0000-000000000000'::uuid)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM accounts."__EFMigrationsHistory" WHERE "MigrationId" = '20260907045416_InitialAccounts') THEN
    CREATE TABLE accounts.external_identities (
        provider text NOT NULL,
        provider_application_id text NOT NULL,
        provider_player_id text NOT NULL,
        user_id uuid NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT pk_external_identities PRIMARY KEY (provider, provider_application_id, provider_player_id),
        CONSTRAINT ck_identities_application CHECK (length(btrim(provider_application_id, chr(9)||chr(10)||chr(11)||chr(12)||chr(13)||chr(32)||chr(133)||chr(160)||chr(5760)||chr(8192)||chr(8193)||chr(8194)||chr(8195)||chr(8196)||chr(8197)||chr(8198)||chr(8199)||chr(8200)||chr(8201)||chr(8202)||chr(8232)||chr(8233)||chr(8239)||chr(8287)||chr(12288))) > 0),
        CONSTRAINT ck_identities_player CHECK (length(btrim(provider_player_id, chr(9)||chr(10)||chr(11)||chr(12)||chr(13)||chr(32)||chr(133)||chr(160)||chr(5760)||chr(8192)||chr(8193)||chr(8194)||chr(8195)||chr(8196)||chr(8197)||chr(8198)||chr(8199)||chr(8200)||chr(8201)||chr(8202)||chr(8232)||chr(8233)||chr(8239)||chr(8287)||chr(12288))) > 0),
        CONSTRAINT ck_identities_provider CHECK (length(btrim(provider, chr(9)||chr(10)||chr(11)||chr(12)||chr(13)||chr(32)||chr(133)||chr(160)||chr(5760)||chr(8192)||chr(8193)||chr(8194)||chr(8195)||chr(8196)||chr(8197)||chr(8198)||chr(8199)||chr(8200)||chr(8201)||chr(8202)||chr(8232)||chr(8233)||chr(8239)||chr(8287)||chr(12288))) > 0),
        CONSTRAINT fk_external_identities_users FOREIGN KEY (user_id) REFERENCES accounts.users (user_id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM accounts."__EFMigrationsHistory" WHERE "MigrationId" = '20260907045416_InitialAccounts') THEN
    CREATE INDEX ix_external_identities_user_id ON accounts.external_identities (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM accounts."__EFMigrationsHistory" WHERE "MigrationId" = '20260907045416_InitialAccounts') THEN
    INSERT INTO accounts."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260907045416_InitialAccounts', '9.0.19');
    END IF;
END $EF$;
COMMIT;

