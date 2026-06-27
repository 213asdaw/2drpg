package com.skeboss.boss;

public record BeamSettings(
        double width,
        int fireDelayTicks,
        int particleTicks,
        double particleStep,
        float particleSize
) {
    public static BeamSettings defaults() {
        return new BeamSettings(1.2, 11, 18, 0.35, 1.5f);
    }
}
