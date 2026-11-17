<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Concerns\HasUuids;
use Illuminate\Database\Eloquent\Model;

class SyncConflict extends Model
{
    use HasUuids;

    protected $keyType = 'string';
    public $incrementing = false;

    protected $fillable = [
        'id',
        'entity_type',
        'entity_id',
        'client_device_id',
        'client_version',
        'server_version',
        'client_payload',
        'server_payload',
        'resolution_status',
    ];

    protected function casts(): array
    {
        return [
            'client_payload' => 'array',
            'server_payload' => 'array',
            'client_version' => 'integer',
            'server_version' => 'integer',
        ];
    }
}
