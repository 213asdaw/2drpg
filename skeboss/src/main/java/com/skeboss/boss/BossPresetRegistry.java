package com.skeboss.boss;

import com.skeboss.SkeBossPlugin;
import org.bukkit.Material;
import org.bukkit.configuration.ConfigurationSection;
import org.bukkit.configuration.file.FileConfiguration;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public final class BossPresetRegistry {

    private final String defaultPresetId;
    private final List<String> presetIds;

    public BossPresetRegistry(SkeBossPlugin plugin) {
        FileConfiguration config = plugin.getConfig();
        defaultPresetId = config.getString("default-boss-preset", "default");
        List<String> ids = new ArrayList<>();
        ids.add("default");
        ConfigurationSection presets = config.getConfigurationSection("boss-presets");
        if (presets != null) {
            for (String key : presets.getKeys(false)) {
                if (!ids.contains(key)) {
                    ids.add(key);
                }
            }
        }
        presetIds = List.copyOf(ids);
    }

    public String getDefaultPresetId() {
        return defaultPresetId;
    }

    public List<String> getPresetIds() {
        return presetIds;
    }

    public BossConfig load(SkeBossPlugin plugin, String presetId) {
        String id = presetId == null || presetId.isBlank() ? defaultPresetId : presetId;
        if (!presetIds.contains(id) && plugin.getConfig().getConfigurationSection("boss-presets." + id) == null) {
            throw new IllegalArgumentException("알 수 없는 보스 프리셋: " + id);
        }
        return new BossConfig(plugin, id);
    }

    public BossConfig loadDefault(SkeBossPlugin plugin) {
        return load(plugin, defaultPresetId);
    }
}
