<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Business;
use App\Models\BusinessContact;
use App\Models\Device;
use App\Models\OutreachMessage;
use App\Models\OutreachTemplate;
use App\Models\PipelineStage;
use App\Models\SalesNote;
use App\Models\SalesTask;
use App\Models\SyncConflict;
use App\Models\WebsiteAudit;
use Carbon\Carbon;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;

class SyncController extends Controller
{
    /**
     * Batch push local client changes into the central database.
     */
    public function push(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'device_id' => 'required|string',
            'businesses' => 'nullable|array',
            'contacts' => 'nullable|array',
            'website_audits' => 'nullable|array',
            'pipeline_stages' => 'nullable|array',
            'sales_tasks' => 'nullable|array',
            'sales_notes' => 'nullable|array',
            'outreach_templates' => 'nullable|array',
            'outreach_messages' => 'nullable|array',
        ]);

        $deviceId = $validated['device_id'];
        $processedCount = 0;
        $conflictsCount = 0;
        $conflicts = [];

        DB::beginTransaction();
        try {
            // 1. Pipeline Stages
            if (!empty($validated['pipeline_stages'])) {
                foreach ($validated['pipeline_stages'] as $item) {
                    $res = $this->upsertEntity(PipelineStage::class, $item, $deviceId);
                    if ($res['conflict']) {
                        $conflictsCount++;
                        $conflicts[] = $res['conflict_record'];
                    } else {
                        $processedCount++;
                    }
                }
            }

            // 2. Businesses
            if (!empty($validated['businesses'])) {
                foreach ($validated['businesses'] as $item) {
                    $res = $this->upsertEntity(Business::class, $item, $deviceId);
                    if ($res['conflict']) {
                        $conflictsCount++;
                        $conflicts[] = $res['conflict_record'];
                    } else {
                        $processedCount++;
                    }
                }
            }

            // 3. Contacts
            if (!empty($validated['contacts'])) {
                foreach ($validated['contacts'] as $item) {
                    $res = $this->upsertEntity(BusinessContact::class, $item, $deviceId);
                    if ($res['conflict']) {
                        $conflictsCount++;
                        $conflicts[] = $res['conflict_record'];
                    } else {
                        $processedCount++;
                    }
                }
            }

            // 4. Website Audits
            if (!empty($validated['website_audits'])) {
                foreach ($validated['website_audits'] as $item) {
                    $res = $this->upsertEntity(WebsiteAudit::class, $item, $deviceId);
                    if ($res['conflict']) {
                        $conflictsCount++;
                        $conflicts[] = $res['conflict_record'];
                    } else {
                        $processedCount++;
                    }
                }
            }

            // 5. Sales Tasks
            if (!empty($validated['sales_tasks'])) {
                foreach ($validated['sales_tasks'] as $item) {
                    $res = $this->upsertEntity(SalesTask::class, $item, $deviceId);
                    if ($res['conflict']) {
                        $conflictsCount++;
                        $conflicts[] = $res['conflict_record'];
                    } else {
                        $processedCount++;
                    }
                }
            }

            // 6. Sales Notes
            if (!empty($validated['sales_notes'])) {
                foreach ($validated['sales_notes'] as $item) {
                    $res = $this->upsertEntity(SalesNote::class, $item, $deviceId);
                    if ($res['conflict']) {
                        $conflictsCount++;
                        $conflicts[] = $res['conflict_record'];
                    } else {
                        $processedCount++;
                    }
                }
            }

            // 7. Outreach Templates
            if (!empty($validated['outreach_templates'])) {
                foreach ($validated['outreach_templates'] as $item) {
                    $res = $this->upsertEntity(OutreachTemplate::class, $item, $deviceId);
                    if ($res['conflict']) {
                        $conflictsCount++;
                        $conflicts[] = $res['conflict_record'];
                    } else {
                        $processedCount++;
                    }
                }
            }

            // 8. Outreach Messages
            if (!empty($validated['outreach_messages'])) {
                foreach ($validated['outreach_messages'] as $item) {
                    $res = $this->upsertEntity(OutreachMessage::class, $item, $deviceId);
                    if ($res['conflict']) {
                        $conflictsCount++;
                        $conflicts[] = $res['conflict_record'];
                    } else {
                        $processedCount++;
                    }
                }
            }

            // Update device sync timestamp
            Device::where('device_id', $deviceId)->update(['last_synced_at' => now()]);

            DB::commit();

            return response()->json([
                'success' => true,
                'pushed_count' => $processedCount,
                'conflicts_count' => $conflictsCount,
                'conflicts' => $conflicts,
                'server_timestamp' => now()->toIso8601String(),
            ]);
        } catch (\Throwable $e) {
            DB::rollBack();
            return response()->json([
                'success' => false,
                'error' => $e->getMessage(),
            ], 500);
        }
    }

    /**
     * Pull delta changes updated or soft-deleted since client's cursor.
     */
    public function pull(Request $request): JsonResponse
    {
        $sinceParam = $request->query('since');
        $since = null;

        if (!empty($sinceParam)) {
            try {
                $since = Carbon::parse($sinceParam);
            } catch (\Throwable) {
                $since = null;
            }
        }

        $serverNow = now()->toIso8601String();

        return response()->json([
            'server_timestamp' => $serverNow,
            'businesses' => $this->getDeltaRecords(Business::class, $since),
            'contacts' => $this->getDeltaRecords(BusinessContact::class, $since),
            'website_audits' => $this->getDeltaRecords(WebsiteAudit::class, $since),
            'pipeline_stages' => $this->getDeltaRecords(PipelineStage::class, $since),
            'sales_tasks' => $this->getDeltaRecords(SalesTask::class, $since),
            'sales_notes' => $this->getDeltaRecords(SalesNote::class, $since),
            'outreach_templates' => $this->getDeltaRecords(OutreachTemplate::class, $since),
            'outreach_messages' => $this->getDeltaRecords(OutreachMessage::class, $since),
        ]);
    }

    /**
     * Get summary sync status and health.
     */
    public function status(): JsonResponse
    {
        return response()->json([
            'server_timestamp' => now()->toIso8601String(),
            'total_counts' => [
                'businesses' => Business::count(),
                'contacts' => BusinessContact::count(),
                'audits' => WebsiteAudit::count(),
                'stages' => PipelineStage::count(),
                'tasks' => SalesTask::count(),
                'notes' => SalesNote::count(),
                'outreach_messages' => OutreachMessage::count(),
                'outreach_templates' => OutreachTemplate::count(),
                'devices' => Device::count(),
                'conflicts' => SyncConflict::count(),
            ],
        ]);
    }

    /**
     * Helper to upsert single entity with conflict detection (LWW).
     */
    private function upsertEntity(string $modelClass, array $data, string $deviceId): array
    {
        if (empty($data['id'])) {
            return ['conflict' => false, 'conflict_record' => null];
        }

        $existing = $modelClass::withTrashed()->find($data['id']);
        $clientVersion = (int)($data['sync_version'] ?? 1);

        // Soft deletion check
        $isDeleted = !empty($data['is_deleted']) || !empty($data['deleted_at']);

        if ($existing) {
            $serverVersion = (int)($existing->sync_version ?? 1);

            // Conflict condition: server has newer revision
            if ($serverVersion > $clientVersion) {
                $conflict = SyncConflict::create([
                    'entity_type' => class_basename($modelClass),
                    'entity_id' => $data['id'],
                    'client_device_id' => $deviceId,
                    'client_version' => $clientVersion,
                    'server_version' => $serverVersion,
                    'client_payload' => $data,
                    'server_payload' => $existing->toArray(),
                    'resolution_status' => 'ServerWins',
                ]);

                return ['conflict' => true, 'conflict_record' => $conflict];
            }

            // Client version is equal or newer -> apply update
            unset($data['is_deleted']);
            $data['sync_version'] = max($clientVersion, $serverVersion) + 1;

            if ($isDeleted) {
                $existing->update($data);
                $existing->delete();
            } else {
                if ($existing->trashed()) {
                    $existing->restore();
                }
                $existing->update($data);
            }
        } else {
            // New record insertion
            unset($data['is_deleted']);
            $data['sync_version'] = max(1, $clientVersion);

            if ($isDeleted) {
                $created = $modelClass::create($data);
                $created->delete();
            } else {
                $modelClass::create($data);
            }
        }

        return ['conflict' => false, 'conflict_record' => null];
    }

    /**
     * Helper to pull delta records with soft-deleted tombstones.
     */
    private function getDeltaRecords(string $modelClass, ?Carbon $since): array
    {
        $query = $modelClass::withTrashed();

        if ($since !== null) {
            $query->where(function ($q) use ($since) {
                $q->where('updated_at', '>', $since)
                  ->orWhere('deleted_at', '>', $since);
            });
        }

        return $query->get()->map(function ($model) {
            $arr = $model->toArray();
            $arr['is_deleted'] = $model->trashed();
            return $arr;
        })->all();
    }
}
