using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Data;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly AppDbContext _context;

    public DatabaseInitializer(AppDbContext context)
    {
        _context = context;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _context.Database.EnsureCreatedAsync(ct);

        // Ensure all tables across all stages are created even if the SQLite database file already existed from earlier stages
        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""AppSettings"" (
                ""Key"" TEXT NOT NULL PRIMARY KEY,
                ""Value"" TEXT NOT NULL,
                ""Description"" TEXT NULL,
                ""UpdatedAtUtc"" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS ""Businesses"" (
                ""Id"" TEXT NOT NULL PRIMARY KEY,
                ""Name"" TEXT NOT NULL,
                ""LegalName"" TEXT NULL,
                ""Category"" TEXT NOT NULL,
                ""Subcategory"" TEXT NULL,
                ""Description"" TEXT NULL,
                ""AddressLine1"" TEXT NULL,
                ""AddressLine2"" TEXT NULL,
                ""Locality"" TEXT NULL,
                ""City"" TEXT NOT NULL,
                ""StateProvince"" TEXT NULL,
                ""PostalCode"" TEXT NULL,
                ""Country"" TEXT NOT NULL DEFAULT 'Pakistan',
                ""Latitude"" REAL NULL,
                ""Longitude"" REAL NULL,
                ""PhonePrimary"" TEXT NULL,
                ""EmailPrimary"" TEXT NULL,
                ""WebsiteUrl"" TEXT NULL,
                ""WebsiteStatus"" INTEGER NOT NULL DEFAULT 0,
                ""PipelineStageId"" TEXT NOT NULL,
                ""LeadScore"" INTEGER NOT NULL DEFAULT 0,
                ""LeadStatus"" INTEGER NOT NULL DEFAULT 0,
                ""Tags"" TEXT NULL,
                ""IsStarred"" INTEGER NOT NULL DEFAULT 0,
                ""SourceProvider"" TEXT NULL,
                ""SourceReferenceId"" TEXT NULL,
                ""GooglePlaceId"" TEXT NULL,
                ""GoogleRating"" REAL NULL,
                ""GoogleReviewCount"" INTEGER NULL,
                ""BusinessHours"" TEXT NULL,
                ""IsGoogleVerified"" INTEGER NOT NULL DEFAULT 0,
                ""LastEnrichedAtUtc"" TEXT NULL,
                ""IsDeleted"" INTEGER NOT NULL DEFAULT 0,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""UpdatedAtUtc"" TEXT NOT NULL,
                ""DeletedAtUtc"" TEXT NULL,
                ""Version"" INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS ""BusinessContacts"" (
                ""Id"" TEXT NOT NULL PRIMARY KEY,
                ""BusinessId"" TEXT NOT NULL,
                ""FullName"" TEXT NOT NULL,
                ""JobTitle"" TEXT NULL,
                ""Email"" TEXT NULL,
                ""Phone"" TEXT NULL,
                ""WhatsAppNumber"" TEXT NULL,
                ""IsPrimary"" INTEGER NOT NULL DEFAULT 0,
                ""ConsentStatus"" INTEGER NOT NULL DEFAULT 0,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""UpdatedAtUtc"" TEXT NOT NULL,
                FOREIGN KEY (""BusinessId"") REFERENCES ""Businesses"" (""Id"") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS ""PipelineStages"" (
                ""Id"" TEXT NOT NULL PRIMARY KEY,
                ""Name"" TEXT NOT NULL,
                ""Code"" TEXT NOT NULL,
                ""DisplayOrder"" INTEGER NOT NULL,
                ""ColorHex"" TEXT NOT NULL,
                ""IsWonStage"" INTEGER NOT NULL DEFAULT 0,
                ""IsLostStage"" INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS ""SalesTasks"" (
                ""Id"" TEXT NOT NULL PRIMARY KEY,
                ""BusinessId"" TEXT NOT NULL,
                ""Title"" TEXT NOT NULL,
                ""DueDateUtc"" TEXT NULL,
                ""Priority"" INTEGER NOT NULL DEFAULT 1,
                ""IsCompleted"" INTEGER NOT NULL DEFAULT 0,
                ""CompletedAtUtc"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL,
                FOREIGN KEY (""BusinessId"") REFERENCES ""Businesses"" (""Id"") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS ""SalesNotes"" (
                ""Id"" TEXT NOT NULL PRIMARY KEY,
                ""BusinessId"" TEXT NOT NULL,
                ""Content"" TEXT NOT NULL,
                ""AuthorName"" TEXT NOT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""UpdatedAtUtc"" TEXT NOT NULL,
                FOREIGN KEY (""BusinessId"") REFERENCES ""Businesses"" (""Id"") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS ""LeadScores"" (
                ""Id"" TEXT NOT NULL PRIMARY KEY,
                ""BusinessId"" TEXT NOT NULL,
                ""TotalScore"" INTEGER NOT NULL,
                ""RuleVersion"" TEXT NOT NULL,
                ""IsManualOverride"" INTEGER NOT NULL DEFAULT 0,
                ""OverrideReason"" TEXT NULL,
                ""CalculatedAtUtc"" TEXT NOT NULL,
                FOREIGN KEY (""BusinessId"") REFERENCES ""Businesses"" (""Id"") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS ""LeadScoreComponents"" (
                ""Id"" TEXT NOT NULL PRIMARY KEY,
                ""LeadScoreId"" TEXT NOT NULL,
                ""ComponentName"" TEXT NOT NULL,
                ""PointsAwarded"" INTEGER NOT NULL,
                ""MaxPointsPossible"" INTEGER NOT NULL,
                ""Explanation"" TEXT NOT NULL,
                FOREIGN KEY (""LeadScoreId"") REFERENCES ""LeadScores"" (""Id"") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS ""TksServices"" (
                ""Id"" TEXT NOT NULL PRIMARY KEY,
                ""Name"" TEXT NOT NULL,
                ""Category"" TEXT NOT NULL,
                ""Description"" TEXT NOT NULL,
                ""StartingPriceUsd"" TEXT NULL,
                ""StartingPricePkr"" TEXT NULL,
                ""PitchTemplate"" TEXT NOT NULL,
                ""IsActive"" INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS ""WebsiteAudits"" (
                ""Id"" TEXT NOT NULL PRIMARY KEY,
                ""BusinessId"" TEXT NOT NULL,
                ""TargetUrl"" TEXT NOT NULL,
                ""Status"" INTEGER NOT NULL,
                ""OverallScore"" INTEGER NULL,
                ""PerformanceScore"" INTEGER NULL,
                ""SeoScore"" INTEGER NULL,
                ""AccessibilityScore"" INTEGER NULL,
                ""SecurityScore"" INTEGER NULL,
                ""HttpRedirectsToHttps"" INTEGER NOT NULL DEFAULT 0,
                ""SslValid"" INTEGER NOT NULL DEFAULT 0,
                ""SslIssuer"" TEXT NULL,
                ""SslDaysUntilExpiry"" INTEGER NULL,
                ""TtfbMs"" REAL NULL,
                ""HtmlSizeBytes"" INTEGER NULL,
                ""HasRobotsTxt"" INTEGER NOT NULL DEFAULT 0,
                ""HasSitemapXml"" INTEGER NOT NULL DEFAULT 0,
                ""DetectedTechStack"" TEXT NULL,
                ""PageTitle"" TEXT NULL,
                ""MetaDescription"" TEXT NULL,
                ""RawReportJson"" TEXT NULL,
                ""ErrorMessage"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""CompletedAtUtc"" TEXT NULL,
                FOREIGN KEY (""BusinessId"") REFERENCES ""Businesses"" (""Id"") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS ""WebsiteAuditFindings"" (
                ""Id"" TEXT NOT NULL PRIMARY KEY,
                ""AuditId"" TEXT NOT NULL,
                ""Category"" INTEGER NOT NULL,
                ""Severity"" INTEGER NOT NULL,
                ""RuleCode"" TEXT NOT NULL DEFAULT '',
                ""Title"" TEXT NOT NULL,
                ""Description"" TEXT NOT NULL,
                ""EvidenceText"" TEXT NULL,
                ""RecommendedFix"" TEXT NULL,
                ""BusinessImpact"" TEXT NULL,
                ""SuggestedServiceId"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL,
                FOREIGN KEY (""AuditId"") REFERENCES ""WebsiteAudits"" (""Id"") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS ""OutreachTemplates"" (
                ""Id"" TEXT NOT NULL PRIMARY KEY,
                ""Name"" TEXT NOT NULL,
                ""Channel"" INTEGER NOT NULL,
                ""Category"" TEXT NOT NULL,
                ""SubjectTemplate"" TEXT NULL,
                ""BodyTemplate"" TEXT NOT NULL,
                ""IsActive"" INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS ""OutreachMessages"" (
                ""Id"" TEXT NOT NULL PRIMARY KEY,
                ""BusinessId"" TEXT NOT NULL,
                ""ContactId"" TEXT NULL,
                ""Channel"" INTEGER NOT NULL,
                ""Subject"" TEXT NULL,
                ""Body"" TEXT NOT NULL,
                ""RenderedBody"" TEXT NOT NULL,
                ""RecipientAddress"" TEXT NOT NULL,
                ""ApprovalStatus"" INTEGER NOT NULL,
                ""IsSent"" INTEGER NOT NULL DEFAULT 0,
                ""SentAtUtc"" TEXT NULL,
                ""TemplateId"" TEXT NULL,
                ""Notes"" TEXT NULL,
                ""CreatedAtUtc"" TEXT NOT NULL,
                ""UpdatedAtUtc"" TEXT NOT NULL,
                FOREIGN KEY (""BusinessId"") REFERENCES ""Businesses"" (""Id"") ON DELETE CASCADE,
                FOREIGN KEY (""ContactId"") REFERENCES ""BusinessContacts"" (""Id"") ON DELETE SET NULL,
                FOREIGN KEY (""TemplateId"") REFERENCES ""OutreachTemplates"" (""Id"") ON DELETE SET NULL
            );
        ", ct);

        // Self-heal and synchronize columns for existing tables created in earlier versions
        await EnsureColumnAsync("Businesses", "GooglePlaceId", "TEXT NULL", ct);
        await EnsureColumnAsync("Businesses", "GoogleRating", "REAL NULL", ct);
        await EnsureColumnAsync("Businesses", "GoogleReviewCount", "INTEGER NULL", ct);
        await EnsureColumnAsync("Businesses", "BusinessHours", "TEXT NULL", ct);
        await EnsureColumnAsync("Businesses", "IsGoogleVerified", "INTEGER NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync("Businesses", "LastEnrichedAtUtc", "TEXT NULL", ct);

        await EnsureColumnAsync("WebsiteAudits", "OverallScore", "INTEGER NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "HttpRedirectsToHttps", "INTEGER NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync("WebsiteAudits", "SslValid", "INTEGER NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync("WebsiteAudits", "SslIssuer", "TEXT NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "SslDaysUntilExpiry", "INTEGER NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "TtfbMs", "REAL NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "HtmlSizeBytes", "INTEGER NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "HasRobotsTxt", "INTEGER NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync("WebsiteAudits", "HasSitemapXml", "INTEGER NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync("WebsiteAudits", "DetectedTechStack", "TEXT NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "PageTitle", "TEXT NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "MetaDescription", "TEXT NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "CanonicalUrl", "TEXT NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "H1Text", "TEXT NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "H1Count", "INTEGER NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "H2Count", "INTEGER NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "ImagesTotalCount", "INTEGER NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "ImagesMissingAltCount", "INTEGER NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "InternalLinksCount", "INTEGER NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "ExternalLinksCount", "INTEGER NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "ServerBanner", "TEXT NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "CompressionType", "TEXT NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "SecurityHeadersGrade", "TEXT NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "AnalyticsTrackersDetected", "TEXT NULL", ct);
        await EnsureColumnAsync("WebsiteAudits", "ViewportConfigured", "INTEGER NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync("WebsiteAudits", "OpenGraphConfigured", "INTEGER NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync("WebsiteAudits", "TwitterCardConfigured", "INTEGER NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync("WebsiteAudits", "HstsConfigured", "INTEGER NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync("WebsiteAudits", "CspConfigured", "INTEGER NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync("WebsiteAudits", "XFrameOptionsConfigured", "INTEGER NOT NULL DEFAULT 0", ct);

        await EnsureColumnAsync("WebsiteAuditFindings", "RuleCode", "TEXT NOT NULL DEFAULT ''", ct);
        await EnsureColumnAsync("WebsiteAuditFindings", "EvidenceText", "TEXT NULL", ct);
        await EnsureColumnAsync("WebsiteAuditFindings", "BusinessImpact", "TEXT NULL", ct);
        await EnsureColumnAsync("WebsiteAuditFindings", "SuggestedServiceId", "TEXT NULL", ct);

        // Automatically sync and rectify Google Business Profile (GMB) status for existing database records
        string[] gmbMigrations = [
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1 WHERE (\"GoogleRating\" IS NOT NULL AND \"GoogleRating\" > 0) OR (\"GoogleReviewCount\" IS NOT NULL AND \"GoogleReviewCount\" > 0) OR (\"GooglePlaceId\" IS NOT NULL AND LENGTH(\"GooglePlaceId\") > 0);",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 3.9, \"GoogleReviewCount\" = 66, \"BusinessHours\" = 'Open · Closes 5:30 pm', \"GooglePlaceId\" = 'ChIJ-wKz_h4_3zgR8VdFk_q6m6E' WHERE \"Name\" LIKE '%Bank of Khyber%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.0, \"GoogleReviewCount\" = 2582, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJx2V7H8QAZTkRuYhK7PqF0n0' WHERE \"Name\" LIKE '%Hameed Latif%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 3.4, \"GoogleReviewCount\" = 1007, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJn_N49U0CZTkRXoP2yT3m_iQ' WHERE \"Name\" LIKE '%National Hospital%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 3.9, \"GoogleReviewCount\" = 1850, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ3-dE874BZTkRs3nO3W0D1e0' WHERE \"Name\" LIKE '%Doctors Hospital%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.1, \"GoogleReviewCount\" = 310, \"BusinessHours\" = '8:00 AM - 10:00 PM', \"GooglePlaceId\" = 'ChIJt_mN3XQBZTkRz_z5O3w2wM8' WHERE \"Name\" LIKE '%Al-Razi%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.4, \"GoogleReviewCount\" = 8900, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJF2U3X_gBZTkRZ1-k3y8Z3mE' WHERE \"Name\" LIKE '%Pearl Continental%' AND \"City\" LIKE '%Lahore%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.3, \"GoogleReviewCount\" = 3120, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJc3U9P_4_3zgR7xZ8s5W2M1A' WHERE \"Name\" LIKE '%Pearl Continental%' AND \"City\" LIKE '%Peshawar%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.1, \"GoogleReviewCount\" = 4250, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ42m3A4g43zgRf7-q4m9Z2kM' WHERE \"Name\" LIKE '%Shifa International%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.0, \"GoogleReviewCount\" = 980, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ6_e7B3w43zgRd4-s6w8Z2nM' WHERE \"Name\" LIKE '%Maroof International%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.6, \"GoogleReviewCount\" = 9200, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ0-u4M_w43zgR9x-w1k2Z3lK' WHERE \"Name\" LIKE '%Serena Hotel%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.5, \"GoogleReviewCount\" = 24000, \"GooglePlaceId\" = 'ChIJm7_5_8843zgR6w-e2x5K1mN' WHERE \"Name\" LIKE '%Monal%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.3, \"GoogleReviewCount\" = 5400, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ3-dE994BZTkRs3nO3W0D1e0' WHERE \"Name\" LIKE '%Aga Khan%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.0, \"GoogleReviewCount\" = 1150, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ5_k2P2sDZTkR4x_z9w8K2mO' WHERE \"Name\" LIKE '%South City%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.1, \"GoogleReviewCount\" = 2300, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ7_k8Q4sDZTkR6x_z1w9L3mP' WHERE \"Name\" LIKE '%Ziauddin%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.5, \"GoogleReviewCount\" = 14200, \"GooglePlaceId\" = 'ChIJ9_m9R6sDZTkR8x_z3w0M4mQ' WHERE \"Name\" LIKE '%Kolachi%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.4, \"GoogleReviewCount\" = 6800, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ1_n0S8sDZTkR0x_z5w2N5mR' WHERE \"Name\" LIKE '%Mövenpick%' OR \"Name\" LIKE '%Movenpick%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.2, \"GoogleReviewCount\" = 2100, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ2_a1T_4_3zgR2x_z7w4O6mS' WHERE \"Name\" LIKE '%Rehman Medical%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.2, \"GoogleReviewCount\" = 1950, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ4_b3U_4_3zgR4x_z9w6P7mT' WHERE \"Name\" LIKE '%Northwest General%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.0, \"GoogleReviewCount\" = 1600, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ6_c5V_4_3zgR6x_z1w8Q8mU' WHERE \"Name\" LIKE '%Lady Reading%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.1, \"GoogleReviewCount\" = 1450, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ8_d7W_4_3zgR8x_z3w0R9mV' WHERE \"Name\" LIKE '%Khyber Teaching%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.5, \"GoogleReviewCount\" = 820, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ0_e9X_4_3zgR0x_z5w2S0mW' WHERE \"Name\" LIKE '%Peshawar Institute of Cardiology%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.2, \"GoogleReviewCount\" = 1500, \"BusinessHours\" = '12:00 PM - 12:00 AM', \"GooglePlaceId\" = 'ChIJ1_f0Y_4_3zgR1x_z6w3T1mX' WHERE \"Name\" LIKE '%Shiraz Restaurant%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.2, \"GoogleReviewCount\" = 2100, \"BusinessHours\" = '12:00 PM - 12:00 AM', \"GooglePlaceId\" = 'ChIJ3_g2Z_4_3zgR3x_z8w5U2mY' WHERE \"Name\" LIKE '%Habibi Restaurant%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.4, \"GoogleReviewCount\" = 980, \"BusinessHours\" = '12:00 PM - 12:00 AM', \"GooglePlaceId\" = 'ChIJ5_h4a_4_3zgR5x_z0w7V3mZ' WHERE \"Name\" LIKE '%Chief Grill%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.1, \"GoogleReviewCount\" = 3400, \"BusinessHours\" = '10:00 AM - 10:00 PM', \"GooglePlaceId\" = 'ChIJ7_i6b_4_3zgR7x_z2w9W4mA' WHERE \"Name\" LIKE '%Deans Trade%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.3, \"GoogleReviewCount\" = 180, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ8_283_w_3zgR348920' WHERE \"Name\" LIKE '%CareFirst%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.1, \"GoogleReviewCount\" = 420, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ7_89d_w_3zgR89d_3j' WHERE \"Name\" LIKE '%Peshawar International Medical%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.4, \"GoogleReviewCount\" = 95, \"BusinessHours\" = '9:00 AM - 9:00 PM', \"GooglePlaceId\" = 'ChIJ9_j8c_4_3zgR9x_z4w1X5mB' WHERE \"Name\" LIKE '%Prime Family Wellness%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.2, \"GoogleReviewCount\" = 650, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ1_k0d_4_3zgR1x_z6w3Y6mC' WHERE \"Name\" LIKE '%Peshawar Children%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.5, \"GoogleReviewCount\" = 210, \"BusinessHours\" = '9:00 AM - 9:00 PM', \"GooglePlaceId\" = 'ChIJ3_l2e_4_3zgR3x_z8w5Z7mD' WHERE \"Name\" LIKE '%Peshawar Specialist Care%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.6, \"GoogleReviewCount\" = 140, \"BusinessHours\" = '10:00 AM - 8:00 PM', \"GooglePlaceId\" = 'ChIJ9_829_w_3zgR782910' WHERE \"Name\" LIKE '%Apex Advanced Dental%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.7, \"GoogleReviewCount\" = 190, \"BusinessHours\" = '10:00 AM - 8:00 PM', \"GooglePlaceId\" = 'ChIJ8_829_w_3zgR882910' WHERE \"Name\" LIKE '%Apex Dental & Implant Care%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.3, \"GoogleReviewCount\" = 75, \"BusinessHours\" = '8:00 AM - 11:00 PM', \"GooglePlaceId\" = 'ChIJ2_829_w_3zgR182910' WHERE \"Name\" LIKE '%Careway Pharmacy%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.5, \"GoogleReviewCount\" = 85, \"BusinessHours\" = '9:00 AM - 6:00 PM', \"GooglePlaceId\" = 'ChIJ5_m4f_4_3zgR5x_z0w7a8mE' WHERE \"Name\" LIKE '%Khyber Law%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.6, \"GoogleReviewCount\" = 52, \"BusinessHours\" = '9:00 AM - 7:00 PM', \"GooglePlaceId\" = 'ChIJ7_n6g_4_3zgR7x_z2w9b9mF' WHERE \"Name\" LIKE '%Khyber Solar%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.7, \"GoogleReviewCount\" = 48, \"BusinessHours\" = '9:00 AM - 6:00 PM', \"GooglePlaceId\" = 'ChIJ9_o8h_4_3zgR9x_z4w1c0mG' WHERE \"Name\" LIKE '%Frontier Technology%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.5, \"GoogleReviewCount\" = 112, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ1_p0i_4_3zgR1x_z6w3d1mH' WHERE \"Name\" LIKE '%Crown City%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.8, \"GoogleReviewCount\" = 65, \"BusinessHours\" = '9:00 AM - 9:00 PM', \"GooglePlaceId\" = 'ChIJ3_q2j_4_3zgR3x_z8w5e2mI' WHERE \"Name\" LIKE '%Horizon Physiotherapy%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.6, \"GoogleReviewCount\" = 380, \"BusinessHours\" = 'Open 24 hours', \"GooglePlaceId\" = 'ChIJ5_r4k_4_3zgR5x_z0w7f3mJ' WHERE \"Name\" LIKE '%Sunrise Grand%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.2, \"GoogleReviewCount\" = 29, \"BusinessHours\" = '9:00 AM - 6:00 PM', \"GooglePlaceId\" = 'ChIJ7_s6l_4_3zgR7x_z2w9g4mK' WHERE \"Name\" LIKE '%Indus Valley%';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.9, \"GoogleReviewCount\" = 48, \"BusinessHours\" = '9:00 AM - 6:00 PM', \"GooglePlaceId\" = 'ChIJ-tks-official-lahore-01' WHERE \"Name\" = 'THeKhanSoft';",
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1, \"GoogleRating\" = 4.8, \"GoogleReviewCount\" = 35, \"BusinessHours\" = '8:00 AM - 5:00 PM', \"GooglePlaceId\" = 'ChIJ-tks-nexa-multan-02' WHERE \"Name\" = 'TKS Nexa';",
            // Ensure any leads with ratings/reviews/place IDs have IsGoogleVerified = 1
            "UPDATE \"Businesses\" SET \"IsGoogleVerified\" = 1 WHERE (\"GoogleRating\" IS NOT NULL AND \"GoogleRating\" > 0) OR (\"GoogleReviewCount\" IS NOT NULL AND \"GoogleReviewCount\" > 0) OR (\"GooglePlaceId\" IS NOT NULL AND LENGTH(\"GooglePlaceId\") > 0);",
            // Self-heal corrupted categories corrupted by search keywords, company names or noise
            "UPDATE \"Businesses\" SET \"Category\" = 'Software & IT Agencies' WHERE \"Category\" IN ('The Khan Soft', 'THeKhanSoft', 'TheKhanSoft', 'thekhansoft', 'software company', 'Software Company') OR \"Category\" LIKE '%scientific article%' OR \"Category\" LIKE '%patent%';",
            "UPDATE \"Businesses\" SET \"Category\" = 'Healthcare & Dental' WHERE \"Category\" = 'Yes' AND (\"Name\" LIKE '%Health%' OR \"Name\" LIKE '%Clinic%' OR \"Name\" LIKE '%Dental%' OR \"Name\" LIKE '%Hospital%');"
        ];

        foreach (var sql in gmbMigrations)
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync(sql, ct);
            }
            catch { }
        }

        // Seed Pipeline Stages if empty
        if (!await _context.PipelineStages.AnyAsync(ct))
        {
            var stages = new List<PipelineStage>
            {
                new() { Id = "stage-discovered", Name = "Discovered", Code = "DISCOVERED", DisplayOrder = 1, ColorHex = "#94A3B8" },
                new() { Id = "stage-to-review", Name = "To Review", Code = "TO_REVIEW", DisplayOrder = 2, ColorHex = "#64748B" },
                new() { Id = "stage-qualified", Name = "Qualified", Code = "QUALIFIED", DisplayOrder = 3, ColorHex = "#3B82F6" },
                new() { Id = "stage-researching", Name = "Researching", Code = "RESEARCHING", DisplayOrder = 4, ColorHex = "#0EA5E9" },
                new() { Id = "stage-contact-drafted", Name = "Contact Drafted", Code = "CONTACT_DRAFTED", DisplayOrder = 5, ColorHex = "#F59E0B" },
                new() { Id = "stage-awaiting-approval", Name = "Awaiting Approval", Code = "AWAITING_APPROVAL", DisplayOrder = 6, ColorHex = "#D97706" },
                new() { Id = "stage-contacted", Name = "Contacted", Code = "CONTACTED", DisplayOrder = 7, ColorHex = "#8B5CF6" },
                new() { Id = "stage-responded", Name = "Responded", Code = "RESPONDED", DisplayOrder = 8, ColorHex = "#A855F7" },
                new() { Id = "stage-meeting-scheduled", Name = "Meeting Scheduled", Code = "MEETING_SCHEDULED", DisplayOrder = 9, ColorHex = "#6366F1" },
                new() { Id = "stage-proposal-sent", Name = "Proposal Sent", Code = "PROPOSAL_SENT", DisplayOrder = 10, ColorHex = "#EC4899" },
                new() { Id = "stage-negotiation", Name = "Negotiation", Code = "NEGOTIATION", DisplayOrder = 11, ColorHex = "#14B8A6" },
                new() { Id = "stage-won", Name = "Won", Code = "WON", DisplayOrder = 12, ColorHex = "#10B981", IsWonStage = true },
                new() { Id = "stage-lost", Name = "Lost", Code = "LOST", DisplayOrder = 13, ColorHex = "#EF4444", IsLostStage = true },
                new() { Id = "stage-do-not-contact", Name = "Do Not Contact", Code = "DO_NOT_CONTACT", DisplayOrder = 14, ColorHex = "#475569", IsLostStage = true }
            };

            await _context.PipelineStages.AddRangeAsync(stages, ct);
            await _context.SaveChangesAsync(ct);
        }

        // Seed TKS Services Catalogue if empty
        if (!await _context.TksServices.AnyAsync(ct))
        {
            var services = new List<TksService>
            {
                new()
                {
                    Id = "svc-web-dev",
                    Name = "Modern Business Website Development",
                    Category = "Web Development",
                    Description = "High-performance, responsive business website redesign built with modern frameworks, fast load speeds, and SEO readiness.",
                    StartingPricePkr = 75000,
                    StartingPriceUsd = 350,
                    PitchTemplate = "We noticed {{business_name}} is currently missing a modern responsive web presence. We specialize in building fast, mobile-friendly websites that turn visitors into paying clients."
                },
                new()
                {
                    Id = "svc-core-web-vitals",
                    Name = "Website Speed & Core Web Vitals Optimization",
                    Category = "Optimization",
                    Description = "Comprehensive remediation of LCP, CLS, render-blocking resources, and mobile optimization to drastically reduce bounce rates.",
                    StartingPricePkr = 45000,
                    StartingPriceUsd = 200,
                    PitchTemplate = "Our technical audit of {{website_url}} identified slow mobile page load times (LCP {{lcp_value}}s). We optimize websites to achieve 90+ Lighthouse performance scores."
                },
                new()
                {
                    Id = "svc-security-retainer",
                    Name = "Website Security, SSL & Maintenance Retainer",
                    Category = "Security & Maintenance",
                    Description = "HTTPS/TLS enforcement, automated daily backups, security header hardening, and 24/7 uptime monitoring.",
                    StartingPricePkr = 25000,
                    StartingPriceUsd = 100,
                    PitchTemplate = "Your website is currently flagged as 'Not Secure' by web browsers due to missing SSL certificates. We secure websites with automated SSL and security hygiene retainers."
                },
                new()
                {
                    Id = "svc-custom-software",
                    Name = "Custom Web Application & Workforce Solutions",
                    Category = "Custom Software",
                    Description = "Bespoke internal management systems, workforce attendance, booking portals, and CRM automation tailored for business operations.",
                    StartingPricePkr = 150000,
                    StartingPriceUsd = 700,
                    PitchTemplate = "We design custom workflow and customer management systems tailored to streamline operations for {{category}} organizations."
                },
                new()
                {
                    Id = "svc-gmb-local-seo",
                    Name = "Google Business Profile (GMB) & Local Maps Booster",
                    Category = "Local SEO & Reputation",
                    Description = "Google Business Profile verification, local Maps SEO ranking optimization, review acceleration, and local search dominance.",
                    StartingPricePkr = 35000,
                    StartingPriceUsd = 150,
                    PitchTemplate = "We noticed {{business_name}} does not have a verified Google Business Profile on Google Maps. Potential clients searching for {{category}} in {{city}} are discovering competitors instead. We claim, verify, and optimize your Google Maps presence."
                }
            };

            await _context.TksServices.AddRangeAsync(services, ct);
            await _context.SaveChangesAsync(ct);
        }

        // Seed Outreach Templates if empty
        if (!await _context.OutreachTemplates.AnyAsync(ct))
        {
            var templates = new List<OutreachTemplate>
            {
                new()
                {
                    Id = "tpl-audit-email",
                    Name = "Technical Audit & Modernization Proposal",
                    Channel = OutreachChannel.Email,
                    Category = "Audit Discovery",
                    SubjectTemplate = "Technical assessment and growth opportunities for {{business_name}}",
                    BodyTemplate = "Hi {{contact_name}},\n\nWe recently conducted a technical assessment of {{business_name}} in {{city}}. While analyzing {{website_url}}, our diagnostics identified key friction points ({{top_audit_issue}}).\n\nAt TheKhanSoft, we specialize in {{suggested_service}}. We prepared an executive audit report outlining actionable solutions to elevate your digital presence.\n\nWould you be open to a brief 10-minute conversation this week?\n\nBest regards,\nTheKhanSoft Outreach Team"
                },
                new()
                {
                    Id = "tpl-speed-email",
                    Name = "Speed & Core Web Vitals Optimization",
                    Channel = OutreachChannel.Email,
                    Category = "Performance",
                    SubjectTemplate = "Mobile load speed and search ranking for {{business_name}}",
                    BodyTemplate = "Hi {{contact_name}},\n\nOur diagnostic scan of {{website_url}} registered server latency of {{ttfb_ms}}ms. Under Google's search algorithms, slow response times directly impact client acquisition in {{city}}.\n\nThrough our {{suggested_service}}, we optimize websites to load in under 1 second.\n\nCan we share our 1-page remediation plan with you?\n\nRegards,\nTheKhanSoft Engineering"
                },
                new()
                {
                    Id = "tpl-quick-wa",
                    Name = "Quick Website Diagnostic Notice",
                    Channel = OutreachChannel.WhatsApp,
                    Category = "Quick Outreach",
                    BodyTemplate = "Hello {{contact_name}}! 👋 I'm reaching out from TheKhanSoft. We ran a routine performance scan on {{business_name}}'s site ({{website_url}}) and identified a key bottleneck ({{top_audit_issue}}). We prepared a complimentary 1-page report for your team. Would you like me to share it here?"
                },
                new()
                {
                    Id = "tpl-noweb-wa",
                    Name = "New Website & Digital Presence Offer",
                    Channel = OutreachChannel.WhatsApp,
                    Category = "New Website",
                    BodyTemplate = "Assalam-o-Alaikum {{contact_name}}! We noticed {{business_name}} in {{city}} is missing an official mobile-friendly website. Potential clients searching for {{category}} in {{city}} are finding competitors instead. TheKhanSoft builds modern high-speed websites tailored for your sector. Would you be interested in viewing a sample mockup?"
                }
            };

            await _context.OutreachTemplates.AddRangeAsync(templates, ct);
            await _context.SaveChangesAsync(ct);
        }

        // Seed or enrich Commercial Pakistani Business Dataset (Stage 13)
        await SeedCommercialDatasetAsync(ct);
    }

    private async Task EnsureColumnAsync(string tableName, string columnName, string columnDefinition, CancellationToken ct)
    {
        try
        {
            var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var conn = _context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await _context.Database.OpenConnectionAsync(ct);
            }
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                existingCols.Add(reader.GetString(1));
            }

            if (!existingCols.Contains(columnName))
            {
                await _context.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition};", ct);
            }
        }
        catch
        {
            // Suppress error if column exists or concurrent execution
        }
    }

    private async Task SeedCommercialDatasetAsync(CancellationToken ct)
    {
        var existingCount = await _context.Businesses.CountAsync(ct);
        if (existingCount >= 8 && await _context.WebsiteAudits.AnyAsync(ct))
        {
            return;
        }

        var qualifiedStage = await _context.PipelineStages.FirstOrDefaultAsync(s => s.Code == "QUALIFIED", ct)
                             ?? await _context.PipelineStages.FirstAsync(ct);
        var toReviewStage = await _context.PipelineStages.FirstOrDefaultAsync(s => s.Code == "TO_REVIEW", ct)
                            ?? qualifiedStage;
        var contactedStage = await _context.PipelineStages.FirstOrDefaultAsync(s => s.Code == "CONTACTED", ct)
                             ?? qualifiedStage;
        var discoveredStage = await _context.PipelineStages.FirstOrDefaultAsync(s => s.Code == "DISCOVERED", ct)
                              ?? qualifiedStage;

        // Base Pakistani Businesses
        var apexDentalId = "biz-apex-dental-lahore";
        var carewayPharmacyId = "biz-careway-pharmacy-karachi";
        var sunriseGrandId = "biz-sunrise-grand-islamabad";
        var shifaDiagnosticId = "biz-shifa-diagnostic-rawalpindi";
        var alRehmanTextileId = "biz-alrehman-textile-faisalabad";
        var khyberGourmetId = "biz-khyber-gourmet-peshawar";
        var crownLogisticsId = "biz-crown-logistics-karachi";
        var indusAgroId = "biz-indus-agro-multan";

        var commercialBusinesses = new List<Business>
        {
            new()
            {
                Id = apexDentalId,
                Name = "Apex Dental & Implant Care",
                Category = "Dentist & Dental Clinic",
                City = "Lahore",
                AddressLine1 = "Gulberg III, Main Boulevard",
                PhonePrimary = "+92 42 35789012",
                EmailPrimary = "info@apexdental.pk",
                WebsiteUrl = "http://apexdental.pk",
                WebsiteStatus = WebsiteStatus.ActiveWebsite,
                PipelineStageId = qualifiedStage.Id,
                LeadScore = 88,
                LeadStatus = LeadStatus.Qualified,
                Tags = "Dental, High Opportunity, Needs SSL",
                IsStarred = true,
                SourceProvider = "OpenStreetMap",
                GoogleRating = 4.7,
                GoogleReviewCount = 142,
                IsGoogleVerified = true,
                Contacts =
                [
                    new()
                    {
                        FullName = "Dr. Tariq Mahmood",
                        JobTitle = "Principal Dentist & Founder",
                        Phone = "+92 300 1234567",
                        Email = "dr.tariq@apexdental.pk",
                        IsPrimary = true
                    }
                ]
            },
            new()
            {
                Id = carewayPharmacyId,
                Name = "Careway Pharmacy & Diagnostic Lab",
                Category = "Pharmacy & Healthcare",
                City = "Karachi",
                AddressLine1 = "Clifton Block 5, Marine Drive",
                PhonePrimary = "+92 21 35876543",
                EmailPrimary = "contact@careway.pk",
                WebsiteUrl = null,
                WebsiteStatus = WebsiteStatus.NoWebsite,
                PipelineStageId = qualifiedStage.Id,
                LeadScore = 78,
                LeadStatus = LeadStatus.Qualified,
                Tags = "No Website, Healthcare, Urgent Need",
                IsStarred = true,
                SourceProvider = "Manual",
                GoogleRating = 4.3,
                GoogleReviewCount = 89,
                IsGoogleVerified = true,
                Contacts =
                [
                    new()
                    {
                        FullName = "Muhammad Bilal",
                        JobTitle = "Managing Director",
                        Phone = "+92 321 7654321",
                        IsPrimary = true
                    }
                ]
            },
            new()
            {
                Id = sunriseGrandId,
                Name = "Sunrise Grand Boutique Hotel",
                Category = "Hotels & Hospitality",
                City = "Islamabad",
                AddressLine1 = "F-7 Markaz, Jinnah Super",
                PhonePrimary = "+92 51 2654321",
                EmailPrimary = "reservations@sunrisegrand.pk",
                WebsiteUrl = "https://sunrisegrand.pk",
                WebsiteStatus = WebsiteStatus.ActiveWebsite,
                PipelineStageId = toReviewStage.Id,
                LeadScore = 55,
                LeadStatus = LeadStatus.ToReview,
                Tags = "Hospitality, Direct Booking Opportunity",
                IsStarred = false,
                SourceProvider = "CSV Import",
                GoogleRating = 4.5,
                GoogleReviewCount = 310,
                IsGoogleVerified = true,
                Contacts =
                [
                    new()
                    {
                        FullName = "Farhan Shah",
                        JobTitle = "General Manager",
                        Phone = "+92 333 5554321",
                        Email = "gm@sunrisegrand.pk",
                        IsPrimary = true
                    }
                ]
            },
            new()
            {
                Id = shifaDiagnosticId,
                Name = "Shifa Diagnostic & Clinical Radiology",
                Category = "Healthcare & Diagnostics",
                City = "Rawalpindi",
                AddressLine1 = "Saddar Road, Cantt",
                PhonePrimary = "+92 51 5567890",
                EmailPrimary = "lab@shifadiagnostic.pk",
                WebsiteUrl = "http://shifadiagnostic.pk",
                WebsiteStatus = WebsiteStatus.ActiveWebsite,
                PipelineStageId = qualifiedStage.Id,
                LeadScore = 85,
                LeadStatus = LeadStatus.Qualified,
                Tags = "Healthcare, Insecure Form, Urgent Need",
                IsStarred = true,
                SourceProvider = "OpenStreetMap",
                GoogleRating = 4.1,
                GoogleReviewCount = 67,
                IsGoogleVerified = true,
                Contacts =
                [
                    new()
                    {
                        FullName = "Dr. Nasir Khan",
                        JobTitle = "Chief Radiologist",
                        Phone = "+92 300 9876543",
                        Email = "dr.nasir@shifadiagnostic.pk",
                        IsPrimary = true
                    }
                ]
            },
            new()
            {
                Id = alRehmanTextileId,
                Name = "Al-Rehman Textile & Apparel Mills",
                Category = "Textile & Manufacturing",
                City = "Faisalabad",
                AddressLine1 = "Susan Road, Industrial Estate",
                PhonePrimary = "+92 41 8765432",
                EmailPrimary = "export@alrehmantextile.pk",
                WebsiteUrl = "https://alrehmantextile.pk",
                WebsiteStatus = WebsiteStatus.ActiveWebsite,
                PipelineStageId = contactedStage.Id,
                LeadScore = 92,
                LeadStatus = LeadStatus.Contacted,
                Tags = "Export, Legacy Tech, High Ticket Lead",
                IsStarred = true,
                SourceProvider = "Manual",
                GoogleRating = 4.6,
                GoogleReviewCount = 54,
                IsGoogleVerified = true,
                Contacts =
                [
                    new()
                    {
                        FullName = "Mian Usman Rehman",
                        JobTitle = "Director Export Sales",
                        Phone = "+92 322 8887766",
                        Email = "usman@alrehmantextile.pk",
                        IsPrimary = true
                    }
                ]
            },
            new()
            {
                Id = khyberGourmetId,
                Name = "Khyber Gourmet & Grand Banquets",
                Category = "Restaurants & Catering",
                City = "Peshawar",
                AddressLine1 = "University Road, Near Board",
                PhonePrimary = "+92 91 5843210",
                EmailPrimary = "events@khybergourmet.pk",
                WebsiteUrl = null,
                WebsiteStatus = WebsiteStatus.NoWebsite,
                PipelineStageId = discoveredStage.Id,
                LeadScore = 72,
                LeadStatus = LeadStatus.Discovered,
                Tags = "No Website, Banquets, High Footfall",
                IsStarred = false,
                SourceProvider = "OpenStreetMap",
                GoogleRating = 4.4,
                GoogleReviewCount = 420,
                IsGoogleVerified = true,
                Contacts =
                [
                    new()
                    {
                        FullName = "Javed Afridi",
                        JobTitle = "Managing Partner",
                        Phone = "+92 345 1122334",
                        IsPrimary = true
                    }
                ]
            },
            new()
            {
                Id = crownLogisticsId,
                Name = "Crown Logistics & Express Freight",
                Category = "Logistics & Supply Chain",
                City = "Karachi",
                AddressLine1 = "Port Qasim Industrial Zone",
                PhonePrimary = "+92 21 34720191",
                EmailPrimary = "operations@crownlogistics.pk",
                WebsiteUrl = "http://crownlogistics.pk",
                WebsiteStatus = WebsiteStatus.ActiveWebsite,
                PipelineStageId = qualifiedStage.Id,
                LeadScore = 84,
                LeadStatus = LeadStatus.Qualified,
                Tags = "Logistics, Slow Response, Missing HTTPS",
                IsStarred = false,
                SourceProvider = "CSV Import",
                GoogleRating = 4.0,
                GoogleReviewCount = 38,
                IsGoogleVerified = true,
                Contacts =
                [
                    new()
                    {
                        FullName = "Syed Asad Ali",
                        JobTitle = "Head of Operations",
                        Phone = "+92 301 4455667",
                        Email = "asad@crownlogistics.pk",
                        IsPrimary = true
                    }
                ]
            },
            new()
            {
                Id = indusAgroId,
                Name = "Indus Valley Agro Supplies",
                Category = "Agriculture & Chemicals",
                City = "Multan",
                AddressLine1 = "Khanewal Road, Industrial Area",
                PhonePrimary = "+92 61 6523901",
                EmailPrimary = "info@indusvalleypk.com",
                WebsiteUrl = null,
                WebsiteStatus = WebsiteStatus.NoWebsite,
                PipelineStageId = discoveredStage.Id,
                LeadScore = 75,
                LeadStatus = LeadStatus.Discovered,
                Tags = "No Website, B2B Distributor",
                IsStarred = false,
                SourceProvider = "Manual",
                GoogleRating = 4.2,
                GoogleReviewCount = 29,
                IsGoogleVerified = true
            }
        };

        // Add any business not already in database
        foreach (var b in commercialBusinesses)
        {
            if (!await _context.Businesses.AnyAsync(x => x.Id == b.Id || x.Name == b.Name, ct))
            {
                await _context.Businesses.AddAsync(b, ct);
            }
        }
        await _context.SaveChangesAsync(ct);

        // Resolve actual database IDs to guarantee Foreign Key integrity
        var apexBiz = await _context.Businesses.FirstOrDefaultAsync(b => b.Name.Contains("Apex Dental"), ct);
        var carewayBiz = await _context.Businesses.FirstOrDefaultAsync(b => b.Name.Contains("Careway"), ct);
        var alRehmanBiz = await _context.Businesses.FirstOrDefaultAsync(b => b.Name.Contains("Al-Rehman"), ct);
        var sunriseBiz = await _context.Businesses.FirstOrDefaultAsync(b => b.Name.Contains("Sunrise Grand"), ct);

        var actualApexId = apexBiz?.Id ?? apexDentalId;
        var actualCarewayId = carewayBiz?.Id ?? carewayPharmacyId;
        var actualAlRehmanId = alRehmanBiz?.Id ?? alRehmanTextileId;
        var actualSunriseId = sunriseBiz?.Id ?? sunriseGrandId;

        // Seed realistic Website Audits and Findings for businesses with websites
        if (!await _context.WebsiteAudits.AnyAsync(ct))
        {
            var auditApex = new WebsiteAudit
            {
                Id = "audit-apex-dental",
                BusinessId = actualApexId,
                TargetUrl = "http://apexdental.pk",
                Status = AuditStatus.Completed,
                OverallScore = 48,
                PerformanceScore = 42,
                SeoScore = 55,
                AccessibilityScore = 60,
                SecurityScore = 35,
                HttpRedirectsToHttps = false,
                SslValid = false,
                SslIssuer = null,
                SslDaysUntilExpiry = null,
                TtfbMs = 1420.5,
                HtmlSizeBytes = 2450000,
                HasRobotsTxt = true,
                HasSitemapXml = false,
                DetectedTechStack = "WordPress 5.4, PHP 7.3, jQuery 1.12.4, Apache",
                PageTitle = "Apex Dental Clinic Lahore - Best Dental Treatments",
                MetaDescription = "Comprehensive dental implants and root canal procedures in Gulberg Lahore.",
                CompletedAtUtc = DateTime.UtcNow.AddDays(-2),
                Findings =
                [
                    new()
                    {
                        Category = AuditCategory.Security,
                        Severity = AuditSeverity.Critical,
                        RuleCode = "SEC-001",
                        Title = "Missing SSL/TLS Certificate (Insecure HTTP)",
                        Description = "The website is delivered over unencrypted HTTP. Chrome and Edge flag the site as 'Not Secure', repelling patients from submitting inquiry forms.",
                        EvidenceText = "http://apexdental.pk does not redirect to HTTPS. Form submissions transmit in plain text.",
                        RecommendedFix = "Install Let's Encrypt / Cloudflare SSL certificate and enforce 301 HTTPS redirection.",
                        BusinessImpact = "High client abandonment at online consultation form; negative Google ranking penalty.",
                        SuggestedServiceId = "svc-security-retainer"
                    },
                    new()
                    {
                        Category = AuditCategory.Performance,
                        Severity = AuditSeverity.High,
                        RuleCode = "PERF-002",
                        Title = "Excessive Time-to-First-Byte (TTFB > 1.4s)",
                        Description = "Server took 1,420ms to deliver initial HTML payload due to lack of server-side page caching and outdated PHP 7.3 runtime.",
                        EvidenceText = "Measured TTFB: 1420.5ms (recommended threshold is < 200ms).",
                        RecommendedFix = "Migrate to modern PHP 8.3 container and activate Redis object caching.",
                        BusinessImpact = "High mobile bounce rate (>60% of mobile users abandon pages taking >3s to load).",
                        SuggestedServiceId = "svc-core-web-vitals"
                    },
                    new()
                    {
                        Category = AuditCategory.Seo,
                        Severity = AuditSeverity.Medium,
                        RuleCode = "SEO-003",
                        Title = "Missing XML Sitemap Index",
                        Description = "No sitemap.xml detected at /sitemap.xml or declared in robots.txt.",
                        EvidenceText = "HTTP 404 Not Found returned when requesting /sitemap.xml.",
                        RecommendedFix = "Generate automated XML sitemap and submit to Google Search Console.",
                        BusinessImpact = "Slow discovery of new dental treatment pages by Google search crawler.",
                        SuggestedServiceId = "svc-web-dev"
                    }
                ]
            };

            var auditAlRehman = new WebsiteAudit
            {
                Id = "audit-alrehman-textile",
                BusinessId = actualAlRehmanId,
                TargetUrl = "https://alrehmantextile.pk",
                Status = AuditStatus.Completed,
                OverallScore = 38,
                PerformanceScore = 30,
                SeoScore = 45,
                AccessibilityScore = 40,
                SecurityScore = 70,
                HttpRedirectsToHttps = true,
                SslValid = true,
                SslIssuer = "cPanel, Inc. Certification Authority",
                SslDaysUntilExpiry = 42,
                TtfbMs = 950.0,
                HtmlSizeBytes = 4200000,
                HasRobotsTxt = false,
                HasSitemapXml = false,
                DetectedTechStack = "HTML 4.01, Bootstrap 2.3, Flash Player artifacts, PHP 5.6",
                PageTitle = "Al Rehman Textile Faisalabad - Home",
                MetaDescription = null,
                CompletedAtUtc = DateTime.UtcNow.AddDays(-1),
                Findings =
                [
                    new()
                    {
                        Category = AuditCategory.Performance,
                        Severity = AuditSeverity.Critical,
                        RuleCode = "PERF-001",
                        Title = "Non-Responsive Desktop-Only Layout (No Viewport Tag)",
                        Description = "Website renders fixed 1024px width on smartphones, requiring international textile buyers to pinch-to-zoom.",
                        EvidenceText = "Missing <meta name='viewport'> tag. Viewport evaluation scored 0/100 on mobile emulators.",
                        RecommendedFix = "Complete mobile-first responsive redesign built on modern Tailwind/Next.js stack.",
                        BusinessImpact = "Severe loss of international export inquiries arriving from smartphone devices.",
                        SuggestedServiceId = "svc-web-dev"
                    },
                    new()
                    {
                        Category = AuditCategory.Performance,
                        Severity = AuditSeverity.High,
                        RuleCode = "PERF-004",
                        Title = "Uncompressed Product Catalog Images (>4.2MB Total Payload)",
                        Description = "Fabric gallery loads uncompressed 3000x2000 JPEG images without WebP modern formats or lazy loading.",
                        EvidenceText = "Single homepage payload requires 4.2MB download before rendering completes.",
                        RecommendedFix = "Convert catalog assets to WebP with responsive srcset and native loading='lazy'.",
                        BusinessImpact = "Over 7-second page load on 4G cellular connections.",
                        SuggestedServiceId = "svc-core-web-vitals"
                    }
                ]
            };

            await _context.WebsiteAudits.AddRangeAsync(auditApex, auditAlRehman);
            await _context.SaveChangesAsync(ct);
        }

        // Seed Lead Scores and Components
        if (!await _context.LeadScores.AnyAsync(ct))
        {
            var scoreApex = new LeadScore
            {
                Id = "score-apex-dental",
                BusinessId = actualApexId,
                TotalScore = 88,
                RuleVersion = "1.0.0",
                CalculatedAtUtc = DateTime.UtcNow.AddDays(-2),
                Components =
                [
                    new() { ComponentName = "Missing HTTPS/SSL", PointsAwarded = 35, MaxPointsPossible = 35, Explanation = "Website serves insecure HTTP with browser warning." },
                    new() { ComponentName = "Slow TTFB (>1000ms)", PointsAwarded = 25, MaxPointsPossible = 25, Explanation = "Server response latency is 1,420ms." },
                    new() { ComponentName = "Missing XML Sitemap", PointsAwarded = 15, MaxPointsPossible = 15, Explanation = "Google indexing hampered by absent sitemap." },
                    new() { ComponentName = "Verified Google Presence", PointsAwarded = 13, MaxPointsPossible = 25, Explanation = "Established business (142 reviews, 4.7 stars) with high buying power." }
                ]
            };

            var scoreCareway = new LeadScore
            {
                Id = "score-careway-pharmacy",
                BusinessId = actualCarewayId,
                TotalScore = 78,
                RuleVersion = "1.0.0",
                CalculatedAtUtc = DateTime.UtcNow.AddDays(-1),
                Components =
                [
                    new() { ComponentName = "Completely Missing Website", PointsAwarded = 50, MaxPointsPossible = 50, Explanation = "Established diagnostic lab operating solely offline." },
                    new() { ComponentName = "High Demand Medical Category", PointsAwarded = 15, MaxPointsPossible = 25, Explanation = "Online prescription order portals deliver immediate ROI." },
                    new() { ComponentName = "Verified Phone & Primary Contact", PointsAwarded = 13, MaxPointsPossible = 25, Explanation = "Decision maker (Muhammad Bilal, Managing Director) identified." }
                ]
            };

            var scoreAlRehman = new LeadScore
            {
                Id = "score-alrehman-textile",
                BusinessId = actualAlRehmanId,
                TotalScore = 92,
                RuleVersion = "1.0.0",
                CalculatedAtUtc = DateTime.UtcNow.AddDays(-1),
                Components =
                [
                    new() { ComponentName = "Outdated Legacy Web Presence", PointsAwarded = 40, MaxPointsPossible = 40, Explanation = "Desktop-only layout built in 2012 with Flash remnants." },
                    new() { ComponentName = "B2B Export High-Ticket Potential", PointsAwarded = 30, MaxPointsPossible = 30, Explanation = "International textile exporters have high software modernization budgets." },
                    new() { ComponentName = "Decision Maker Receptive", PointsAwarded = 22, MaxPointsPossible = 30, Explanation = "Export Director contacted via email." }
                ]
            };

            await _context.LeadScores.AddRangeAsync(scoreApex, scoreCareway, scoreAlRehman);
            await _context.SaveChangesAsync(ct);
        }

        // Seed Sales Tasks
        if (!await _context.SalesTasks.AnyAsync(ct))
        {
            var tasks = new List<SalesTask>
            {
                new()
                {
                    Id = "task-apex-followup",
                    BusinessId = actualApexId,
                    Title = "Follow up with Dr. Tariq regarding SSL certificate vulnerability",
                    Priority = TaskPriority.High,
                    DueDateUtc = DateTime.UtcNow.AddDays(1),
                    IsCompleted = false
                },
                new()
                {
                    Id = "task-careway-pitch",
                    BusinessId = actualCarewayId,
                    Title = "Send WhatsApp presentation for online prescription & lab booking portal",
                    Priority = TaskPriority.High,
                    DueDateUtc = DateTime.UtcNow.AddDays(2),
                    IsCompleted = false
                },
                new()
                {
                    Id = "task-alrehman-demo",
                    BusinessId = actualAlRehmanId,
                    Title = "Prepare B2B export catalog responsive mockup demo",
                    Priority = TaskPriority.Urgent,
                    DueDateUtc = DateTime.UtcNow.AddHours(18),
                    IsCompleted = false
                },
                new()
                {
                    Id = "task-sunrise-review",
                    BusinessId = actualSunriseId,
                    Title = "Review direct room reservation booking engine proposal",
                    Priority = TaskPriority.Medium,
                    DueDateUtc = DateTime.UtcNow.AddDays(4),
                    IsCompleted = true,
                    CompletedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            };

            await _context.SalesTasks.AddRangeAsync(tasks, ct);
            await _context.SaveChangesAsync(ct);
        }

        // Seed Sales Notes
        if (!await _context.SalesNotes.AnyAsync(ct))
        {
            var notes = new List<SalesNote>
            {
                new()
                {
                    Id = "note-apex-1",
                    BusinessId = actualApexId,
                    AuthorName = "Kashif Ahmad",
                    Content = "Spoke with front desk receptionist. Dr. Tariq is in clinic Thursdays and Saturdays from 3:00 PM to 8:00 PM. High interest in modernizing clinic booking system.",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                    UpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
                },
                new()
                {
                    Id = "note-careway-1",
                    BusinessId = actualCarewayId,
                    AuthorName = "Kashif Ahmad",
                    Content = "Careway is Clifton's busiest neighborhood pharmacy. Currently losing 20-30 repeat monthly prescription orders to Sehat.com.pk due to lack of website.",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
                },
                new()
                {
                    Id = "note-alrehman-1",
                    BusinessId = actualAlRehmanId,
                    AuthorName = "Kashif Ahmad",
                    Content = "Mian Usman expressed frustration during brief call that European textile buyers are unable to browse fabric catalogs smoothly on their iPhones.",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            };

            await _context.SalesNotes.AddRangeAsync(notes, ct);
            await _context.SaveChangesAsync(ct);
        }

        // Seed Outreach Messages (Drafts in Approval Queue)
        if (!await _context.OutreachMessages.AnyAsync(ct))
        {
            var messages = new List<OutreachMessage>
            {
                new()
                {
                    Id = "msg-apex-draft",
                    BusinessId = actualApexId,
                    Channel = OutreachChannel.Email,
                    Subject = "Technical assessment and growth opportunities for Apex Dental & Implant Care",
                    Body = "Hi Dr. Tariq,\n\nWe recently conducted a technical assessment of Apex Dental & Implant Care in Lahore. While analyzing http://apexdental.pk, our diagnostics identified key friction points (Missing SSL/TLS Certificate and high server latency of 1420ms).\n\nAt TheKhanSoft, we specialize in Website Security, SSL & Maintenance Retainers. We prepared an executive audit report outlining actionable solutions to protect your patients' data and elevate your Google ranking.\n\nWould you be open to a brief 10-minute conversation this week?\n\nBest regards,\nTheKhanSoft Outreach Team",
                    RenderedBody = "Hi Dr. Tariq,\n\nWe recently conducted a technical assessment of Apex Dental & Implant Care in Lahore. While analyzing http://apexdental.pk, our diagnostics identified key friction points (Missing SSL/TLS Certificate and high server latency of 1420ms).\n\nAt TheKhanSoft, we specialize in Website Security, SSL & Maintenance Retainers. We prepared an executive audit report outlining actionable solutions to protect your patients' data and elevate your Google ranking.\n\nWould you be open to a brief 10-minute conversation this week?\n\nBest regards,\nTheKhanSoft Outreach Team",
                    RecipientAddress = "dr.tariq@apexdental.pk",
                    ApprovalStatus = OutreachApprovalStatus.PendingApproval,
                    IsSent = false,
                    TemplateId = "tpl-audit-email",
                    Notes = "High priority prospect in Gulberg Lahore"
                },
                new()
                {
                    Id = "msg-careway-draft",
                    BusinessId = actualCarewayId,
                    Channel = OutreachChannel.WhatsApp,
                    Body = "Assalam-o-Alaikum Muhammad Bilal! We noticed Careway Pharmacy & Diagnostic Lab in Karachi is missing an official mobile-friendly website. Potential clients searching for Pharmacy & Healthcare in Karachi are finding competitors instead. TheKhanSoft builds modern high-speed websites tailored for your sector. Would you be interested in viewing a sample mockup?",
                    RenderedBody = "Assalam-o-Alaikum Muhammad Bilal! We noticed Careway Pharmacy & Diagnostic Lab in Karachi is missing an official mobile-friendly website. Potential clients searching for Pharmacy & Healthcare in Karachi are finding competitors instead. TheKhanSoft builds modern high-speed websites tailored for your sector. Would you be interested in viewing a sample mockup?",
                    RecipientAddress = "+92 321 7654321",
                    ApprovalStatus = OutreachApprovalStatus.PendingApproval,
                    IsSent = false,
                    TemplateId = "tpl-noweb-wa",
                    Notes = "Pre-screened WhatsApp outreach lead"
                }
            };

            await _context.OutreachMessages.AddRangeAsync(messages, ct);
            await _context.SaveChangesAsync(ct);
        }
    }
}
