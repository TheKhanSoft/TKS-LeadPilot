<?php

use App\Http\Controllers\Api\AuthController;
use App\Http\Controllers\Api\HealthController;
use App\Http\Controllers\Api\SyncController;
use Illuminate\Support\Facades\Route;

// V1 API Routes
Route::prefix('v1')->group(function () {
    // 1. Health & Status Check
    Route::get('/health', [HealthController::class, 'check']);

    // 2. Public Authentication
    Route::prefix('auth')->group(function () {
        Route::post('/register', [AuthController::class, 'register']);
        Route::post('/login', [AuthController::class, 'login']);
    });

    // 3. Protected Sync Endpoints (Sanctum Token Guard)
    Route::middleware('auth:sanctum')->group(function () {
        Route::get('/auth/me', [AuthController::class, 'me']);
        Route::post('/auth/device', [AuthController::class, 'registerDevice']);

        Route::prefix('sync')->group(function () {
            Route::post('/push', [SyncController::class, 'push']);
            Route::get('/pull', [SyncController::class, 'pull']);
            Route::get('/status', [SyncController::class, 'status']);
        });
    });

    // 4. Also support direct sync endpoints for local desktop / development testing with optional Sanctum
    // When no bearer token is provided in local dev mode, fallback or open endpoints can be tested
    Route::prefix('dev-sync')->group(function () {
        Route::post('/push', [SyncController::class, 'push']);
        Route::get('/pull', [SyncController::class, 'pull']);
        Route::get('/status', [SyncController::class, 'status']);
    });
});
