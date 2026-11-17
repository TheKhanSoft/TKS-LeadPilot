<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Run the migrations for LeadPilot synchronization infrastructure.
     */
    public function up(): void
    {
        // 1. Registered Devices
        Schema::create('devices', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->nullable()->constrained()->nullOnDelete();
            $table->string('device_id')->unique();
            $table->string('device_name');
            $table->string('platform')->default('windows');
            $table->timestamp('last_synced_at')->nullable();
            $table->timestamps();
        });

        // 2. Sync Conflicts
        Schema::create('sync_conflicts', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->string('entity_type');
            $table->string('entity_id');
            $table->string('client_device_id');
            $table->integer('client_version')->default(1);
            $table->integer('server_version')->default(1);
            $table->json('client_payload')->nullable();
            $table->json('server_payload')->nullable();
            $table->string('resolution_status')->default('ServerWins');
            $table->timestamps();
        });

        // 3. Pipeline Stages
        Schema::create('pipeline_stages', function (Blueprint $table) {
            $table->string('id')->primary(); // Preserves client-generated UUID / code
            $table->string('name');
            $table->string('code')->unique();
            $table->integer('order_index')->default(0);
            $table->string('color_hex')->nullable();
            $table->text('description')->nullable();
            $table->integer('sync_version')->default(1);
            $table->softDeletes();
            $table->timestamps();
        });

        // 4. Businesses
        Schema::create('businesses', function (Blueprint $table) {
            $table->string('id')->primary(); // UUIDv4
            $table->string('name');
            $table->string('category')->nullable()->index();
            $table->string('city')->nullable()->index();
            $table->string('address_line1')->nullable();
            $table->string('postal_code')->nullable();
            $table->double('latitude')->nullable();
            $table->double('longitude')->nullable();
            $table->string('phone_primary')->nullable();
            $table->string('email_primary')->nullable();
            $table->string('website_url')->nullable();
            $table->integer('website_status')->default(0);
            $table->string('pipeline_stage_id')->nullable()->index();
            $table->integer('lead_status')->default(0);
            $table->integer('lead_score')->default(0);
            $table->text('tags')->nullable();
            $table->boolean('is_starred')->default(false);
            $table->string('source_provider')->nullable();
            $table->string('source_external_id')->nullable();
            $table->string('google_place_id')->nullable();
            $table->double('google_rating')->nullable();
            $table->integer('google_review_count')->nullable();
            $table->boolean('is_google_verified')->default(false);
            $table->text('business_hours')->nullable();
            $table->timestamp('last_enriched_at_utc')->nullable();
            $table->integer('sync_version')->default(1);
            $table->softDeletes();
            $table->timestamps();
        });

        // 5. Business Contacts
        Schema::create('business_contacts', function (Blueprint $table) {
            $table->string('id')->primary(); // UUIDv4
            $table->string('business_id')->index();
            $table->string('full_name');
            $table->string('job_title')->nullable();
            $table->string('phone')->nullable();
            $table->string('email')->nullable();
            $table->boolean('is_primary')->default(false);
            $table->integer('sync_version')->default(1);
            $table->softDeletes();
            $table->timestamps();
        });

        // 6. Website Audits
        Schema::create('website_audits', function (Blueprint $table) {
            $table->string('id')->primary(); // UUIDv4
            $table->string('business_id')->index();
            $table->string('audited_url');
            $table->integer('overall_score')->default(0);
            $table->integer('security_score')->default(0);
            $table->integer('performance_score')->default(0);
            $table->integer('seo_score')->default(0);
            $table->double('ttfb_ms')->default(0);
            $table->double('page_size_kb')->default(0);
            $table->boolean('ssl_valid')->default(false);
            $table->string('ssl_issuer')->nullable();
            $table->timestamp('ssl_expires_at')->nullable();
            $table->string('detected_cms')->nullable();
            $table->text('detected_tech_stack')->nullable();
            $table->json('findings_json')->nullable();
            $table->integer('sync_version')->default(1);
            $table->softDeletes();
            $table->timestamps();
        });

        // 7. Sales Tasks
        Schema::create('sales_tasks', function (Blueprint $table) {
            $table->string('id')->primary(); // UUIDv4
            $table->string('business_id')->index();
            $table->string('title');
            $table->timestamp('due_date_utc')->nullable();
            $table->boolean('is_completed')->default(false);
            $table->timestamp('completed_at_utc')->nullable();
            $table->integer('sync_version')->default(1);
            $table->softDeletes();
            $table->timestamps();
        });

        // 8. Sales Notes
        Schema::create('sales_notes', function (Blueprint $table) {
            $table->string('id')->primary(); // UUIDv4
            $table->string('business_id')->index();
            $table->text('content');
            $table->string('author_name')->nullable();
            $table->integer('sync_version')->default(1);
            $table->softDeletes();
            $table->timestamps();
        });

        // 9. Outreach Templates
        Schema::create('outreach_templates', function (Blueprint $table) {
            $table->string('id')->primary(); // UUIDv4
            $table->string('name');
            $table->integer('channel')->default(0);
            $table->string('category')->nullable();
            $table->string('subject_template')->nullable();
            $table->text('body_template');
            $table->integer('sync_version')->default(1);
            $table->softDeletes();
            $table->timestamps();
        });

        // 10. Outreach Messages
        Schema::create('outreach_messages', function (Blueprint $table) {
            $table->string('id')->primary(); // UUIDv4
            $table->string('business_id')->index();
            $table->string('contact_id')->nullable()->index();
            $table->string('template_id')->nullable()->index();
            $table->integer('channel')->default(0);
            $table->string('recipient_address');
            $table->string('subject')->nullable();
            $table->text('rendered_body');
            $table->integer('approval_status')->default(0);
            $table->timestamp('dispatched_at_utc')->nullable();
            $table->string('external_message_id')->nullable();
            $table->text('error_message')->nullable();
            $table->integer('sync_version')->default(1);
            $table->softDeletes();
            $table->timestamps();
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::dropIfExists('outreach_messages');
        Schema::dropIfExists('outreach_templates');
        Schema::dropIfExists('sales_notes');
        Schema::dropIfExists('sales_tasks');
        Schema::dropIfExists('website_audits');
        Schema::dropIfExists('business_contacts');
        Schema::dropIfExists('businesses');
        Schema::dropIfExists('pipeline_stages');
        Schema::dropIfExists('sync_conflicts');
        Schema::dropIfExists('devices');
    }
};
