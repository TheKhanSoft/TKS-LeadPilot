<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class BusinessContact extends Model
{
    use SoftDeletes;

    protected $keyType = 'string';
    public $incrementing = false;

    protected $fillable = [
        'id',
        'business_id',
        'full_name',
        'job_title',
        'phone',
        'email',
        'is_primary',
        'sync_version',
    ];

    protected function casts(): array
    {
        return [
            'is_primary' => 'boolean',
            'sync_version' => 'integer',
        ];
    }

    public function business(): BelongsTo
    {
        return $this->belongsTo(Business::class, 'business_id');
    }
}
