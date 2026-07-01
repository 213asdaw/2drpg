package com.skeboss.coin;

import com.skeboss.SkeBossPlugin;
import com.skeboss.util.TextUtil;
import org.bukkit.Bukkit;
import org.bukkit.ChatColor;
import org.bukkit.Location;
import org.bukkit.Material;
import org.bukkit.Sound;
import org.bukkit.entity.Display;
import org.bukkit.entity.ItemDisplay;
import org.bukkit.entity.Player;
import org.bukkit.entity.Snowball;
import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.meta.ItemMeta;
import org.bukkit.scheduler.BukkitRunnable;
import org.bukkit.util.Transformation;
import org.bukkit.util.Vector;
import org.joml.AxisAngle4f;
import org.joml.Vector3f;

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
            if (config.getCustomModelData() != 0) {
                meta.setCustomModelData(config.getCustomModelData());
            }
            item.setItemMeta(meta);
        }
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
        if (config.isRequireModelData() && config.getCustomModelData() != 0) {
            if (!meta.hasCustomModelData() || meta.getCustomModelData() != config.getCustomModelData()) {
                return false;
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
        return config.isRequireModelData()
                && meta.hasCustomModelData()
                && meta.getCustomModelData() == config.getCustomModelData();
    }

    public boolean tryThrow(Player player, ItemStack sourceItem) {
        if (!config.isEnabled() || !isCoinItem(sourceItem)) {
            return false;
        }
        if (isOnCooldown(player)) {
            TextUtil.actionBar(player, "&c쿨타임 &f" + cooldownLeftSeconds(player) + "초");
            return true;
        }

        Location spawn = player.getEyeLocation();
        Vector direction = spawn.getDirection().normalize().multiply(config.getThrowSpeed());
        ItemStack visual = createProjectileItem();

        Snowball snowball = player.getWorld().spawn(spawn, Snowball.class, projectile -> {
            projectile.setShooter(player);
            projectile.setVelocity(direction);
            projectile.setItem(visual);
        });

        attachVisualFollower(snowball, visual.clone());
        player.getWorld().playSound(spawn, Sound.ENTITY_SNOWBALL_THROW, 0.9f, 1.1f);
        TextUtil.actionBar(player, config.getThrowMessage());

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
        ItemMeta meta = item.getItemMeta();
        if (meta != null && config.getProjectileModelData() != 0) {
            meta.setCustomModelData(config.getProjectileModelData());
            item.setItemMeta(meta);
        }
        return item;
    }

    private void attachVisualFollower(Snowball snowball, ItemStack visual) {
        ItemDisplay display = snowball.getWorld().spawn(snowball.getLocation(), ItemDisplay.class, entity -> {
            entity.setItemStack(visual);
            entity.setBillboard(Display.Billboard.FIXED);
            entity.setInterpolationDuration(0);
            entity.setTeleportDuration(0);
            entity.setTransformation(new Transformation(
                    new Vector3f(0f, 0f, 0f),
                    new AxisAngle4f(0f, 0f, 1f, 0f),
                    new Vector3f(0.6f, 0.6f, 0.6f),
                    new AxisAngle4f(0f, 0f, 1f, 0f)
            ));
        });

        UUID snowballId = snowball.getUniqueId();
        UUID displayId = display.getUniqueId();
        new BukkitRunnable() {
            @Override
            public void run() {
                var projectile = Bukkit.getEntity(snowballId);
                var marker = Bukkit.getEntity(displayId);
                if (!(projectile instanceof Snowball ball) || !ball.isValid() || ball.isDead()) {
                    if (marker != null) {
                        marker.remove();
                    }
                    cancel();
                    return;
                }
                if (marker == null || !marker.isValid()) {
                    cancel();
                    return;
                }
                Location loc = ball.getLocation();
                marker.teleport(loc);
                marker.setRotation(loc.getYaw(), loc.getPitch());
            }
        }.runTaskTimer(plugin, 0L, 1L);
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
