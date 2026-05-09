using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Synapse.Infrastructure.Migrations
{
    public partial class SeedTestData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword("Password123!", 11);

            migrationBuilder.Sql($"""
                INSERT INTO "Users" ("Email", "Username", "PasswordHash", "Role", "CreatedAt", "LastLoginAt", "Language")
                VALUES
                    ('user_a@example.com',   'tester_a',    '{hash}', 0, NOW(), NOW(), 'pl'),
                    ('user_b@example.com',   'tester_b',    '{hash}', 0, NOW(), NOW(), 'pl'),
                    ('business@example.com', 'krakow_cafe', '{hash}', 1, NOW(), NOW(), 'pl')
                ON CONFLICT ("Email") DO UPDATE
                    SET "PasswordHash" = EXCLUDED."PasswordHash",
                        "Username"     = EXCLUDED."Username",
                        "Role"         = EXCLUDED."Role";
            """);

            migrationBuilder.Sql("""
                INSERT INTO "Businesses" ("Name", "Address", "City", "Category", "IsActive", "DefaultDiscountPercent", "CreatedAt", "OwnerId", "Location", "StripeOnboardingComplete")
                SELECT v."Name", v."Address", v."City", v."Category", true, 15, NOW(),
                       u."Id",
                       ST_SetSRID(ST_MakePoint(v."Lng", v."Lat"), 4326)::geography,
                       false
                FROM (VALUES
                    ('Propaganda Pub',     'Miodowa 20',            'Kraków', 'Pub',      19.9449::float8, 50.0519::float8),
                    ('Camelot Cafe',       'Świętego Tomasza 17',   'Kraków', 'Coffee',   19.9395::float8, 50.0628::float8),
                    ('Forum Przestrzenie', 'Marii Konopnickiej 28', 'Kraków', 'Cultural', 19.9365::float8, 50.0460::float8)
                ) AS v("Name", "Address", "City", "Category", "Lng", "Lat")
                JOIN "Users" u ON u."Email" = 'business@example.com'
                WHERE NOT EXISTS (SELECT 1 FROM "Businesses" b WHERE b."Name" = v."Name");
            """);

            migrationBuilder.Sql("""
                UPDATE "Businesses"
                SET "IsActive" = true,
                    "OwnerId"  = (SELECT "Id" FROM "Users" WHERE "Email" = 'business@example.com')
                WHERE "Name" IN ('Propaganda Pub', 'Camelot Cafe', 'Forum Przestrzenie');
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "Businesses" WHERE "Name" IN ('Propaganda Pub', 'Camelot Cafe', 'Forum Przestrzenie');
                DELETE FROM "Users"      WHERE "Email" IN ('user_a@example.com', 'user_b@example.com', 'business@example.com');
            """);
        }
    }
}
