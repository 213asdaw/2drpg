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
        ChainSettings chain
) {
    public boolean isBeamSkill() {
        return beam != null;
    }

    public boolean isChainSkill() {
        return chain != null;
    }
}
