package com.skeboss.minion;

import org.bukkit.Bukkit;
import org.bukkit.Location;
import org.bukkit.World;

import java.util.UUID;

public final class MinionSpawner {

    private final String id;
    private final String presetId;
    private final String worldName;
    private final double x;
    private final double y;
    private final double z;
    private final float yaw;
    private UUID activeMinionId;

    public MinionSpawner(String id, String presetId, Location location) {
        this.id = id;
        this.presetId = presetId == null || presetId.isBlank() ? null : presetId;
        this.worldName = location.getWorld().getName();
        this.x = location.getX();
        this.y = location.getY();
        this.z = location.getZ();
        this.yaw = location.getYaw();
    }

    public MinionSpawner(String id, String presetId, String worldName, double x, double y, double z, float yaw) {
        this.id = id;
        this.presetId = presetId == null || presetId.isBlank() ? null : presetId;
        this.worldName = worldName;
        this.x = x;
        this.y = y;
        this.z = z;
        this.yaw = yaw;
    }

    public String getId() {
        return id;
    }

    public String getPresetId() {
        return presetId;
    }

    public Location toLocation() {
        World world = Bukkit.getWorld(worldName);
        if (world == null) {
            return null;
        }
        Location location = new Location(world, x, y, z);
        location.setYaw(yaw);
        return location;
    }

    public UUID getActiveMinionId() {
        return activeMinionId;
    }

    public void setActiveMinionId(UUID activeMinionId) {
        this.activeMinionId = activeMinionId;
    }

    public void clearActiveMinion() {
        this.activeMinionId = null;
    }
}
