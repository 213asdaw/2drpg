package com.skeboss.coin;

import com.skeboss.SkeBossPlugin;
import com.skeboss.util.TextUtil;
import org.bukkit.ChatColor;
import org.bukkit.Location;
import org.bukkit.Sound;
import org.bukkit.entity.Player;
import org.bukkit.entity.Snowball;
import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.meta.ItemMeta;
import org.bukkit.util.Vector;

import java.util.HashMap;
import java.util.Map;
import java.util.UUID;

public final class CoinManager {

    private final SkeBossPlugin plugin;
    private CoinItemConfig config;
    private final Map<UUID, Long> cooldowns = new HashMap<>();

    public CoinManager(SkeBossPlugin plugin) {
        this.plugin = plugin;
        reload();
    }

    public void reload() {
        config = new CoinItemConfig(plugin);
    }

    public CoinItemConfig getConfig() {
        return config;
    }

    public ItemStack createCoinItem(int amount) {
        ItemStack item = new ItemStack(config.getMaterial(), Math.max(1, amount));
        ItemMeta meta = item.getItemMeta();
        if (meta != null) {
            meta.setDisplayName(config.coloredDisplayName());
            item.setItemMeta(meta);
        }
        CoinModelDataHelper.apply(item, modelStringOrNull(config.getCustomModelString()), config.getCustomModelData());
        return item;
    }

    public boolean isCoinItem(ItemStack item) {
        if (!config.isEnabled() || item == null || item.getType().isAir()) {
            return false;
        }
        if (item.getType() != config.getMaterial()) {
            return false;
        }
        if (!item.hasItemMeta()) {
            return false;
        }
        ItemMeta meta = item.getItemMeta();
        if (config.isRequireModelData() && config.hasModelMarker()) {
            if (CoinModelDataHelper.matches(meta, modelStringOrNull(config.getCustomModelString()), config.getCustomModelData())) {
                return true;
            }
        }
        if (meta.hasDisplayName()) {
            String name = ChatColor.stripColor(meta.getDisplayName()).trim();
            String configName = TextUtil.stripColor(config.getDisplayName());
            if (!configName.isEmpty() && name.equalsIgnoreCase(configName)) {
                return true;
            }
            for (String keyword : config.getNameKeywords()) {
                if (!keyword.isBlank() && name.toLowerCase().contains(keyword.toLowerCase())) {
                    return true;
                }
            }
        }
        return !config.isRequireModelData();
    }

    public boolean tryThrow(Player player, ItemStack sourceItem) {
        if (!config.isEnabled() || !isCoinItem(sourceItem)) {
            return false;
        }
        if (isOnCooldown(player)) {
            TextUtil.message(player, "&c쿨타임 &f" + cooldownLeftSeconds(player) + "초");
            return true;
        }

        Location spawn = player.getEyeLocation();
        Vector direction = spawn.getDirection().normalize().multiply(config.getThrowSpeed());
        ItemStack visual = createProjectileItem();

        player.getWorld().spawn(spawn, Snowball.class, projectile -> {
            projectile.setShooter(player);
            projectile.setVelocity(direction);
            projectile.setItem(visual);
        });

        player.getWorld().playSound(spawn, Sound.ENTITY_SNOWBALL_THROW, 0.9f, 1.1f);
        TextUtil.message(player, config.getThrowMessage());

        if (config.isConsume()) {
            sourceItem.setAmount(sourceItem.getAmount() - 1);
        }
        if (config.getCooldownSeconds() > 0) {
            cooldowns.put(player.getUniqueId(),
                    System.currentTimeMillis() + config.getCooldownSeconds() * 1000L);
        }
        return true;
    }

    private ItemStack createProjectileItem() {
        ItemStack item = new ItemStack(config.getProjectileMaterial());
        CoinModelDataHelper.apply(item, modelStringOrNull(config.getProjectileModelString()), config.getProjectileModelData());
        return item;
    }

    private static String modelStringOrNull(String value) {
        if (value == null || value.isBlank()) {
            return null;
        }
        return value;
    }

    private boolean isOnCooldown(Player player) {
        return cooldownLeftMs(player) > 0;
    }

    private long cooldownLeftSeconds(Player player) {
        return (cooldownLeftMs(player) + 999) / 1000;
    }

    private long cooldownLeftMs(Player player) {
        Long readyAt = cooldowns.get(player.getUniqueId());
        if (readyAt == null) {
            return 0;
        }
        return Math.max(0, readyAt - System.currentTimeMillis());
    }
}
