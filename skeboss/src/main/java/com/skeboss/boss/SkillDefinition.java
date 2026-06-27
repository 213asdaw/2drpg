package com.skeboss.boss;

public record SkillDefinition(
        String id,
        String animation,
        int cooldownSeconds,
        int durationTicks,
        double damage,
        double range,
        double knockback,
        double aoeRadius
) {
}
