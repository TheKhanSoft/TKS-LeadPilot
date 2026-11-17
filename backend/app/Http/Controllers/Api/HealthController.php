<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use Illuminate\Http\JsonResponse;
use Illuminate\Support\Facades\DB;

class HealthController extends Controller
{
    /**
     * Check API health, version, and database connectivity.
     */
    public function check(): JsonResponse
    {
        $dbConnected = false;
        $dbError = null;

        try {
            DB::connection()->getPdo();
            $dbConnected = true;
        } catch (\Throwable $e) {
            $dbError = $e->getMessage();
        }

        return response()->json([
            'status' => $dbConnected ? 'healthy' : 'degraded',
            'api_name' => 'TKS LeadPilot Central API',
            'version' => '1.0.0',
            'timestamp' => now()->toIso8601String(),
            'database' => [
                'connected' => $dbConnected,
                'driver' => DB::connection()->getDriverName(),
                'error' => $dbError,
            ],
            'sync_protocol' => 'v1-uuid-delta',
        ], $dbConnected ? 200 : 503);
    }
}
