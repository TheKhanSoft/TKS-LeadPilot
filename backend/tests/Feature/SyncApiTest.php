<?php

namespace Tests\Feature;

use App\Models\Business;
use App\Models\BusinessContact;
use App\Models\Device;
use App\Models\SyncConflict;
use App\Models\User;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Str;
use Tests\TestCase;

class SyncApiTest extends TestCase
{
    use RefreshDatabase;

    public function test_health_check_returns_healthy_status(): void
    {
        $response = $this->getJson('/api/v1/health');

        $response->assertStatus(200)
            ->assertJson([
                'status' => 'healthy',
                'api_name' => 'TKS LeadPilot Central API',
                'version' => '1.0.0',
            ]);
    }

    public function test_auth_workflow_registers_and_authenticates_device(): void
    {
        $regResponse = $this->postJson('/api/v1/auth/register', [
            'name' => 'Kashif Khan',
            'email' => 'kashif@thekhansoft.com',
            'password' => 'SecurePass123!',
            'device_id' => 'dev-workstation-001',
            'device_name' => 'Office Windows Desktop',
            'platform' => 'windows',
        ]);

        $regResponse->assertStatus(201)
            ->assertJsonStructure(['user' => ['id', 'name', 'email'], 'token']);

        $token = $regResponse->json('token');

        // Test authenticated me endpoint
        $meResponse = $this->withHeader('Authorization', "Bearer $token")
            ->getJson('/api/v1/auth/me');

        $meResponse->assertStatus(200)
            ->assertJsonPath('user.email', 'kashif@thekhansoft.com');

        $this->assertDatabaseHas('devices', [
            'device_id' => 'dev-workstation-001',
            'device_name' => 'Office Windows Desktop',
        ]);
    }

    public function test_sync_push_creates_and_updates_businesses_and_contacts_idempotently(): void
    {
        $user = User::factory()->create();
        $token = $user->createToken('test-device')->plainTextToken;

        $bizId = Str::uuid()->toString();
        $contactId = Str::uuid()->toString();

        $payload = [
            'device_id' => 'device-test-uuid',
            'businesses' => [
                [
                    'id' => $bizId,
                    'name' => 'Al-Razi Medical Diagnostics',
                    'category' => 'Medical & Diagnostic Center',
                    'city' => 'Islamabad',
                    'website_url' => 'https://alrazi.pk',
                    'lead_score' => 84,
                    'sync_version' => 1,
                ],
            ],
            'contacts' => [
                [
                    'id' => $contactId,
                    'business_id' => $bizId,
                    'full_name' => 'Dr. Salman Ahmed',
                    'email' => 'dr.salman@alrazi.pk',
                    'is_primary' => true,
                    'sync_version' => 1,
                ],
            ],
        ];

        // 1. Initial Push
        $pushResponse = $this->withHeader('Authorization', "Bearer $token")
            ->postJson('/api/v1/sync/push', $payload);

        $pushResponse->assertStatus(200)
            ->assertJson([
                'success' => true,
                'pushed_count' => 2,
                'conflicts_count' => 0,
            ]);

        $this->assertDatabaseHas('businesses', [
            'id' => $bizId,
            'name' => 'Al-Razi Medical Diagnostics',
        ]);
        $this->assertDatabaseHas('business_contacts', [
            'id' => $contactId,
            'full_name' => 'Dr. Salman Ahmed',
        ]);

        // 2. Idempotent Second Push with updated lead score
        $payload['businesses'][0]['lead_score'] = 92;
        $payload['businesses'][0]['sync_version'] = 2;

        $updateResponse = $this->withHeader('Authorization', "Bearer $token")
            ->postJson('/api/v1/sync/push', $payload);

        $updateResponse->assertStatus(200)
            ->assertJson(['success' => true, 'conflicts_count' => 0]);

        $this->assertDatabaseHas('businesses', [
            'id' => $bizId,
            'lead_score' => 92,
        ]);
    }

    public function test_sync_pull_returns_delta_records_with_tombstones(): void
    {
        $biz1 = Business::create([
            'id' => Str::uuid()->toString(),
            'name' => 'Rawal Dental Clinic',
            'city' => 'Rawalpindi',
            'sync_version' => 1,
        ]);

        $biz2 = Business::create([
            'id' => Str::uuid()->toString(),
            'name' => 'Parked Horizon Corp',
            'city' => 'Lahore',
            'sync_version' => 1,
        ]);
        $biz2->delete(); // Soft-deleted tombstone

        $response = $this->getJson('/api/v1/dev-sync/pull');

        $response->assertStatus(200)
            ->assertJsonStructure(['server_timestamp', 'businesses', 'contacts']);

        $businesses = collect($response->json('businesses'));

        $rawBiz = $businesses->firstWhere('id', $biz1->id);
        $this->assertNotNull($rawBiz);
        $this->assertFalse($rawBiz['is_deleted']);

        $deletedBiz = $businesses->firstWhere('id', $biz2->id);
        $this->assertNotNull($deletedBiz);
        $this->assertTrue($deletedBiz['is_deleted']);
    }

    public function test_sync_conflict_detection_preserves_newer_server_version(): void
    {
        $bizId = Str::uuid()->toString();

        // Server has version 5
        $serverBiz = Business::create([
            'id' => $bizId,
            'name' => 'Server Authoritative Enterprise',
            'city' => 'Lahore',
            'sync_version' => 5,
        ]);

        // Client pushes older version 2
        $payload = [
            'device_id' => 'stale-client-device',
            'businesses' => [
                [
                    'id' => $bizId,
                    'name' => 'Stale Client Overwrite Attempt',
                    'city' => 'Lahore',
                    'sync_version' => 2,
                ],
            ],
        ];

        $response = $this->postJson('/api/v1/dev-sync/push', $payload);

        $response->assertStatus(200)
            ->assertJson([
                'success' => true,
                'conflicts_count' => 1,
            ]);

        // Assert Server record was NOT overwritten
        $serverBiz->refresh();
        $this->assertEquals('Server Authoritative Enterprise', $serverBiz->name);
        $this->assertEquals(5, $serverBiz->sync_version);

        // Assert Conflict log was recorded
        $this->assertDatabaseHas('sync_conflicts', [
            'entity_type' => 'Business',
            'entity_id' => $bizId,
            'client_version' => 2,
            'server_version' => 5,
            'resolution_status' => 'ServerWins',
        ]);
    }
}
