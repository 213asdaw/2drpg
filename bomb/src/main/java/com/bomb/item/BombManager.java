package com.bomb.item;

import com.bomb.BombPlugin;
import com.bomb.util.TextUtil;
import org.bukkit.ChatColor;
import org.bukkit.Location;
import org.bukkit.NamespacedKey;
import org.bukkit.Sound;
import org.bukkit.entity.Player;
import org.bukkit.entity.Snowball;
import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.meta.ItemMeta;
import org.bukkit.persistence.PersistentDataType;
import org.bukkit.util.Vector;

import java.util.HashMap;
import java.util.Map;
import java.util.UUID;

public final class BombManager {

    private final BombPlugin plugin;
    private final NamespacedKey bombProjectileKey;
    private BombItemConfig config;
    private final Map<UUID, Long> cooldowns = new HashMap<>();

    public BombManager(BombPlugin plugin) {
        this.plugin = plugin;
        this.bombProjectileKey = new NamespacedKey(plugin, "bomb_projectile");
        reload();
    }

    public void reload() {
        config = new BombItemConfig(plugin);
    }

    public BombItemConfig getConfig() {
        return config;
    }

    public NamespacedKey getBombProjectileKey() {
        return bombProjectileKey;
    }

    public ItemStack createBombItem(int amount) {
        ItemStack item = new ItemStack(config.getMaterial(), Math.max(1, amount));
        ItemMeta meta = item.getItemMeta();
        if (meta != null) {
            meta.setDisplayName(config.coloredDisplayName());
            if (!config.getLore().isEmpty()) {
                meta.setLore(config.getLore().stream().map(TextUtil::color).toList());
            }
            item.setItemMeta(meta);
        }
        ModelDataHelper.apply(item, config.getCustomModelData());
        return item;
    }

    public boolean isBombItem(ItemStack item) {
        if (item == null || item.getType().isAir() || item.getType() != config.getMaterial()) {
            return false;
        }
        if (!item.hasItemMeta()) {
            return false;
        }
        ItemMeta meta = item.getItemMeta();
        if (ModelDataHelper.matches(meta, config.getCustomModelData())) {
            return true;
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
        return false;
    }

    public boolean tryThrow(Player player, ItemStack sourceItem) {
        if (!isBombItem(sourceItem)) {
            return false;
        }
        if (config.isBlockSneakThrow() && player.isSneaking()) {
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
            projectile.getPersistentDataContainer().set(bombProjectileKey, PersistentDataType.BYTE, (byte) 1);
        });

        player.getWorld().playSound(spawn, Sound.ENTITY_BLAZE_SHOOT, 0.9f, 0.85f);
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

    public void explodeBomb(Location location, Player source) {
        if (location == null || location.getWorld() == null) {
            return;
        }
        location.getWorld().playSound(location, Sound.ENTITY_GENERIC_EXPLODE, 1.0f, 1.0f);
        location.getWorld().createExplosion(
                location,
                config.getExplosionPower(),
                config.isSetFire(),
                config.isBreakBlocks(),
                source
        );
    }

    private ItemStack createProjectileItem() {
        ItemStack item = new ItemStack(config.getProjectileMaterial());
        ModelDataHelper.apply(item, config.getProjectileModelData());
        return item;
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
