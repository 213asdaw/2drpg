package com.skeboss.weapon;

import com.skeboss.SkeBossPlugin;
import org.bukkit.Material;
import org.bukkit.configuration.ConfigurationSection;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

public final class WeaponConfig {

    private final boolean pvp;
    private final boolean targetBoss;
    private final boolean targetMobs;
    private final boolean pluginRightClick;
    private final Map<String, WeaponDefinition> weapons;

    public WeaponConfig(SkeBossPlugin plugin) {
        ConfigurationSection root = plugin.getConfig().getConfigurationSection("weapons");
        pvp = root != null && root.getBoolean("pvp", false);
        targetBoss = root == null || root.getBoolean("target-boss", true);
        targetMobs = root == null || root.getBoolean("target-mobs", true);
        pluginRightClick = root == null || root.getBoolean("plugin-right-click", true);
        weapons = loadWeapons(root);
    }

    private Map<String, WeaponDefinition> loadWeapons(ConfigurationSection root) {
        if (root == null) {
            return Map.of(
                    "laser-rifle", defaultLaser(),
                    "chain-hook", defaultChain()
            );
        }

        Map<String, WeaponDefinition> loaded = new ConcurrentHashMap<>();
        for (String key : root.getKeys(false)) {
            if (key.equals("pvp") || key.equals("target-boss") || key.equals("target-mobs")
                    || key.equals("plugin-right-click")) {
                continue;
            }
            ConfigurationSection section = root.getConfigurationSection(key);
            if (section == null) {
                continue;
            }
            loaded.put(key, parseWeapon(key, section));
        }

        if (loaded.isEmpty()) {
            loaded.put("artificial-arm", defaultCombined());
            loaded.put("laser-rifle", defaultLaser());
            loaded.put("chain-hook", defaultChain());
        }
        return Collections.unmodifiableMap(loaded);
    }

    private WeaponDefinition parseWeapon(String key, ConfigurationSection section) {
        Material material = Material.matchMaterial(section.getString("material", "BLAZE_ROD"));
        if (material == null) {
            material = Material.BLAZE_ROD;
        }
        List<String> lore = section.getStringList("lore");
        if (lore.isEmpty()) {
            lore = List.of("&7우클릭으로 스킬 시전");
        }
        String sneakSkill = section.getString("sneak-skill");
        if (sneakSkill != null && sneakSkill.isBlank()) {
            sneakSkill = null;
        }
        return new WeaponDefinition(
                key,
                material,
                section.getString("display-name", "&c무기"),
                lore,
                section.getString("skill", key.contains("chain") ? "chain" : "laser"),
                sneakSkill,
                section.getInt("cooldown-seconds", 8)
        );
    }

    private static WeaponDefinition defaultCombined() {
        return new WeaponDefinition(
                "artificial-arm",
                Material.BLAZE_ROD,
                "&c&l인조 무기",
                List.of(
                        "&7인조인간의 전투 장비",
                        "&e우클릭 &7- 레이저",
                        "&e웅크린 채 우클릭 &7- 사슬"
                ),
                "laser",
                "chain",
                8
        );
    }

    private static WeaponDefinition defaultLaser() {
        return new WeaponDefinition(
                "laser-rifle",
                Material.BLAZE_ROD,
                "&c&l인조 레이저",
                List.of("&7인조인간의 레이저 기술", "&7우클릭: 레이저 발사"),
                "laser",
                null,
                8
        );
    }

    private static WeaponDefinition defaultChain() {
        return new WeaponDefinition(
                "chain-hook",
                Material.FISHING_ROD,
                "&7&l인조 사슬",
                List.of("&7인조인간의 사슬 기술", "&7우클릭: 사슬 발사·끌어오기"),
                "chain",
                null,
                10
        );
    }

    public boolean isPvp() {
        return pvp;
    }

    public boolean isTargetBoss() {
        return targetBoss;
    }

    public boolean isPluginRightClick() {
        return pluginRightClick;
    }

    public boolean isTargetMobs() {
        return targetMobs;
    }

    public Map<String, WeaponDefinition> getWeapons() {
        return weapons;
    }

    public WeaponDefinition getWeapon(String id) {
        return weapons.get(id);
    }

    public List<String> getWeaponIds() {
        return new ArrayList<>(weapons.keySet());
    }
}
