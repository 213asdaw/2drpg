package com.skeboss.listener;

import com.skeboss.SkeBossPlugin;
import com.skeboss.weapon.WeaponManager;
import org.bukkit.ChatColor;
import org.bukkit.entity.Player;
import org.bukkit.event.EventHandler;
import org.bukkit.event.EventPriority;
import org.bukkit.event.Listener;
import org.bukkit.event.block.Action;
import org.bukkit.event.player.PlayerAnimationEvent;
import org.bukkit.event.player.PlayerAnimationType;
import org.bukkit.event.player.PlayerInteractEvent;
import org.bukkit.event.player.PlayerItemHeldEvent;
import org.bukkit.event.player.PlayerQuitEvent;
import org.bukkit.inventory.EquipmentSlot;
import org.bukkit.inventory.ItemStack;

import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

/**
 * untitled 불꽃검기 등이 손에 든 아이템을 검기로 바꿔놓고 복구 못 할 때 인조 무기를 되돌림.
 */
public final class WeaponItemGuard implements Listener {

    private final SkeBossPlugin plugin;
    private final WeaponManager weaponManager;
    private final Map<UUID, ItemStack> backups = new ConcurrentHashMap<>();

    public WeaponItemGuard(SkeBossPlugin plugin, WeaponManager weaponManager) {
        this.plugin = plugin;
        this.weaponManager = weaponManager;
    }

    @EventHandler(priority = EventPriority.LOWEST, ignoreCancelled = true)
    public void onInteract(PlayerInteractEvent event) {
        if (event.getHand() != EquipmentSlot.HAND) {
            return;
        }
        Action action = event.getAction();
        if (action != Action.RIGHT_CLICK_AIR && action != Action.RIGHT_CLICK_BLOCK
                && action != Action.LEFT_CLICK_AIR && action != Action.LEFT_CLICK_BLOCK) {
            return;
        }
        backupIfWeapon(event.getPlayer());
    }

    @EventHandler(priority = EventPriority.LOWEST, ignoreCancelled = true)
    public void onSwing(PlayerAnimationEvent event) {
        if (event.getAnimationType() != PlayerAnimationType.ARM_SWING) {
            return;
        }
        backupIfWeapon(event.getPlayer());
    }

    @EventHandler(priority = EventPriority.MONITOR, ignoreCancelled = true)
    public void onSlotChange(PlayerItemHeldEvent event) {
        backups.remove(event.getPlayer().getUniqueId());
    }

    @EventHandler
    public void onQuit(PlayerQuitEvent event) {
        backups.remove(event.getPlayer().getUniqueId());
    }

    private void backupIfWeapon(Player player) {
        if (!weaponManager.getWeaponConfig().isRestoreAfterSkill()) {
            return;
        }

        ItemStack hand = player.getInventory().getItemInMainHand();
        if (!weaponManager.isInteractWeapon(hand)) {
            return;
        }

        backups.put(player.getUniqueId(), hand.clone());
        scheduleRestoreChecks(player);
    }

    private void scheduleRestoreChecks(Player player) {
        UUID id = player.getUniqueId();
        for (long delay : new long[]{1L, 3L, 5L, 10L, 20L, 40L, 60L}) {
            plugin.getServer().getScheduler().runTaskLater(plugin, () -> tryRestore(player, id), delay);
        }
    }

    private void tryRestore(Player player, UUID id) {
        if (!player.isOnline()) {
            backups.remove(id);
            return;
        }

        ItemStack backup = backups.get(id);
        if (backup == null) {
            return;
        }

        ItemStack current = player.getInventory().getItemInMainHand();
        if (weaponManager.isInteractWeapon(current)) {
            backups.remove(id);
            return;
        }

        if (!shouldRestore(backup, current)) {
            return;
        }

        player.getInventory().setItemInMainHand(backup.clone());
        backups.remove(id);
    }

    private boolean shouldRestore(ItemStack backup, ItemStack current) {
        if (current == null || current.getType().isAir()) {
            return true;
        }

        String currentName = displayName(current);
        for (String keyword : weaponManager.getWeaponConfig().getRestoreKeywords()) {
            if (!keyword.isBlank() && currentName.contains(keyword)) {
                return true;
            }
        }

        if (current.getType() != backup.getType()) {
            return true;
        }

        return weaponManager.getNbtWeaponId(current) == null
                && weaponManager.getNbtWeaponId(backup) != null;
    }

    private static String displayName(ItemStack item) {
        if (item == null || !item.hasItemMeta() || !item.getItemMeta().hasDisplayName()) {
            return "";
        }
        return ChatColor.stripColor(item.getItemMeta().getDisplayName());
    }
}
