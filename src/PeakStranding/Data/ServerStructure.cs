using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace PeakStranding.Data;

[Serializable]
public class ServerStructureDto
{
    public ulong user_id;
    public string username;
    public int map_id;
    public string scene;
    public int segment;
    public string prefab;

    public float pos_x, pos_y, pos_z;
    public float rot_x, rot_y, rot_z, rot_w;

    public float rope_start_x, rope_start_y, rope_start_z;
    public float rope_end_x, rope_end_y, rope_end_z;
    public float rope_length;

    public float rope_flying_rotation_x, rope_flying_rotation_y, rope_flying_rotation_z;
    public float rope_anchor_rotation_x, rope_anchor_rotation_y, rope_anchor_rotation_z, rope_anchor_rotation_w;

    public bool antigrav;
}

public static class ServerStructureConverters
{
    public static ServerStructureDto ToServerDto(
        this PlacedItemData item,
        ulong userSteamId,
        string username,
        int mapId)
    {
        return new ServerStructureDto
        {
            user_id = userSteamId,
            username = username,
            map_id = mapId,
            scene = item.Scene,
            segment = item.MapSegment,
            prefab = item.PrefabName,

            pos_x = item.Position.x,
            pos_y = item.Position.y,
            pos_z = item.Position.z,

            rot_x = item.Rotation.x,
            rot_y = item.Rotation.y,
            rot_z = item.Rotation.z,
            rot_w = item.Rotation.w,

            rope_start_x = item.RopeStart.x,
            rope_start_y = item.RopeStart.y,
            rope_start_z = item.RopeStart.z,

            rope_end_x = item.RopeEnd.x,
            rope_end_y = item.RopeEnd.y,
            rope_end_z = item.RopeEnd.z,

            rope_length = item.RopeLength,

            rope_flying_rotation_x = item.RopeFlyingRotation.x,
            rope_flying_rotation_y = item.RopeFlyingRotation.y,
            rope_flying_rotation_z = item.RopeFlyingRotation.z,

            rope_anchor_rotation_x = item.RopeAnchorRotation.x,
            rope_anchor_rotation_y = item.RopeAnchorRotation.y,
            rope_anchor_rotation_z = item.RopeAnchorRotation.z,
            rope_anchor_rotation_w = item.RopeAnchorRotation.w,

            antigrav = item.RopeAntiGrav
        };
    }

    public static PlacedItemData ToPlacedItemData(this ServerStructureDto s)
    {
        return new PlacedItemData
        {
            PrefabName = s.prefab,

            Position = new Vector3(s.pos_x, s.pos_y, s.pos_z),
            Rotation = new Quaternion(s.rot_x, s.rot_y, s.rot_z, s.rot_w),

            RopeStart = new Vector3(s.rope_start_x, s.rope_start_y, s.rope_start_z),
            RopeEnd = new Vector3(s.rope_end_x, s.rope_end_y, s.rope_end_z),
            RopeLength = s.rope_length,

            RopeFlyingRotation = new Vector3(
                                    s.rope_flying_rotation_x,
                                    s.rope_flying_rotation_y,
                                    s.rope_flying_rotation_z),

            RopeAnchorRotation = new Quaternion(
                                    s.rope_anchor_rotation_x,
                                    s.rope_anchor_rotation_y,
                                    s.rope_anchor_rotation_z,
                                    s.rope_anchor_rotation_w),

            RopeAntiGrav = s.antigrav,

            Scene = s.scene,
            MapSegment = s.segment
        };
    }

    public static List<PlacedItemData> ToPlacedItemList(
        this IEnumerable<ServerStructureDto> rows)
        => rows.Select(dto => dto.ToPlacedItemData())
               .ToList();
}
