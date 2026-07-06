package com.skeboss.minion;

import com.skeboss.SkeBossPlugin;
import org.bukkit.boss.BarColor;
import org.bukkit.boss.BarStyle;
import org.bukkit.configuration.ConfigurationSection;
import org.bukkit.configuration.file.FileConfiguration;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public final class MinionConfig {

    private final String defaultPresetId;
    private final Map<String, MinionPreset> presets;
    private final int spawnDelayTicks;
    private final int aiIntervalTicks;
    private final int spawnerRespawnSeconds;

    public MinionConfig(SkeBossPlugin plugin) {
        FileConfiguration config = plugin.getConfig();
        String sharedModelId = config.getString("minion.model-id", "player_model");
        List<String> sharedFallbackIds = readModelFallbackIds(config.getStringList("minion.model-fallback-ids"));

        ConfigurationSection presetsSection = config.getConfigurationSection("minion.presets");
        if (presetsSection != null && !presetsSection.getKeys(false).isEmpty()) {
            defaultPresetId = config.getString("minion.default-preset",
                    presetsSection.getKeys(false).iterator().next());
            Map<String, MinionPreset> loaded = new LinkedHashMap<>();
            for (String id : presetsSection.getKeys(false)) {
                loaded.put(id, loadPreset(id, presetsSection.getConfigurationSection(id), sharedModelId, sharedFallbackIds));
            }
            presets = Collections.unmodifiableMap(loaded);
        } else {
            defaultPresetId = "default";
            presets = Map.of(defaultPresetId, loadLegacyPreset(config, sharedModelId, sharedFallbackIds));
        }

        spawnDelayTicks = config.getInt("minion.spawn-delay-ticks", 0);
        aiIntervalTicks = config.getInt("minion.ai-interval-ticks", 5);
        spawnerRespawnSeconds = config.getInt("minion.spawner-respawn-seconds", 30);
    }

    private static MinionPreset loadLegacyPreset(FileConfiguration config, String sharedModelId, List<String> sharedFallbackIds) {
        return new MinionPreset(
                "default",
                sharedModelId,
                sharedFallbackIds,
                config.getString("minion.display-name", "&e조연우"),
                config.getString("minion.skin-username", "EMP4348"),
                config.getString("minion.skin-file", ""),
                config.getString("minion.skript-tag", "EMP4348"),
                config.getDouble("minion.max-health", 20.0),
                config.getDouble("minion.movement-speed", 0.32),
                config.getDouble("minion.follow-range", 24.0),
                MinionAiMode.fromConfig(config.getString("minion.ai-mode", "backline")),
                config.getDouble("minion.guard-radius", 10.0),
                config.getDouble("minion.leash-radius", 14.0),
                config.getDouble("minion.home-tolerance", 1.5),
                config.getDouble("minion.melee-range", 2.5),
                config.getDouble("minion.melee-damage", 4.0),
                config.getBoolean("minion.fire-immune", true),
                config.getBoolean("minion.hide-base-entity", true),
                config.getDouble("minion.model-scale", 1.0),
                config.getDouble("minion.hitbox-scale", 1.0),
                config.getDouble("minion.viewer-sync-radius", 48.0),
                config.getString("minion.walk-animation", "walk"),
                config.getString("minion.attack-animation", "attack"),
                config.getDouble("minion.blend-in", 0.15),
                config.getDouble("minion.blend-out", 0.25),
                config.getString("minion.rpg.skript-attack-variable", "공격력"),
                config.getString("minion.rpg.skript-defense-variable", "방어력"),
                config.getDouble("minion.rpg.fallback-attack", 5.0),
                config.getDouble("minion.rpg.fallback-defense", 0.0),
                config.getBoolean("minion.boss-bar.enabled", true),
                parseBarColor(config.getString("minion.boss-bar.color", "YELLOW")),
                parseBarStyle(config.getString("minion.boss-bar.style", "SEGMENTED_6"))
        );
    }

    private static MinionPreset loadPreset(String id, ConfigurationSection section, String sharedModelId, List<String> sharedFallbackIds) {
        if (section == null) {
            throw new IllegalStateException("잡몹 프리셋 없음: " + id);
        }
        String modelId = section.getString("model-id", sharedModelId);
        List<String> fallbackIds = section.contains("model-fallback-ids")
                ? readModelFallbackIds(section.getStringList("model-fallback-ids"))
                : sharedFallbackIds;
        return new MinionPreset(
                id,
                modelId,
                fallbackIds,
                section.getString("display-name", "&e" + id),
                section.getString("skin-username", ""),
                section.getString("skin-file", ""),
                section.getString("skript-tag", id),
                section.getDouble("max-health", 20.0),
                section.getDouble("movement-speed", 0.32),
                section.getDouble("follow-range", 24.0),
                MinionAiMode.fromConfig(section.getString("ai-mode", "backline")),
                section.getDouble("guard-radius", 10.0),
                section.getDouble("leash-radius", 14.0),
                section.getDouble("home-tolerance", 1.5),
                section.getDouble("melee-range", 2.5),
                section.getDouble("melee-damage", 4.0),
                section.getBoolean("fire-immune", true),
                section.getBoolean("hide-base-entity", true),
                section.getDouble("model-scale", 1.0),
                section.getDouble("hitbox-scale", 1.0),
                section.getDouble("viewer-sync-radius", 48.0),
                section.getString("walk-animation", "walk"),
                section.getString("attack-animation", "attack"),
                section.getDouble("blend-in", 0.15),
                section.getDouble("blend-out", 0.25),
                section.getString("rpg.skript-attack-variable", "공격력"),
                section.getString("rpg.skript-defense-variable", "방어력"),
                section.getDouble("rpg.fallback-attack", 5.0),
                section.getDouble("rpg.fallback-defense", 0.0),
                section.getBoolean("boss-bar.enabled", true),
                parseBarColor(section.getString("boss-bar.color", "YELLOW")),
                parseBarStyle(section.getString("boss-bar.style", "SEGMENTED_6"))
        );
    }

    private static BarColor parseBarColor(String raw) {
        try {
            return BarColor.valueOf(raw.toUpperCase());
        } catch (IllegalArgumentException ex) {
            return BarColor.YELLOW;
        }
    }

    private static BarStyle parseBarStyle(String raw) {
        try {
            return BarStyle.valueOf(raw.toUpperCase());
        } catch (IllegalArgumentException ex) {
            return BarStyle.SEGMENTED_6;
        }
    }

    private static List<String> readModelFallbackIds(List<String> ids) {
        List<String> copy = new ArrayList<>(ids);
        if (copy.isEmpty()) {
            copy.add("player_model");
            copy.add("skin_2");
            copy.add("skin");
            copy.add("player");
        }
        return List.copyOf(copy);
    }

    public String getDefaultPresetId() {
        return defaultPresetId;
    }

    public MinionPreset getDefaultPreset() {
        return getPreset(defaultPresetId);
    }

    public MinionPreset getPreset(String id) {
        if (id == null || id.isBlank()) {
            return getDefaultPreset();
        }
        MinionPreset preset = presets.get(id);
        if (preset != null) {
            return preset;
        }
        return getDefaultPreset();
    }

    public Map<String, MinionPreset> getPresets() {
        return presets;
    }

    public boolean hasPreset(String id) {
        return id != null && presets.containsKey(id);
    }

    public int getSpawnDelayTicks() {
        return spawnDelayTicks;
    }

    public int getAiIntervalTicks() {
        return aiIntervalTicks;
    }

    public int getSpawnerRespawnSeconds() {
        return spawnerRespawnSeconds;
    }

    // 하위 호환 — 기본 프리셋 값
    public String getModelId() {
        return getDefaultPreset().getModelId();
    }

    public List<String> getModelFallbackIds() {
        return getDefaultPreset().getModelFallbackIds();
    }

    public String getDisplayName() {
        return getDefaultPreset().getDisplayName();
    }

    public String getSkinUsername() {
        return getDefaultPreset().getSkinUsername();
    }

    public String getSkriptTag() {
        return getDefaultPreset().getSkriptTag();
    }

    public double getMaxHealth() {
        return getDefaultPreset().getMaxHealth();
    }

    public double getMovementSpeed() {
        return getDefaultPreset().getMovementSpeed();
    }

    public double getFollowRange() {
        return getDefaultPreset().getFollowRange();
    }

    public boolean isBacklineMode() {
        return getDefaultPreset().isBacklineMode();
    }

    public double getMeleeRange() {
        return getDefaultPreset().getMeleeRange();
    }

    public double getMeleeDamage() {
        return getDefaultPreset().getMeleeDamage();
    }

    public boolean isFireImmune() {
        return getDefaultPreset().isFireImmune();
    }

    public boolean isHideBaseEntity() {
        return getDefaultPreset().isHideBaseEntity();
    }

    public double getModelScale() {
        return getDefaultPreset().getModelScale();
    }

    public double getHitboxScale() {
        return getDefaultPreset().getHitboxScale();
    }

    public double getViewerSyncRadius() {
        return getDefaultPreset().getViewerSyncRadius();
    }

    public String getWalkAnimation() {
        return getDefaultPreset().getWalkAnimation();
    }

    public String getAttackAnimation() {
        return getDefaultPreset().getAttackAnimation();
    }

    public double getBlendIn() {
        return getDefaultPreset().getBlendIn();
    }

    public double getBlendOut() {
        return getDefaultPreset().getBlendOut();
    }

    public boolean isBossBarEnabled() {
        return getDefaultPreset().isBossBarEnabled();
    }

    public BarColor getBossBarColor() {
        return getDefaultPreset().getBossBarColor();
    }

    public BarStyle getBossBarStyle() {
        return getDefaultPreset().getBossBarStyle();
    }

    public double getHomeTolerance() {
        return getDefaultPreset().getHomeTolerance();
    }
}
