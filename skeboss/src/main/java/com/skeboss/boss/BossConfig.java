package com.skeboss.boss;

import com.skeboss.SkeBossPlugin;
import org.bukkit.configuration.ConfigurationSection;
import org.bukkit.configuration.file.FileConfiguration;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public final class BossConfig {

    private final String modelId;
    private final String idleAnimation;
    private final String walkAnimation;
    private final float yawOffset;
    private final float faceYawOffset;
    private final double blendIn;
    private final double blendOut;
    private final double maxHealth;
    private final String displayName;
    private final double followRange;
    private final double meleeRange;
    private final double meleeDamage;
    private final double movementSpeed;
    private final int aiIntervalTicks;
    private final int spawnDelayTicks;
    private final boolean hideBaseEntity;
    private final double viewerSyncRadius;
    private final double modelScale;
    private final double hitboxScale;
    private final String targetMode;
    private final long aggroDropSeconds;
    private final List<SkillDefinition> skills;

    public BossConfig(SkeBossPlugin plugin) {
        FileConfiguration config = plugin.getConfig();
        modelId = config.getString("model-id", "ske");
        idleAnimation = config.getString("animations.idle", "none");
        walkAnimation = config.getString("animations.walk", "walk");
        yawOffset = (float) config.getDouble("yaw-offset", 180.0);
        faceYawOffset = (float) config.getDouble("face-yaw-offset", 180.0);
        blendIn = config.getDouble("blend-in", 0.15);
        blendOut = config.getDouble("blend-out", 0.25);

        maxHealth = config.getDouble("boss.max-health", 500.0);
        displayName = config.getString("boss.display-name", "&c&l인조인간");
        followRange = config.getDouble("boss.follow-range", 32.0);
        meleeRange = config.getDouble("boss.melee-range", 3.5);
        meleeDamage = config.getDouble("boss.melee-damage", 8.0);
        movementSpeed = config.getDouble("boss.movement-speed", 0.28);
        aiIntervalTicks = config.getInt("boss.ai-interval-ticks", 5);
        spawnDelayTicks = config.getInt("boss.spawn-delay-ticks", 2);
        hideBaseEntity = config.getBoolean("boss.hide-base-entity", true);
        viewerSyncRadius = config.getDouble("boss.viewer-sync-radius", 64.0);
        modelScale = config.getDouble("boss.model-scale", 2.0);
        hitboxScale = config.getDouble("boss.hitbox-scale", 2.0);
        targetMode = config.getString("boss.target-mode", "aggro");
        aggroDropSeconds = config.getLong("boss.aggro-drop-seconds", 30L);

        skills = loadSkills(config.getConfigurationSection("skills"));
    }

    private List<SkillDefinition> loadSkills(ConfigurationSection section) {
        if (section == null) {
            return List.of(defaultLaserSkill());
        }

        List<SkillDefinition> loaded = new ArrayList<>();
        for (String key : section.getKeys(false)) {
            ConfigurationSection skill = section.getConfigurationSection(key);
            if (skill == null) {
                continue;
            }
            loaded.add(parseSkill(key, skill));
        }
        return Collections.unmodifiableList(loaded);
    }

    private SkillDefinition parseSkill(String key, ConfigurationSection skill) {
        BeamSettings beam = null;
        if (skill.getBoolean("beam", false) || skill.isConfigurationSection("beam")) {
            ConfigurationSection beamSec = skill.getConfigurationSection("beam");
            BeamSettings defaults = BeamSettings.defaults();
            if (beamSec != null) {
                beam = new BeamSettings(
                        beamSec.getDouble("width", defaults.width()),
                        beamSec.getInt("fire-delay-ticks", defaults.fireDelayTicks()),
                        beamSec.getInt("particle-ticks", defaults.particleTicks()),
                        beamSec.getDouble("particle-step", defaults.particleStep()),
                        (float) beamSec.getDouble("particle-size", defaults.particleSize())
                );
            } else {
                beam = new BeamSettings(
                        skill.getDouble("beam-width", defaults.width()),
                        skill.getInt("fire-delay-ticks", defaults.fireDelayTicks()),
                        skill.getInt("particle-ticks", defaults.particleTicks()),
                        skill.getDouble("particle-step", defaults.particleStep()),
                        (float) skill.getDouble("particle-size", defaults.particleSize())
                );
            }
        }

        return new SkillDefinition(
                key,
                skill.getString("animation", key),
                skill.getInt("cooldown-seconds", 8),
                skill.getInt("duration-ticks", 40),
                skill.getDouble("damage", 10.0),
                skill.getDouble("range", 5.0),
                skill.getDouble("knockback", 0.5),
                skill.getDouble("aoe-radius", 0.0),
                beam
        );
    }

    private static SkillDefinition defaultLaserSkill() {
        return new SkillDefinition(
                "laser", "attack_laser", 10, 50, 18.0, 20.0, 0.6, 0.0, BeamSettings.defaults()
        );
    }

    public String getModelId() {
        return modelId;
    }

    public String getIdleAnimation() {
        return idleAnimation;
    }

    public String getWalkAnimation() {
        return walkAnimation;
    }

    public float getFaceYawOffset() {
        return faceYawOffset;
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

    public double getFollowRange() {
        return followRange;
    }

    public double getMeleeRange() {
        return meleeRange;
    }

    public double getMeleeDamage() {
        return meleeDamage;
    }

    public double getMovementSpeed() {
        return movementSpeed;
    }

    public int getAiIntervalTicks() {
        return aiIntervalTicks;
    }

    public int getSpawnDelayTicks() {
        return spawnDelayTicks;
    }

    public boolean isHideBaseEntity() {
        return hideBaseEntity;
    }

    public double getViewerSyncRadius() {
        return viewerSyncRadius;
    }

    public double getModelScale() {
        return modelScale;
    }

    public double getHitboxScale() {
        return hitboxScale;
    }

    public String getTargetMode() {
        return targetMode;
    }

    public long getAggroDropMs() {
        return aggroDropSeconds * 1000L;
    }

    public List<SkillDefinition> getSkills() {
        return skills;
    }
}
