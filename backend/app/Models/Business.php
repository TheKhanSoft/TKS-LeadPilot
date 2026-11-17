<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;

class Business extends Model
{
    use SoftDeletes;

    protected $keyType = 'string';
    public $incrementing = false;

    protected $fillable = [
        'id',
        'name',
        'category',
        'city',
        'address_line1',
        'postal_code',
        'latitude',
        'longitude',
        'phone_primary',
        'email_primary',
        'website_url',
        'website_status',
        'pipeline_stage_id',
        'lead_status',
        'lead_score',
        'tags',
        'is_starred',
        'source_provider',
        'source_external_id',
        'google_place_id',
        'google_rating',
        'google_review_count',
        'is_google_verified',
        'business_hours',
        'last_enriched_at_utc',
        'sync_version',
    ];

    protected function casts(): array
    {
        return [
            'latitude' => 'double',
            'longitude' => 'double',
            'website_status' => 'integer',
            'lead_status' => 'integer',
            'lead_score' => 'integer',
            'is_starred' => 'boolean',
            'google_rating' => 'double',
            'google_review_count' => 'integer',
            'is_google_verified' => 'boolean',
            'last_enriched_at_utc' => 'datetime',
            'sync_version' => 'integer',
        ];
    }

    public function pipelineStage(): BelongsTo
    {
        return $this->belongsTo(PipelineStage::class, 'pipeline_stage_id');
    }

    public function contacts(): HasMany
    {
        return $this->hasMany(BusinessContact::class, 'business_id');
    }

    public function websiteAudits(): HasMany
    {
        return $this->hasMany(WebsiteAudit::class, 'business_id');
    }

    public function salesTasks(): HasMany
    {
        return $this->hasMany(SalesTask::class, 'business_id');
    }

    public function salesNotes(): HasMany
    {
        return $this->hasMany(SalesNote::class, 'business_id');
    }

    public function outreachMessages(): HasMany
    {
        return $this->hasMany(OutreachMessage::class, 'business_id');
    }
}
