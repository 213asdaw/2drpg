package com.skeboss.cutscene;

import java.util.Map;

public sealed interface CutsceneStep permits
        CutsceneStep.Freeze,
        CutsceneStep.Restore,
        CutsceneStep.Wait,
        CutsceneStep.Title,
        CutsceneStep.Message,
        CutsceneStep.Sfx,
        CutsceneStep.Camera,
        CutsceneStep.Spawn,
        CutsceneStep.Move,
        CutsceneStep.Animate,
        CutsceneStep.StopAnimation,
        CutsceneStep.Despawn,
        CutsceneStep.Cleanup {

    record Freeze(boolean enabled) implements CutsceneStep {
    }

    record Restore() implements CutsceneStep {
    }

    record Wait(int ticks) implements CutsceneStep {
    }

    record Title(String main, String sub, int fadeIn, int stay, int fadeOut) implements CutsceneStep {
    }

    record Message(String text) implements CutsceneStep {
    }

    record Sfx(org.bukkit.Sound sound, float volume, float pitch) implements CutsceneStep {
    }

    record Camera(double[] offset, String lookAtActor, Float yaw, Float pitch, boolean hold) implements CutsceneStep {
    }

    record Spawn(
            String actorId,
            String model,
            String preset,
            String skin,
            double[] offset,
            float yaw
    ) implements CutsceneStep {
    }

    record Move(String actorId, double[] toOffset, int durationTicks, String animation) implements CutsceneStep {
    }

    record Animate(String actorId, String animation, int durationTicks, boolean loop) implements CutsceneStep {
    }

    record StopAnimation(String actorId, String animation) implements CutsceneStep {
    }

    record Despawn(String actorId) implements CutsceneStep {
    }

    record Cleanup() implements CutsceneStep {
    }

    static CutsceneStep parse(Map<?, ?> raw) {
        if (raw == null || raw.isEmpty()) {
            throw new IllegalArgumentException("빈 스텝");
        }
        if (raw.size() == 1) {
            Map.Entry<?, ?> entry = raw.entrySet().iterator().next();
            String key = String.valueOf(entry.getKey()).toLowerCase();
            Object value = entry.getValue();
            return parseKeyed(key, value);
        }
        return parseSection(raw);
    }

    private static CutsceneStep parseKeyed(String key, Object value) {
        return switch (key) {
            case "freeze" -> new Freeze(Boolean.TRUE.equals(value) || "true".equalsIgnoreCase(String.valueOf(value)));
            case "restore" -> new Restore();
            case "cleanup" -> new Cleanup();
            case "wait" -> new Wait(asInt(value, 20));
            case "message" -> new Message(String.valueOf(value));
            case "title" -> parseTitle(value);
            case "sound" -> parseSound(value);
            case "camera" -> parseCamera(value);
            case "spawn" -> parseSpawn(asMap(value));
            case "move" -> parseMove(asMap(value));
            case "animate" -> parseAnimate(asMap(value));
            case "stop-animation", "stop_animation" -> parseStopAnimation(asMap(value));
            case "despawn" -> new Despawn(String.valueOf(value));
            default -> throw new IllegalArgumentException("알 수 없는 스텝: " + key);
        };
    }

    private static CutsceneStep parseSection(Map<?, ?> map) {
        if (map.containsKey("freeze")) {
            return new Freeze(asBool(map.get("freeze"), true));
        }
        if (map.containsKey("restore")) {
            return new Restore();
        }
        if (map.containsKey("cleanup")) {
            return new Cleanup();
        }
        if (map.containsKey("wait")) {
            return new Wait(asInt(map.get("wait"), 20));
        }
        if (map.containsKey("message")) {
            return new Message(String.valueOf(map.get("message")));
        }
        if (map.containsKey("title")) {
            return parseTitle(map.get("title"));
        }
        if (map.containsKey("sound")) {
            return parseSound(map.get("sound"));
        }
        if (map.containsKey("camera")) {
            return parseCamera(map.get("camera"));
        }
        if (map.containsKey("spawn")) {
            return parseSpawn(asMap(map.get("spawn")));
        }
        if (map.containsKey("move")) {
            return parseMove(asMap(map.get("move")));
        }
        if (map.containsKey("animate")) {
            return parseAnimate(asMap(map.get("animate")));
        }
        if (map.containsKey("stop-animation") || map.containsKey("stop_animation")) {
            Object stop = map.containsKey("stop-animation") ? map.get("stop-animation") : map.get("stop_animation");
            return parseStopAnimation(asMap(stop));
        }
        if (map.containsKey("despawn")) {
            return new Despawn(String.valueOf(map.get("despawn")));
        }
        throw new IllegalArgumentException("스텝 키를 찾을 수 없습니다: " + map.keySet());
    }

    private static Title parseTitle(Object value) {
        if (value instanceof String text) {
            return new Title(text, "", 10, 60, 20);
        }
        Map<?, ?> map = asMap(value);
        return new Title(
                string(map, "main", ""),
                string(map, "sub", string(map, "subtitle", "")),
                asInt(map.get("fade-in"), asInt(map.get("fade_in"), 10)),
                asInt(map.get("stay"), 60),
                asInt(map.get("fade-out"), asInt(map.get("fade_out"), 20))
        );
    }

    private static Sfx parseSound(Object value) {
        if (value instanceof String soundName) {
            return new Sfx(parseSoundEnum(soundName), 1.0f, 1.0f);
        }
        Map<?, ?> map = asMap(value);
        return new Sfx(
                parseSoundEnum(string(map, "sound", string(map, "name", "ENTITY_EXPERIENCE_ORB_PICKUP"))),
                (float) asDouble(map.get("volume"), 1.0),
                (float) asDouble(map.get("pitch"), 1.0)
        );
    }

    private static org.bukkit.Sound parseSoundEnum(String name) {
        try {
            return org.bukkit.Sound.valueOf(name.trim().toUpperCase());
        } catch (IllegalArgumentException ex) {
            throw new IllegalArgumentException("알 수 없는 사운드: " + name);
        }
    }

    private static Camera parseCamera(Object value) {
        Map<?, ?> map = asMap(value);
        double[] offset = readOffset(map, "offset");
        String lookAt = string(map, "look-at", string(map, "look_at", null));
        Float yaw = map.containsKey("yaw") ? (float) asDouble(map.get("yaw"), 0) : null;
        Float pitch = map.containsKey("pitch") ? (float) asDouble(map.get("pitch"), 0) : null;
        boolean hold = asBool(map.get("hold"), true);
        return new Camera(offset, lookAt, yaw, pitch, hold);
    }

    private static Spawn parseSpawn(Map<?, ?> map) {
        return new Spawn(
                string(map, "id", string(map, "actor", "actor")),
                string(map, "model", "player_model"),
                string(map, "preset", null),
                string(map, "skin", null),
                readOffset(map, "offset"),
                (float) asDouble(map.get("yaw"), 0)
        );
    }

    private static Move parseMove(Map<?, ?> map) {
        return new Move(
                string(map, "actor", string(map, "id", "")),
                readOffset(map, "to-offset", "to_offset", "offset"),
                asInt(map.get("duration"), asInt(map.get("duration-ticks"), 20)),
                string(map, "animation", "walk")
        );
    }

    private static Animate parseAnimate(Map<?, ?> map) {
        return new Animate(
                string(map, "actor", string(map, "id", "")),
                string(map, "animation", "attack"),
                asInt(map.get("duration"), asInt(map.get("duration-ticks"), 20)),
                asBool(map.get("loop"), false)
        );
    }

    private static StopAnimation parseStopAnimation(Map<?, ?> map) {
        return new StopAnimation(
                string(map, "actor", string(map, "id", "")),
                string(map, "animation", null)
        );
    }

    private static double[] readOffset(Map<?, ?> map, String... keys) {
        for (String key : keys) {
            if (!map.containsKey(key)) {
                continue;
            }
            Object value = map.get(key);
            if (value instanceof Iterable<?> iterable) {
                double[] out = new double[3];
                int i = 0;
                for (Object part : iterable) {
                    if (i >= 3) {
                        break;
                    }
                    out[i++] = asDouble(part, 0);
                }
                while (i < 3) {
                    out[i++] = 0;
                }
                return out;
            }
            if (value instanceof String text) {
                String[] parts = text.split(",");
                double[] out = new double[3];
                for (int i = 0; i < 3; i++) {
                    out[i] = i < parts.length ? asDouble(parts[i].trim(), 0) : 0;
                }
                return out;
            }
        }
        return new double[]{0, 0, 0};
    }

    private static Map<?, ?> asMap(Object value) {
        if (value instanceof Map<?, ?> map) {
            return map;
        }
        throw new IllegalArgumentException("맵이 필요합니다: " + value);
    }

    private static String string(Map<?, ?> map, String key, String fallback) {
        if (!map.containsKey(key) || map.get(key) == null) {
            return fallback;
        }
        return String.valueOf(map.get(key));
    }

    private static int asInt(Object value, int fallback) {
        if (value instanceof Number number) {
            return number.intValue();
        }
        if (value == null) {
            return fallback;
        }
        try {
            return Integer.parseInt(String.valueOf(value));
        } catch (NumberFormatException ex) {
            return fallback;
        }
    }

    private static double asDouble(Object value, double fallback) {
        if (value instanceof Number number) {
            return number.doubleValue();
        }
        if (value == null) {
            return fallback;
        }
        try {
            return Double.parseDouble(String.valueOf(value));
        } catch (NumberFormatException ex) {
            return fallback;
        }
    }

    private static boolean asBool(Object value, boolean fallback) {
        if (value instanceof Boolean bool) {
            return bool;
        }
        if (value == null) {
            return fallback;
        }
        return Boolean.parseBoolean(String.valueOf(value));
    }
}
