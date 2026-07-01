package com.skeboss.coin;

import com.skeboss.SkeBossPlugin;
import com.skeboss.util.TextUtil;
import org.bukkit.Material;
import org.bukkit.configuration.ConfigurationSection;

import java.util.List;

public final class CoinItemConfig {

    private final boolean enabled;
    private final Material material;
    private final String customModelString;
    private final int customModelData;
    private final boolean requireModelData;
    private final String displayName;
    private final List<String> nameKeywords;
    private final Material projectileMaterial;
    private final String projectileModelString;
    private final int projectileModelData;
    private final double throwSpeed;
    private final boolean consume;
    private final int cooldownSeconds;
    private final String throwMessage;

    public CoinItemConfig(SkeBossPlugin plugin) {
        ConfigurationSection root = plugin.getConfig().getConfigurationSection("coin-item");
        enabled = root == null || root.getBoolean("enabled", true);
        Material mat = Material.matchMaterial(root != null ? root.getString("material", "PAPER") : "PAPER");
        material = mat != null ? mat : Material.PAPER;
        customModelString = root != null ? root.getString("custom-model-string", "coin") : "coin";
        customModelData = root != null ? root.getInt("custom-model-data", 0) : 0;
        requireModelData = root == null || root.getBoolean("require-model-data", true);
        displayName = root != null ? root.getString("display-name", "&e&l코인") : "&e&l코인";
        nameKeywords = root != null ? root.getStringList("name-keywords") : List.of("코인", "coin");
        Material projMat = Material.matchMaterial(
                root != null ? root.getString("projectile.material", "PAPER") : "PAPER");
        projectileMaterial = projMat != null ? projMat : Material.PAPER;
        ConfigurationSection projectile = root != null ? root.getConfigurationSection("projectile") : null;
        projectileModelString = projectile != null && projectile.contains("custom-model-string")
                ? projectile.getString("custom-model-string")
                : customModelString;
        projectileModelData = projectile != null
                ? projectile.getInt("custom-model-data", customModelData)
                : customModelData;
        throwSpeed = root != null ? root.getDouble("throw-speed", 1.0) : 1.0;
        consume = root == null || root.getBoolean("consume", true);
        cooldownSeconds = root != null ? root.getInt("cooldown-seconds", 0) : 0;
        throwMessage = root != null
                ? root.getString("throw-message", "&e&l◆ 코인 &7던짐!")
                : "&e&l◆ 코인 &7던짐!";
    }

    public boolean isEnabled() {
        return enabled;
    }

    public Material getMaterial() {
        return material;
    }

    public String getCustomModelString() {
        return customModelString;
    }

    public int getCustomModelData() {
        return customModelData;
    }

    public boolean isRequireModelData() {
        return requireModelData;
    }

    public boolean hasModelMarker() {
        return (customModelString != null && !customModelString.isBlank()) || customModelData != 0;
    }

    public String getDisplayName() {
        return displayName;
    }

    public List<String> getNameKeywords() {
        return nameKeywords;
    }

    public Material getProjectileMaterial() {
        return projectileMaterial;
    }

    public String getProjectileModelString() {
        return projectileModelString;
    }

    public int getProjectileModelData() {
        return projectileModelData;
    }

    public double getThrowSpeed() {
        return throwSpeed;
    }

    public boolean isConsume() {
        return consume;
    }

    public int getCooldownSeconds() {
        return cooldownSeconds;
    }

    public String getThrowMessage() {
        return throwMessage;
    }

    public String coloredDisplayName() {
        return TextUtil.color(displayName);
    }
}
