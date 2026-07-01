package com.skeboss.skript;

import org.bukkit.entity.Entity;
import org.bukkit.plugin.Plugin;

import java.lang.reflect.Method;
import java.util.Locale;
import java.util.UUID;
import java.util.logging.Level;

/**
 * Skript 전역 변수 읽기 (컴파일 시 Skript JAR 불필요).
 * {@code {공격력::%uuid of entity%}} 형식과 호환.
 */
public final class SkriptBridge {

    private static final char SKRIPT_SEP = '\0';

    private final Plugin plugin;
    private final boolean available;
    private final boolean caseInsensitive;
    private final Method getVariable;

    public SkriptBridge(Plugin plugin) {
        this.plugin = plugin;
        Method resolvedGet = null;
        boolean ci = false;
        boolean ok = false;

        if (plugin.getServer().getPluginManager().isPluginEnabled("Skript")) {
            try {
                Class<?> variablesClass = Class.forName("ch.njol.skript.variables.Variables");
                resolvedGet = variablesClass.getMethod("getVariable", String.class, org.bukkit.event.Event.class, boolean.class);
                try {
                    Object skriptConfig = Class.forName("ch.njol.skript.Skript")
                            .getMethod("getInstance")
                            .invoke(null);
                    Object config = skriptConfig.getClass().getMethod("getConfig").invoke(skriptConfig);
                    ci = (boolean) config.getClass().getField("caseInsensitiveVariables").get(config);
                } catch (ReflectiveOperationException ignored) {
                    ci = false;
                }
                ok = true;
                plugin.getLogger().info("Skript 변수 연동 활성화 (RPG 공격력 → 빔 데미지)");
            } catch (ReflectiveOperationException ex) {
                plugin.getLogger().log(Level.WARNING, "Skript는 있으나 Variables API 연결 실패", ex);
            }
        }

        this.getVariable = resolvedGet;
        this.caseInsensitive = ci;
        this.available = ok;
    }

    public boolean isAvailable() {
        return available;
    }

    /**
     * {@code {name::%uuid of entity%}} 또는 {@code {name::%entity%}} 값을 읽습니다.
     */
    public double getEntityStat(Entity entity, String variableName, double fallback) {
        if (!available || entity == null || variableName == null || variableName.isBlank()) {
            return fallback;
        }

        UUID uuid = entity.getUniqueId();
        String uuidText = uuid.toString();
        String uuidShort = uuidText.replace("-", "");

        for (String key : new String[]{
                variableName + "::" + uuidText,
                variableName + "::" + uuidShort,
                variableName + SKRIPT_SEP + uuidText,
                variableName + SKRIPT_SEP + uuidShort,
                variableName + "::" + entity.getUniqueId(),
        }) {
            Double parsed = parseNumber(readVariable(key));
            if (parsed != null) {
                return parsed;
            }
        }

        if (entity instanceof org.bukkit.entity.Player player) {
            String name = player.getName();
            for (String key : new String[]{
                    variableName + "::" + name,
                    variableName + "::" + name.toLowerCase(Locale.ENGLISH),
            }) {
                Double parsed = parseNumber(readVariable(key));
                if (parsed != null) {
                    return parsed;
                }
            }
        }

        return fallback;
    }

    private Object readVariable(String key) {
        try {
            String resolved = caseInsensitive ? key.toLowerCase(Locale.ENGLISH) : key;
            return getVariable.invoke(null, resolved, null, false);
        } catch (ReflectiveOperationException ex) {
            plugin.getLogger().log(Level.FINE, "Skript 변수 읽기 실패: " + key, ex);
            return null;
        }
    }

    private static Double parseNumber(Object value) {
        if (value == null) {
            return null;
        }
        if (value instanceof Number number) {
            return number.doubleValue();
        }
        try {
            return Double.parseDouble(value.toString());
        } catch (NumberFormatException ex) {
            return null;
        }
    }
}
