package com.skeboss.boss;

import com.skeboss.SkeBossPlugin;
import org.bukkit.configuration.file.FileConfiguration;

public final class BossSettings {

    private final String modelId;
    private final String idleAnimation;
    private final String skillAnimation;
    private final float yawOffset;
    private final double blendIn;
    private final double blendOut;
    private final double maxHealth;
    private final String displayName;

    public BossSettings(SkeBossPlugin plugin) {
        FileConfiguration config = plugin.getConfig();
        modelId = config.getString("model-id", "ske_boss");
        idleAnimation = config.getString("animations.idle", "idle");
        skillAnimation = config.getString("animations.skill", "skill_attack");
        yawOffset = (float) config.getDouble("yaw-offset", 180.0);
        blendIn = config.getDouble("blend-in", 0.15);
        blendOut = config.getDouble("blend-out", 0.25);
        maxHealth = config.getDouble("boss.max-health", 500.0);
        displayName = config.getString("boss.display-name", "&c&l해골 보스");
    }

    public String getModelId() {
        return modelId;
    }

    public String getIdleAnimation() {
        return idleAnimation;
    }

    public String getSkillAnimation() {
        return skillAnimation;
    }

    public float getYawOffset() {
        return yawOffset;
    }

    public double getBlendIn() {
        return blendIn;
    }

    public double getBlendOut() {
        return blendOut;
    }

    public double getMaxHealth() {
        return maxHealth;
    }

    public String getDisplayName() {
        return displayName;
    }
}
