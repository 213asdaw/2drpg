package com.skeboss.minion;

import com.skeboss.SkeBossPlugin;
import org.bukkit.Bukkit;
import org.bukkit.Location;
import org.bukkit.configuration.file.FileConfiguration;
import org.bukkit.configuration.file.YamlConfiguration;

import java.io.File;
import java.io.IOException;
import java.util.Collection;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;

public final class MinionSpawnerStorage {

    private final SkeBossPlugin plugin;
    private final File file;
    private final Map<String, MinionSpawner> spawners = new LinkedHashMap<>();

    public MinionSpawnerStorage(SkeBossPlugin plugin) {
        this.plugin = plugin;
        this.file = new File(plugin.getDataFolder(), "spawners.yml");
    }

    public void load() {
        spawners.clear();
        if (!file.exists()) {
            return;
        }
        FileConfiguration config = YamlConfiguration.loadConfiguration(file);
        if (!config.isConfigurationSection("spawners")) {
            return;
        }
        for (String id : config.getConfigurationSection("spawners").getKeys(false)) {
            String path = "spawners." + id + ".";
            String world = config.getString(path + "world");
            if (world == null) {
                continue;
            }
            double x = config.getDouble(path + "x");
            double y = config.getDouble(path + "y");
            double z = config.getDouble(path + "z");
            float yaw = (float) config.getDouble(path + "yaw", 0.0);
            MinionSpawner spawner = new MinionSpawner(id, world, x, y, z, yaw);
            String active = config.getString(path + "active-minion");
            if (active != null) {
                try {
                    spawner.setActiveMinionId(UUID.fromString(active));
                } catch (IllegalArgumentException ignored) {
                }
            }
            spawners.put(id, spawner);
        }
    }

    public void save() {
        FileConfiguration config = new YamlConfiguration();
        for (MinionSpawner spawner : spawners.values()) {
            String path = "spawners." + spawner.getId() + ".";
            config.set(path + "world", spawner.toLocation() != null
                    ? spawner.toLocation().getWorld().getName()
                    : Bukkit.getWorlds().get(0).getName());
            Location loc = spawner.toLocation();
            if (loc != null) {
                config.set(path + "x", loc.getX());
                config.set(path + "y", loc.getY());
                config.set(path + "z", loc.getZ());
                config.set(path + "yaw", loc.getYaw());
            }
            if (spawner.getActiveMinionId() != null) {
                config.set(path + "active-minion", spawner.getActiveMinionId().toString());
            }
        }
        try {
            config.save(file);
        } catch (IOException ex) {
            plugin.getLogger().severe("spawners.yml 저장 실패: " + ex.getMessage());
        }
    }

    public MinionSpawner put(MinionSpawner spawner) {
        spawners.put(spawner.getId(), spawner);
        save();
        return spawner;
    }

    public boolean remove(String id) {
        boolean removed = spawners.remove(id) != null;
        if (removed) {
            save();
        }
        return removed;
    }

    public MinionSpawner get(String id) {
        return spawners.get(id);
    }

    public Collection<MinionSpawner> all() {
        return spawners.values();
    }

    public void clearAllActiveMinions() {
        for (MinionSpawner spawner : spawners.values()) {
            spawner.clearActiveMinion();
        }
    }
}
