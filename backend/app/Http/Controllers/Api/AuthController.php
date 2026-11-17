<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Device;
use App\Models\User;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Hash;
use Illuminate\Validation\ValidationException;

class AuthController extends Controller
{
    /**
     * Register a new user and generate access token.
     */
    public function register(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'name' => 'required|string|max:255',
            'email' => 'required|string|email|max:255|unique:users',
            'password' => 'required|string|min:8',
            'device_id' => 'nullable|string',
            'device_name' => 'nullable|string',
            'platform' => 'nullable|string',
        ]);

        $user = User::create([
            'name' => $validated['name'],
            'email' => $validated['email'],
            'password' => Hash::make($validated['password']),
        ]);

        $deviceName = $validated['device_name'] ?? 'Primary Client';
        $token = $user->createToken($deviceName)->plainTextToken;

        if (!empty($validated['device_id'])) {
            Device::updateOrCreate(
                ['device_id' => $validated['device_id']],
                [
                    'user_id' => $user->id,
                    'device_name' => $deviceName,
                    'platform' => $validated['platform'] ?? 'windows',
                    'last_synced_at' => null,
                ]
            );
        }

        return response()->json([
            'user' => [
                'id' => $user->id,
                'name' => $user->name,
                'email' => $user->email,
            ],
            'token' => $token,
        ], 201);
    }

    /**
     * Authenticate existing user and generate access token.
     */
    public function login(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'email' => 'required|string|email',
            'password' => 'required|string',
            'device_id' => 'nullable|string',
            'device_name' => 'nullable|string',
            'platform' => 'nullable|string',
        ]);

        $user = User::where('email', $validated['email'])->first();

        if (!$user || !Hash::check($validated['password'], $user->password)) {
            throw ValidationException::withMessages([
                'email' => ['The provided credentials are incorrect.'],
            ]);
        }

        $deviceName = $validated['device_name'] ?? 'Primary Client';
        $token = $user->createToken($deviceName)->plainTextToken;

        if (!empty($validated['device_id'])) {
            Device::updateOrCreate(
                ['device_id' => $validated['device_id']],
                [
                    'user_id' => $user->id,
                    'device_name' => $deviceName,
                    'platform' => $validated['platform'] ?? 'windows',
                    'last_synced_at' => now(),
                ]
            );
        }

        return response()->json([
            'user' => [
                'id' => $user->id,
                'name' => $user->name,
                'email' => $user->email,
            ],
            'token' => $token,
        ]);
    }

    /**
     * Register or update client device.
     */
    public function registerDevice(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'device_id' => 'required|string',
            'device_name' => 'required|string',
            'platform' => 'nullable|string',
        ]);

        $user = $request->user();

        $device = Device::updateOrCreate(
            ['device_id' => $validated['device_id']],
            [
                'user_id' => $user?->id,
                'device_name' => $validated['device_name'],
                'platform' => $validated['platform'] ?? 'windows',
                'last_synced_at' => now(),
            ]
        );

        return response()->json([
            'device' => $device,
        ]);
    }

    /**
     * Current authenticated user.
     */
    public function me(Request $request): JsonResponse
    {
        $user = $request->user();
        $devices = Device::where('user_id', $user->id)->get();

        return response()->json([
            'user' => $user,
            'devices' => $devices,
        ]);
    }
}
