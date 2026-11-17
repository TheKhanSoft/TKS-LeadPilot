<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;

class OutreachTemplate extends Model
{
    use SoftDeletes;

    protected $keyType = 'string';
    public $incrementing = false;

    protected $fillable = [
        'id',
        'name',
        'channel',
        'category',
        'subject_template',
        'body_template',
        'sync_version',
    ];

    protected function casts(): array
    {
        return [
            'channel' => 'integer',
            'sync_version' => 'integer',
        ];
    }
}
