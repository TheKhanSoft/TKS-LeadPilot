<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class OutreachMessage extends Model
{
    use SoftDeletes;

    protected $keyType = 'string';
    public $incrementing = false;

    protected $fillable = [
        'id',
        'business_id',
        'contact_id',
        'template_id',
        'channel',
        'recipient_address',
        'subject',
        'rendered_body',
        'approval_status',
        'dispatched_at_utc',
        'external_message_id',
        'error_message',
        'sync_version',
    ];

    protected function casts(): array
    {
        return [
            'channel' => 'integer',
            'approval_status' => 'integer',
            'dispatched_at_utc' => 'datetime',
            'sync_version' => 'integer',
        ];
    }

    public function business(): BelongsTo
    {
        return $this->belongsTo(Business::class, 'business_id');
    }

    public function contact(): BelongsTo
    {
        return $this->belongsTo(BusinessContact::class, 'contact_id');
    }

    public function template(): BelongsTo
    {
        return $this->belongsTo(OutreachTemplate::class, 'template_id');
    }
}
