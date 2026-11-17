<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\HasMany;

class PipelineStage extends Model
{
    use SoftDeletes;

    protected $keyType = 'string';
    public $incrementing = false;

    protected $fillable = [
        'id',
        'name',
        'code',
        'order_index',
        'color_hex',
        'description',
        'sync_version',
    ];

    protected function casts(): array
    {
        return [
            'order_index' => 'integer',
            'sync_version' => 'integer',
        ];
    }

    public function businesses(): HasMany
    {
        return $this->hasMany(Business::class, 'pipeline_stage_id');
    }
}
