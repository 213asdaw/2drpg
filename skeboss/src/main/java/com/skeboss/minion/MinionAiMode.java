package com.skeboss.minion;

public enum MinionAiMode {
    AGGRESSIVE,
    BACKLINE;

    public static MinionAiMode fromConfig(String raw) {
        if (raw == null) {
            return BACKLINE;
        }
        return switch (raw.trim().toLowerCase()) {
            case "aggressive", "front", "선공", "선공몹" -> AGGRESSIVE;
            case "backline", "rear", "guard", "후공", "후공몹" -> BACKLINE;
            default -> BACKLINE;
        };
    }
}
