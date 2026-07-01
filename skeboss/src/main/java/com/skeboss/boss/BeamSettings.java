package com.skeboss.boss;

public record BeamSettings(
        double width,
        int fireDelayTicks,
        int particleTicks,
        int damageIntervalTicks,
        double particleStep,
        float particleSize
) {
    public static BeamSettings defaults() {
        return new BeamSettings(1.8, 11, 25, 4, 0.25, 2.0f);
    }
}
