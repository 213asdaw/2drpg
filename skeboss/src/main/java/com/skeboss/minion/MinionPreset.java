package com.skeboss.minion;

import org.bukkit.boss.BarColor;
import org.bukkit.boss.BarStyle;

import java.util.List;

public final class MinionPreset {

    private final String id;
    private final String modelId;
    private final List<String> modelFallbackIds;
    private final String displayName;
    private final String skinUsername;
    private final String skinFile;
    private final String skriptTag;
    private final double maxHealth;
    private final double movementSpeed;
    private final double followRange;
    private final MinionAiMode aiMode;
    private final double guardRadius;
    private final double leashRadius;
    private final double homeTolerance;
    private final double meleeRange;
    private final double meleeDamage;
    private final boolean fireImmune;
    private final boolean hideBaseEntity;
    private final double modelScale;
    private final double hitboxScale;
    private final double viewerSyncRadius;
    private final String walkAnimation;
    private final String attackAnimation;
    private final double blendIn;
    private final double blendOut;
    private final String skriptAttackVariable;
    private final String skriptDefenseVariable;
    private final double fallbackAttack;
    private final double fallbackDefense;
    private final boolean bossBarEnabled;
    private final BarColor bossBarColor;
    private final BarStyle bossBarStyle;

    public MinionPreset(
            String id,
            String modelId,
            List<String> modelFallbackIds,
            String displayName,
            String skinUsername,
            String skinFile,
            String skriptTag,
            double maxHealth,
            double movementSpeed,
            double followRange,
            MinionAiMode aiMode,
            double guardRadius,
            double leashRadius,
            double homeTolerance,
            double meleeRange,
            double meleeDamage,
            boolean fireImmune,
            boolean hideBaseEntity,
            double modelScale,
            double hitboxScale,
            double viewerSyncRadius,
            String walkAnimation,
            String attackAnimation,
            double blendIn,
            double blendOut,
            String skriptAttackVariable,
            String skriptDefenseVariable,
            double fallbackAttack,
            double fallbackDefense,
            boolean bossBarEnabled,
            BarColor bossBarColor,
            BarStyle bossBarStyle
    ) {
        this.id = id;
        this.modelId = modelId;
        this.modelFallbackIds = modelFallbackIds;
        this.displayName = displayName;
        this.skinUsername = skinUsername;
        this.skinFile = skinFile;
        this.skriptTag = skriptTag;
        this.maxHealth = maxHealth;
        this.movementSpeed = movementSpeed;
        this.followRange = followRange;
        this.aiMode = aiMode;
        this.guardRadius = guardRadius;
        this.leashRadius = leashRadius;
        this.homeTolerance = homeTolerance;
        this.meleeRange = meleeRange;
        this.meleeDamage = meleeDamage;
        this.fireImmune = fireImmune;
        this.hideBaseEntity = hideBaseEntity;
        this.modelScale = modelScale;
        this.hitboxScale = hitboxScale;
        this.viewerSyncRadius = viewerSyncRadius;
        this.walkAnimation = walkAnimation;
        this.attackAnimation = attackAnimation;
        this.blendIn = blendIn;
        this.blendOut = blendOut;
        this.skriptAttackVariable = skriptAttackVariable;
        this.skriptDefenseVariable = skriptDefenseVariable;
        this.fallbackAttack = fallbackAttack;
        this.fallbackDefense = fallbackDefense;
        this.bossBarEnabled = bossBarEnabled;
        this.bossBarColor = bossBarColor;
        this.bossBarStyle = bossBarStyle;
    }

    public String getId() {
        return id;
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

    public String getSkinFile() {
        return skinFile;
    }

    public boolean hasSkinFile() {
        return skinFile != null && !skinFile.isBlank();
    }

    public boolean hasSkinUsername() {
        return skinUsername != null && !skinUsername.isBlank();
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

    public MinionAiMode getAiMode() {
        return aiMode;
    }

    public boolean isBacklineMode() {
        return aiMode == MinionAiMode.BACKLINE;
    }

    public double getGuardRadius() {
        return guardRadius;
    }

    public double getLeashRadius() {
        return leashRadius;
    }

    public double getHomeTolerance() {
        return homeTolerance;
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

    public String getWalkAnimation() {
        return walkAnimation;
    }

    public String getAttackAnimation() {
        return attackAnimation;
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

    public String skinProfileName() {
        if (hasSkinUsername()) {
            return skinUsername;
        }
        return id;
    }
}
