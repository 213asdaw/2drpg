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
    private final double blendIn;
    private final double blendOut;
    private final double maxHealth;
    private final String displayName;
    private final double followRange;
    private final double meleeRange;
    private final double meleeDamage;
    private final int aiIntervalTicks;
    private final List<SkillDefinition> skills;

    public BossConfig(SkeBossPlugin plugin) {
        FileConfiguration config = plugin.getConfig();
        modelId = config.getString("model-id", "ske");
        idleAnimation = config.getString("animations.idle", "walk");
        walkAnimation = config.getString("animations.walk", "walk");
        yawOffset = (float) config.getDouble("yaw-offset", 180.0);
        blendIn = config.getDouble("blend-in", 0.15);
        blendOut = config.getDouble("blend-out", 0.25);

        maxHealth = config.getDouble("boss.max-health", 500.0);
        displayName = config.getString("boss.display-name", "&c&l해골 보스");
        followRange = config.getDouble("boss.follow-range", 32.0);
        meleeRange = config.getDouble("boss.melee-range", 3.5);
        meleeDamage = config.getDouble("boss.melee-damage", 8.0);
        aiIntervalTicks = config.getInt("boss.ai-interval-ticks", 5);

        skills = loadSkills(config.getConfigurationSection("skills"));
    }

    private List<SkillDefinition> loadSkills(ConfigurationSection section) {
        if (section == null) {
            return List.of(
                    new SkillDefinition("laser", "attack_laser", 10, 50, 18.0, 12.0, 0.6, 0.0)
            );
        }

        List<SkillDefinition> loaded = new ArrayList<>();
        for (String key : section.getKeys(false)) {
            ConfigurationSection skill = section.getConfigurationSection(key);
            if (skill == null) {
                continue;
            }
            loaded.add(new SkillDefinition(
                    key,
                    skill.getString("animation", key),
                    skill.getInt("cooldown-seconds", 8),
                    skill.getInt("duration-ticks", 40),
                    skill.getDouble("damage", 10.0),
                    skill.getDouble("range", 5.0),
                    skill.getDouble("knockback", 0.5),
                    skill.getDouble("aoe-radius", 0.0)
            ));
        }
        return Collections.unmodifiableList(loaded);
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

    public int getAiIntervalTicks() {
        return aiIntervalTicks;
    }

    public List<SkillDefinition> getSkills() {
        return skills;
    }
}
