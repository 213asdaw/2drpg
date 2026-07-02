package com.bomb.item;

import com.bomb.BombPlugin;
import com.bomb.util.TextUtil;
import org.bukkit.Material;
import org.bukkit.configuration.ConfigurationSection;

import java.util.List;

public final class BombItemConfig {

    private final boolean blockSneakThrow;
    private final Material material;
    private final int customModelData;
    private final String displayName;
    private final List<String> nameKeywords;
    private final List<String> lore;
    private final Material projectileMaterial;
    private final int projectileModelData;
    private final double throwSpeed;
    private final boolean consume;
    private final int cooldownSeconds;
    private final String throwMessage;
    private final float explosionPower;
    private final boolean breakBlocks;
    private final boolean setFire;

    public BombItemConfig(BombPlugin plugin) {
        ConfigurationSection root = plugin.getConfig().getConfigurationSection("bomb-item");
        Material mat = Material.matchMaterial(root != null ? root.getString("material", "BLAZE_ROD") : "BLAZE_ROD");
        material = mat != null ? mat : Material.BLAZE_ROD;
        customModelData = root != null ? root.getInt("custom-model-data", 1003) : 1003;
        displayName = root != null ? root.getString("display-name", "&c&l폭탄마의 폭탄") : "&c&l폭탄마의 폭탄";
        nameKeywords = root != null ? root.getStringList("name-keywords") : List.of("폭탄마의 폭탄");
        lore = root != null ? root.getStringList("lore") : List.of();
        throwSpeed = root != null ? root.getDouble("throw-speed", 1.2) : 1.2;
        consume = root != null && root.getBoolean("consume", false);
        cooldownSeconds = root != null ? root.getInt("cooldown-seconds", 3) : 3;
        throwMessage = root != null ? root.getString("throw-message", "&c&l◆ 폭탄 &7던짐!") : "&c&l◆ 폭탄 &7던짐!";
        blockSneakThrow = root == null || root.getBoolean("block-sneak-throw", true);

        ConfigurationSection projectile = plugin.getConfig().getConfigurationSection("projectile");
        Material projMat = Material.matchMaterial(
                projectile != null ? projectile.getString("material", "BLAZE_ROD") : "BLAZE_ROD");
        projectileMaterial = projMat != null ? projMat : Material.BLAZE_ROD;
        projectileModelData = projectile != null
                ? projectile.getInt("custom-model-data", customModelData)
                : customModelData;

        ConfigurationSection explosion = plugin.getConfig().getConfigurationSection("explosion");
        explosionPower = (float) (explosion != null ? explosion.getDouble("power", 3.0) : 3.0);
        breakBlocks = explosion != null && explosion.getBoolean("break-blocks", false);
        setFire = explosion != null && explosion.getBoolean("set-fire", false);
    }

    public boolean isBlockSneakThrow() {
        return blockSneakThrow;
    }

    public Material getMaterial() {
        return material;
    }

    public int getCustomModelData() {
        return customModelData;
    }

    public String getDisplayName() {
        return displayName;
    }

    public List<String> getNameKeywords() {
        return nameKeywords;
    }

    public List<String> getLore() {
        return lore;
    }

    public Material getProjectileMaterial() {
        return projectileMaterial;
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

    public float getExplosionPower() {
        return explosionPower;
    }

    public boolean isBreakBlocks() {
        return breakBlocks;
    }

    public boolean isSetFire() {
        return setFire;
    }

    public String coloredDisplayName() {
        return TextUtil.color(displayName);
    }
}
