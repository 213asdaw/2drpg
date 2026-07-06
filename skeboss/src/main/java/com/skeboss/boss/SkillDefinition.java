package com.skeboss.boss;

public record SkillDefinition(
        String id,
        String animation,
        int cooldownSeconds,
        int durationTicks,
        double damage,
        double range,
        double minRange,
        int priority,
        double knockback,
        double aoeRadius,
        BeamSettings beam,
        ChainSettings chain,
        String untitledSkill,
        String bombSkill
) {
    public boolean isBeamSkill() {
        return beam != null;
    }

    public boolean isChainSkill() {
        return chain != null;
    }

    public boolean isUntitledSkill() {
        return untitledSkill != null && !untitledSkill.isBlank();
    }

    public boolean isBombSkill() {
        return bombSkill != null && !bombSkill.isBlank();
    }
}
