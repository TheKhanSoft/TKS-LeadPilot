<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class WebsiteAudit extends Model
{
    use SoftDeletes;

    protected $keyType = 'string';
    public $incrementing = false;

    protected $fillable = [
        'id',
        'business_id',
        'audited_url',
        'overall_score',
        'security_score',
        'performance_score',
        'seo_score',
        'ttfb_ms',
        'page_size_kb',
        'ssl_valid',
        'ssl_issuer',
        'ssl_expires_at',
        'detected_cms',
        'detected_tech_stack',
        'findings_json',
        'sync_version',
    ];

    protected function casts(): array
    {
        return [
            'overall_score' => 'integer',
            'security_score' => 'integer',
            'performance_score' => 'integer',
            'seo_score' => 'integer',
            'ttfb_ms' => 'double',
            'page_size_kb' => 'double',
            'ssl_valid' => 'boolean',
            'ssl_expires_at' => 'datetime',
            'findings_json' => 'array',
            'sync_version' => 'integer',
        ];
    }

    public function business(): BelongsTo
    {
        return $this->belongsTo(Business::class, 'business_id');
    }
}
