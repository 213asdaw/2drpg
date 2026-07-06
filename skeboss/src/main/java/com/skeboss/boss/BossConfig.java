package com.skeboss.boss;

import com.skeboss.SkeBossPlugin;
import org.bukkit.Material;
import org.bukkit.boss.BarColor;
import org.bukkit.boss.BarStyle;
import org.bukkit.configuration.ConfigurationSection;
import org.bukkit.configuration.file.FileConfiguration;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public final class BossConfig {

    private final String presetId;
    private final String modelId;
    private final List<String> modelFallbackIds;
    private final String skinUsername;
    private final String idleAnimation;
    private final String walkAnimation;
    private final float yawOffset;
    private final float faceYawOffset;
    private final float skillFaceYawOffset;
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
    private final String skillTargetMode;
    private final long aggroDropSeconds;
    private final String skriptAttackVariable;
    private final double beamAttackMultiplier;
    private final double fallbackAttackStat;
    private final float skillMaxPitch;
    private final boolean fireImmune;
    private final boolean bossBarEnabled;
    private final BarColor bossBarColor;
    private final BarStyle bossBarStyle;
    private final Material handMaterial;
    private final String handDisplayName;
    private final List<SkillDefinition> skills;

    public BossConfig(SkeBossPlugin plugin) {
        this(plugin, "default");
    }

    public BossConfig(SkeBossPlugin plugin, String presetId) {
        FileConfiguration root = plugin.getConfig();
        this.presetId = presetId;
        ConfigurationSection preset = "default".equals(presetId)
                ? root.getConfigurationSection("boss-presets.default")
                : root.getConfigurationSection("boss-presets." + presetId);

        modelId = str(preset, root, "model-id", "ske");
        modelFallbackIds = readModelFallbackIds(preset, root);
        String skin = preset != null && preset.contains("skin-username")
                ? preset.getString("skin-username")
                : (preset != null && preset.contains("skin.username")
                ? preset.getString("skin.username")
                : root.getString("skin-username"));
        skinUsername = skin != null && !skin.isBlank() ? skin : null;
        idleAnimation = str(preset, root, "animations.idle", "none");
        walkAnimation = str(preset, root, "animations.walk", "walk");
        yawOffset = (float) num(preset, root, "yaw-offset", 180.0);
        faceYawOffset = (float) num(preset, root, "face-yaw-offset", 180.0);
        skillFaceYawOffset = (float) num(preset, root, "skill-face-yaw-offset", 0.0);
        blendIn = num(preset, root, "blend-in", 0.15);
        blendOut = num(preset, root, "blend-out", 0.25);

        maxHealth = num(preset, root, "boss.max-health", 500.0);
        displayName = str(preset, root, "boss.display-name", "&c&l보스");
        followRange = num(preset, root, "boss.follow-range", 32.0);
        meleeRange = num(preset, root, "boss.melee-range", 3.5);
        meleeDamage = num(preset, root, "boss.melee-damage", 8.0);
        movementSpeed = num(preset, root, "boss.movement-speed", 0.38);
        aiIntervalTicks = (int) num(preset, root, "boss.ai-interval-ticks", 5);
        spawnDelayTicks = (int) num(preset, root, "boss.spawn-delay-ticks", 2);
        hideBaseEntity = bool(preset, root, "boss.hide-base-entity", true);
        viewerSyncRadius = num(preset, root, "boss.viewer-sync-radius", 64.0);
        modelScale = num(preset, root, "boss.model-scale", 2.0);
        hitboxScale = num(preset, root, "boss.hitbox-scale", 2.0);
        targetMode = str(preset, root, "boss.target-mode", "aggro");
        skillTargetMode = str(preset, root, "boss.skill-target-mode", "aggro");
        aggroDropSeconds = (long) num(preset, root, "boss.aggro-drop-seconds", 30L);
        skriptAttackVariable = str(preset, root, "boss.rpg.skript-attack-variable", "공격력");
        beamAttackMultiplier = num(preset, root, "boss.rpg.beam-attack-multiplier", 2.0);
        fallbackAttackStat = num(preset, root, "boss.rpg.fallback-attack-stat", 80.0);
        skillMaxPitch = (float) num(preset, root, "boss.skill-max-pitch", 75.0);
        fireImmune = bool(preset, root, "boss.fire-immune", true);
        bossBarEnabled = bool(preset, root, "boss.boss-bar.enabled", true);
        bossBarColor = parseBarColor(str(preset, root, "boss.boss-bar.color", "RED"));
        bossBarStyle = parseBarStyle(str(preset, root, "boss.boss-bar.style", "SEGMENTED_10"));

        ConfigurationSection hand = section(preset, root, "hand-item");
        if (hand != null) {
            Material material = Material.matchMaterial(hand.getString("material", "DIAMOND_SWORD"));
            handMaterial = material != null ? material : Material.DIAMOND_SWORD;
            handDisplayName = hand.getString("display-name");
        } else {
            handMaterial = null;
            handDisplayName = null;
        }

        ConfigurationSection skillsSection = preset != null && preset.isConfigurationSection("skills")
                ? preset.getConfigurationSection("skills")
                : root.getConfigurationSection("skills");
        skills = loadSkills(skillsSection);
    }

    private static ConfigurationSection section(ConfigurationSection preset, FileConfiguration root, String path) {
        if (preset != null && preset.isConfigurationSection(path)) {
            return preset.getConfigurationSection(path);
        }
        return root.getConfigurationSection(path);
    }

    private static String str(ConfigurationSection preset, FileConfiguration root, String path, String def) {
        if (preset != null && preset.contains(path)) {
            return preset.getString(path, def);
        }
        return root.getString(path, def);
    }

    private static double num(ConfigurationSection preset, FileConfiguration root, String path, double def) {
        if (preset != null && preset.contains(path)) {
            return preset.getDouble(path, def);
        }
        return root.getDouble(path, def);
    }

    private static boolean bool(ConfigurationSection preset, FileConfiguration root, String path, boolean def) {
        if (preset != null && preset.contains(path)) {
            return preset.getBoolean(path, def);
        }
        return root.getBoolean(path, def);
    }

    private static BarColor parseBarColor(String raw) {
        try {
            return BarColor.valueOf(raw.toUpperCase());
        } catch (IllegalArgumentException ex) {
            return BarColor.RED;
        }
    }

    private static BarStyle parseBarStyle(String raw) {
        try {
            return BarStyle.valueOf(raw.toUpperCase());
        } catch (IllegalArgumentException ex) {
            return BarStyle.SEGMENTED_10;
        }
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
                        beamSec.getInt("damage-interval-ticks", defaults.damageIntervalTicks()),
                        beamSec.getDouble("particle-step", defaults.particleStep()),
                        (float) beamSec.getDouble("particle-size", defaults.particleSize())
                );
            } else {
                beam = new BeamSettings(
                        skill.getDouble("beam-width", defaults.width()),
                        skill.getInt("fire-delay-ticks", defaults.fireDelayTicks()),
                        skill.getInt("particle-ticks", defaults.particleTicks()),
                        skill.getInt("damage-interval-ticks", defaults.damageIntervalTicks()),
                        skill.getDouble("particle-step", defaults.particleStep()),
                        (float) skill.getDouble("particle-size", defaults.particleSize())
                );
            }
        }

        ChainSettings chain = null;
        if (skill.getBoolean("chain", false) || skill.isConfigurationSection("chain")) {
            ConfigurationSection chainSec = skill.getConfigurationSection("chain");
            ChainSettings defaults = ChainSettings.defaults();
            if (chainSec != null) {
                chain = new ChainSettings(
                        chainSec.getInt("fire-delay-ticks", defaults.fireDelayTicks()),
                        chainSec.getDouble("chain-speed", defaults.chainSpeed()),
                        chainSec.getDouble("hit-radius", defaults.hitRadius()),
                        chainSec.getDouble("pull-speed", defaults.pullSpeed()),
                        chainSec.getInt("pull-ticks", defaults.pullTicks()),
                        chainSec.getDouble("particle-step", defaults.particleStep())
                );
            } else {
                chain = new ChainSettings(
                        skill.getInt("fire-delay-ticks", defaults.fireDelayTicks()),
                        skill.getDouble("chain-speed", defaults.chainSpeed()),
                        skill.getDouble("hit-radius", defaults.hitRadius()),
                        skill.getDouble("pull-speed", defaults.pullSpeed()),
                        skill.getInt("pull-ticks", defaults.pullTicks()),
                        skill.getDouble("particle-step", defaults.particleStep())
                );
            }
        }

        String untitled = skill.getString("untitled");
        if (untitled != null && untitled.isBlank()) {
            untitled = null;
        }

        String bomb = skill.getString("bomb");
        if (bomb != null && bomb.isBlank()) {
            bomb = null;
        }

        return new SkillDefinition(
                key,
                skill.getString("animation", key),
                skill.getInt("cooldown-seconds", 8),
                skill.getInt("duration-ticks", 40),
                skill.getDouble("damage", 10.0),
                skill.getDouble("range", 5.0),
                skill.getDouble("min-range", 0.0),
                skill.getInt("priority", 0),
                skill.getDouble("knockback", 0.5),
                skill.getDouble("aoe-radius", 0.0),
                beam,
                chain,
                untitled,
                bomb
        );
    }

    private static SkillDefinition defaultLaserSkill() {
        return new SkillDefinition(
                "laser", "attack_laser", 10, 50, 18.0, 20.0, 6.0, 10, 0.6, 0.0,
                BeamSettings.defaults(), null, null, null
        );
    }

    private static List<String> readModelFallbackIds(ConfigurationSection preset, FileConfiguration root) {
        List<String> ids = new ArrayList<>();
        if (preset != null && preset.isList("model-fallback-ids")) {
            ids.addAll(preset.getStringList("model-fallback-ids"));
        } else if (root.isList("model-fallback-ids")) {
            ids.addAll(root.getStringList("model-fallback-ids"));
        }
        if (ids.isEmpty()) {
            ids.add("player_model");
            ids.add("skin_2");
            ids.add("skin");
            ids.add("player");
        }
        return List.copyOf(ids);
    }

    public boolean usesPlayerSkin() {
        return skinUsername != null;
    }

    public String getSkinUsername() {
        return skinUsername;
    }

    public List<String> getModelFallbackIds() {
        return modelFallbackIds;
    }

    public String getPresetId() {
        return presetId;
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

    public float getSkillFaceYawOffset() {
        return skillFaceYawOffset;
    }

    public String getSkillTargetMode() {
        return skillTargetMode;
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

    public String getSkriptAttackVariable() {
        return skriptAttackVariable;
    }

    public double getBeamAttackMultiplier() {
        return beamAttackMultiplier;
    }

    public double getFallbackAttackStat() {
        return fallbackAttackStat;
    }

    public float getSkillMaxPitch() {
        return skillMaxPitch;
    }

    public boolean isFireImmune() {
        return fireImmune;
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

    public Material getHandMaterial() {
        return handMaterial;
    }

    public String getHandDisplayName() {
        return handDisplayName;
    }

    public boolean hasHandItem() {
        return handMaterial != null;
    }

    public List<SkillDefinition> getSkills() {
        return skills;
    }
}
