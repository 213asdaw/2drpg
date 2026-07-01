package com.skeboss.boss;

public record ChainSettings(
        int fireDelayTicks,
        double chainSpeed,
        double hitRadius,
        double pullSpeed,
        int pullTicks,
        double particleStep
) {
    public static ChainSettings defaults() {
        return new ChainSettings(8, 1.1, 1.4, 0.5, 14, 0.35);
    }
}
