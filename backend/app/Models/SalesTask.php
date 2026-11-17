<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class SalesTask extends Model
{
    use SoftDeletes;

    protected $keyType = 'string';
    public $incrementing = false;

    protected $fillable = [
        'id',
        'business_id',
        'title',
        'due_date_utc',
        'is_completed',
        'completed_at_utc',
        'sync_version',
    ];

    protected function casts(): array
    {
        return [
            'due_date_utc' => 'datetime',
            'is_completed' => 'boolean',
            'completed_at_utc' => 'datetime',
            'sync_version' => 'integer',
        ];
    }

    public function business(): BelongsTo
    {
        return $this->belongsTo(Business::class, 'business_id');
    }
}
