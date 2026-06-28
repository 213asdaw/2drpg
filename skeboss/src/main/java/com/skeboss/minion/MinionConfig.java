package com.skeboss.minion;

import com.skeboss.SkeBossPlugin;
import org.bukkit.boss.BarColor;
import org.bukkit.boss.BarStyle;
import org.bukkit.configuration.file.FileConfiguration;

import java.util.ArrayList;
import java.util.List;

public final class MinionConfig {

    private final String modelId;
    private final List<String> modelFallbackIds;
    private final String displayName;
    private final String skinUsername;
    private final String skriptTag;
    private final double maxHealth;
    private final double movementSpeed;
    private final double followRange;
    private final double meleeRange;
    private final double meleeDamage;
    private final boolean fireImmune;
    private final boolean hideBaseEntity;
    private final double modelScale;
    private final double hitboxScale;
    private final double viewerSyncRadius;
    private final int spawnDelayTicks;
    private final int aiIntervalTicks;
    private final int spawnerRespawnSeconds;
    private final String walkAnimation;
    private final double blendIn;
    private final double blendOut;
    private final String skriptAttackVariable;
    private final String skriptDefenseVariable;
    private final double fallbackAttack;
    private final double fallbackDefense;
    private final boolean bossBarEnabled;
    private final BarColor bossBarColor;
    private final BarStyle bossBarStyle;

    public MinionConfig(SkeBossPlugin plugin) {
        FileConfiguration config = plugin.getConfig();
        modelId = config.getString("minion.model-id", "player_model");
        modelFallbackIds = readModelFallbackIds(config);
        displayName = config.getString("minion.display-name", "&7EMP4348");
        skinUsername = config.getString("minion.skin-username", "EMP4348");
        skriptTag = config.getString("minion.skript-tag", "EMP4348");
        maxHealth = config.getDouble("minion.max-health", 20.0);
        movementSpeed = config.getDouble("minion.movement-speed", 0.32);
        followRange = config.getDouble("minion.follow-range", 24.0);
        meleeRange = config.getDouble("minion.melee-range", 2.5);
        meleeDamage = config.getDouble("minion.melee-damage", 4.0);
        fireImmune = config.getBoolean("minion.fire-immune", true);
        hideBaseEntity = config.getBoolean("minion.hide-base-entity", true);
        modelScale = config.getDouble("minion.model-scale", 1.0);
        hitboxScale = config.getDouble("minion.hitbox-scale", 1.0);
        viewerSyncRadius = config.getDouble("minion.viewer-sync-radius", 48.0);
        spawnDelayTicks = config.getInt("minion.spawn-delay-ticks", 2);
        aiIntervalTicks = config.getInt("minion.ai-interval-ticks", 5);
        spawnerRespawnSeconds = config.getInt("minion.spawner-respawn-seconds", 30);
        walkAnimation = config.getString("minion.walk-animation", "walk");
        blendIn = config.getDouble("minion.blend-in", 0.15);
        blendOut = config.getDouble("minion.blend-out", 0.25);
        skriptAttackVariable = config.getString("minion.rpg.skript-attack-variable", "공격력");
        skriptDefenseVariable = config.getString("minion.rpg.skript-defense-variable", "방어력");
        fallbackAttack = config.getDouble("minion.rpg.fallback-attack", 5.0);
        fallbackDefense = config.getDouble("minion.rpg.fallback-defense", 0.0);
        bossBarEnabled = config.getBoolean("minion.boss-bar.enabled", true);
        bossBarColor = parseBarColor(config.getString("minion.boss-bar.color", "YELLOW"));
        bossBarStyle = parseBarStyle(config.getString("minion.boss-bar.style", "SEGMENTED_6"));
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

    private static List<String> readModelFallbackIds(FileConfiguration config) {
        List<String> ids = new ArrayList<>(config.getStringList("minion.model-fallback-ids"));
        if (ids.isEmpty()) {
            ids.add("player_model");
            ids.add("skin");
            ids.add("player");
        }
        return List.copyOf(ids);
    }

    public String getModelId() {
        return modelId;
    }

    public List<String> getModelFallbackIds() {
        return modelFallbackIds;
    }

    public String getDisplayName() {
        return displayName;
    }

    public String getSkinUsername() {
        return skinUsername;
    }

    public String getSkriptTag() {
        return skriptTag;
    }

    public double getMaxHealth() {
        return maxHealth;
    }

    public double getMovementSpeed() {
        return movementSpeed;
    }

    public double getFollowRange() {
        return followRange;
    }

    public double getMeleeRange() {
        return meleeRange;
    }

    public double getMeleeDamage() {
        return meleeDamage;
    }

    public boolean isFireImmune() {
        return fireImmune;
    }

    public boolean isHideBaseEntity() {
        return hideBaseEntity;
    }

    public double getModelScale() {
        return modelScale;
    }

    public double getHitboxScale() {
        return hitboxScale;
    }

    public double getViewerSyncRadius() {
        return viewerSyncRadius;
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

    public String getWalkAnimation() {
        return walkAnimation;
    }

    public double getBlendIn() {
        return blendIn;
    }

    public double getBlendOut() {
        return blendOut;
    }

    public String getSkriptAttackVariable() {
        return skriptAttackVariable;
    }

    public String getSkriptDefenseVariable() {
        return skriptDefenseVariable;
    }

    public double getFallbackAttack() {
        return fallbackAttack;
    }

    public double getFallbackDefense() {
        return fallbackDefense;
    }

    public boolean isBossBarEnabled() {
        return bossBarEnabled;
    }

    public BarColor getBossBarColor() {
        return bossBarColor;
    }

    public BarStyle getBossBarStyle() {
        return bossBarStyle;
    }
}
